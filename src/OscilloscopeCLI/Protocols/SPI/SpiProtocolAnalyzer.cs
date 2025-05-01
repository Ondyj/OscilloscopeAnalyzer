using OscilloscopeCLI.Signal;

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
        DecodedBytes.Clear();

        var csAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.ChipSelect);
        var sclkAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.Clock);
        var mosiAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.Mosi);

        DigitalSignalAnalyzer? misoAnalyzer = null;
        hasMiso = !string.IsNullOrEmpty(mapping.Miso) && signalData.ContainsKey(mapping.Miso);
        if (hasMiso)
            misoAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.Miso!);

        var activeTransfers = csAnalyzer.GetConstantLevelSegments().Where(seg => seg.Value == 0).ToList();
        var sclkEdges = sclkAnalyzer.DetectTransitions().Where(t => {
            if (!settings.Cpha)
                return settings.Cpol ? t.From == 1 && t.To == 0 : t.From == 0 && t.To == 1;
            else
                return settings.Cpol ? t.From == 0 && t.To == 1 : t.From == 1 && t.To == 0;
        }).ToList();

        foreach (var transfer in activeTransfers) {
            AnalyzeTransfer(transfer.StartTime, transfer.EndTime, sclkEdges, mosiAnalyzer, misoAnalyzer);
        }
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
    private void AnalyzeTransfer(double startTime, double endTime, List<DigitalTransition> sclkEdges, DigitalSignalAnalyzer mosiAnalyzer, DigitalSignalAnalyzer? misoAnalyzer) {
        var edgesInTransfer = sclkEdges.Where(e => e.Time >= startTime && e.Time <= endTime).ToList();
        var bitsMosi = new List<bool>();
        var bitsMiso = new List<bool>();

        if (edgesInTransfer.Count == 0) {
            DecodedBytes.Add(new SpiDecodedByte {
                Timestamp = startTime,
                ValueMOSI = 0,
                ValueMISO = 0,
                Error = "příliš krátký přenos (žádné hrany SCLK)"
            });
            return;
        }

        foreach (var edge in edgesInTransfer) {
            bitsMosi.Add(GetBitAtTime(mosiAnalyzer.GetSamples(), edge.Time));
            if (hasMiso && misoAnalyzer != null)
                bitsMiso.Add(GetBitAtTime(misoAnalyzer.GetSamples(), edge.Time));

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

        if (edgesInTransfer.Count != settings.BitsPerWord && bitsMosi.Count == 0) {
            DecodedBytes.Add(new SpiDecodedByte {
                Timestamp = endTime,
                ValueMOSI = 0,
                ValueMISO = 0,
                Error = $"nesoulad počtu hran ({edgesInTransfer.Count}) a očekávaných bitů ({settings.BitsPerWord})"
            });
        }
    }


    /// <summary>
    /// Vrati stav signalu v danem case.
    /// </summary>
    private bool GetBitAtTime(List<(double Timestamp, bool State)> samples, double time) {
        for (int i = 1; i < samples.Count; i++) {
            if (samples[i].Timestamp >= time)
                return samples[i - 1].State;
        }
        return samples[^1].State;
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
