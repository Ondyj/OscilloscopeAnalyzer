using OscilloscopeCLI.Signal;
using System.Diagnostics;

namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Analyzer pro dekodovani SPI komunikace ze signalovych dat.
/// </summary>
public class SpiProtocolAnalyzer : IProtocolAnalyzer, ISearchableAnalyzer, IExportableAnalyzer {
    private readonly Dictionary<string, List<(double Time, double Value)>> signalData;  // Vstupni signalova data (timestamp, hodnota)
    private readonly SpiSettings settings; // Nastaveni SPI analyzy (CPOL, CPHA, pocet bitu)
    private bool hasMiso = false; // Udava, zda je dostupny kanal MISO

    public List<SpiDecodedByte> DecodedBytes { get; private set; } = new(); // Seznam dekodovanych bajtu

    public string ProtocolName => "SPI"; // Nazev analyzovaneho protokolu

    private SpiMatchSearcher matchSearcher; // Vyhledavac podle hodnoty
    private SpiExporter exporter; // Exporter do CSV
    public SpiSettings Settings => settings;

    private readonly SpiChannelMapping mapping;

    /// <summary>
    /// Vytvori novou instanci analyzatoru SPI protokolu.
    /// </summary>
    /// <param name="signalData">Vstupni signalova data ve formatu: kanal -> seznam (cas, hodnota).</param>
    /// <param name="settings">Nastaveni parametru SPI komunikace (CPOL, CPHA, BitsPerWord).</param>
    /// <param name="mapping">Mapovani fyzickych kanalu na funkce protokolu (CS, SCLK, MOSI, MISO).</param>
    public SpiProtocolAnalyzer(Dictionary<string, List<(double Time, double Value)>> signalData, SpiSettings settings, SpiChannelMapping mapping) {
        this.signalData = signalData;
        this.settings = settings;
        this.mapping = mapping;
        hasMiso = !string.IsNullOrEmpty(mapping.Miso) && signalData.ContainsKey(mapping.Miso);

        matchSearcher = new SpiMatchSearcher(DecodedBytes);
        exporter = new SpiExporter(DecodedBytes, hasMiso);
    }

    /// <summary>
    /// Provede analyzu SPI komunikace.
    /// </summary>
    public void Analyze() {
        var globalWatch = Stopwatch.StartNew();
        DecodedBytes.Clear();
        Console.WriteLine("[SPI] Zahájena analýza...");

        var sw = Stopwatch.StartNew();
        var csAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.ChipSelect);
        var sclkAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.Clock);
        var mosiAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.Mosi);

        DigitalSignalAnalyzer? misoAnalyzer = null;
        hasMiso = !string.IsNullOrEmpty(mapping.Miso) && signalData.ContainsKey(mapping.Miso);
        if (hasMiso)
            misoAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.Miso!);
        sw.Stop();
        Console.WriteLine($"[SPI] Příprava analyzátorů trvala {sw.Elapsed.TotalMilliseconds:F2} ms");

        sw.Restart();
        var activeTransfers = csAnalyzer.GetConstantLevelSegments().Where(seg => seg.Value == 0).ToList();
        sw.Stop();
        Console.WriteLine($"[SPI] Detekce přenosů (CS LOW) trvala {sw.Elapsed.TotalMilliseconds:F2} ms");

        sw.Restart();
        var sclkEdges = sclkAnalyzer.DetectTransitions().Where(t => {
            if (!settings.Cpha)
                return settings.Cpol ? t.From == 1 && t.To == 0 : t.From == 0 && t.To == 1;
            else
                return settings.Cpol ? t.From == 0 && t.To == 1 : t.From == 1 && t.To == 0;
        }).ToList();
        sw.Stop();
        Console.WriteLine($"[SPI] Detekce hran SCLK trvala {sw.Elapsed.TotalMilliseconds:F2} ms");

        Console.WriteLine($"[SPI] Detekováno {activeTransfers.Count} přenosů (CS aktivní).");
        Console.WriteLine($"[SPI] Celkem {sclkEdges.Count} relevantních hran SCLK pro dekódování.");

        sw.Restart();
        int edgeIndex = 0;
        var mosiReader = new SignalReader(mosiAnalyzer.GetSamples());
        SignalReader? misoReader = hasMiso && misoAnalyzer != null ? new SignalReader(misoAnalyzer.GetSamples()) : null;

        foreach (var transfer in activeTransfers) {
            var edges = new List<DigitalTransition>();

            while (edgeIndex < sclkEdges.Count && sclkEdges[edgeIndex].Time < transfer.StartTime)
                edgeIndex++;

            int localEdgeStart = edgeIndex;
            while (edgeIndex < sclkEdges.Count && sclkEdges[edgeIndex].Time <= transfer.EndTime) {
                edges.Add(sclkEdges[edgeIndex]);
                edgeIndex++;
            }

            AnalyzeTransfer(edges, mosiReader, misoReader, transfer.StartTime, transfer.EndTime);
        }
                sw.Stop();
        Console.WriteLine($"[SPI] Dekódování všech přenosů trvalo {sw.Elapsed.TotalMilliseconds:F2} ms");

        globalWatch.Stop();
        Console.WriteLine($"[SPI] Analýza dokončena za {globalWatch.Elapsed.TotalMilliseconds:F2} ms.");
        Console.WriteLine($"[SPI] Dekódováno {DecodedBytes.Count} bajtů.");
    }

    private static Dictionary<string, List<Tuple<double, double>>> ConvertToTuple(Dictionary<string, List<(double Time, double Value)>> source) {
        return source.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(p => Tuple.Create(p.Time, p.Value)).ToList()
        );
    }

    /// <summary>
    /// Analyzuje jednotlive SPI prenosy v ramci jednoho CS aktivniho segmentu.
    /// </summary>
    private void AnalyzeTransfer(List<DigitalTransition> edges, SignalReader mosiReader, SignalReader? misoReader, double startTime, double endTime) {
        var bitsMosi = new List<bool>();
        var bitsMiso = new List<bool>();

        foreach (var edge in edges) {
            bitsMosi.Add(mosiReader.GetStateAt(edge.Time));
            if (hasMiso && misoReader != null)
                bitsMiso.Add(misoReader.GetStateAt(edge.Time));

            if (bitsMosi.Count == settings.BitsPerWord) {
                DecodedBytes.Add(new SpiDecodedByte {
                    Timestamp = edge.Time,
                    ValueMOSI = PackBits(bitsMosi),
                    ValueMISO = hasMiso && bitsMiso.Count == settings.BitsPerWord ? PackBits(bitsMiso) : (byte)0x00,
                    Error = null
                });
                bitsMosi.Clear();
                bitsMiso.Clear();
            }
        }

        if (bitsMosi.Count > 0) {
            DecodedBytes.Add(new SpiDecodedByte {
                Timestamp = endTime,
                ValueMOSI = PackBits(bitsMosi),
                ValueMISO = hasMiso ? PackBits(bitsMiso) : (byte)0x00,
                Error = "nekompletní bajt (méně bitů než očekáváno)"
            });
        }

        if (edges.Count != settings.BitsPerWord && bitsMosi.Count == 0) {
            DecodedBytes.Add(new SpiDecodedByte {
                Timestamp = endTime,
                ValueMOSI = 0,
                ValueMISO = 0,
                Error = $"nesoulad počtu hran ({edges.Count}) a očekávaných bitů ({settings.BitsPerWord})"
            });
        }
    }

    /// <summary>
    /// Prevede seznam bitu (LSB first) na bajt.
    /// </summary>
    private byte PackBits(List<bool> bits) {
        byte result = 0;
        for (int i = 0; i < bits.Count; i++) {
            if (bits[i])
                result |= (byte)(1 << i);
        }
        return result;
    }

    // --- IMPLEMENTACE INTERFACU ---

    public void Search(byte value) => matchSearcher.Search(value);
    public bool HasMatches() => matchSearcher.HasMatches();
    public int MatchCount => matchSearcher.MatchCount;
    public string GetMatchDisplay(int index) => matchSearcher.GetMatchDisplay(index);
    public double GetMatchTimestamp(int index) => matchSearcher.GetMatchTimestamp(index);
    public void ExportResults(string path) => exporter.ExportToCsv(path);
}
