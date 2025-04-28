using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Analyzer pro dekodovani SPI komunikace ze signalovych dat.
/// </summary>
public class SpiProtocolAnalyzer : IProtocolAnalyzer, ISearchableAnalyzer, IExportableAnalyzer {
    /// <summary>
    /// Vstupni signalova data (timestamp, hodnota).
    /// </summary>
    private readonly Dictionary<string, List<Tuple<double, double>>> signalData;

    /// <summary>
    /// Nastaveni SPI analyzy (CPOL, CPHA, pocet bitu).
    /// </summary>
    private readonly SpiSettings settings;

    /// <summary>
    /// Udava, zda je dostupny kanal MISO.
    /// </summary>
    private bool hasMiso = false;

    /// <summary>
    /// Seznam dekodovanych bajtu.
    /// </summary>
    public List<SpiDecodedByte> DecodedBytes { get; private set; } = new();

    /// <summary>
    /// Nazev analyzovaneho protokolu.
    /// </summary>
    public string ProtocolName => "SPI";

    private SpiMatchSearcher matchSearcher;
    private SpiExporter exporter;

    /// <summary>
    /// Vytvori novou instanci analyzatoru SPI protokolu.
    /// </summary>
    /// <param name="signalData">Signalova data.</param>
    /// <param name="settings">Nastaveni SPI analyzatoru.</param>
    public SpiProtocolAnalyzer(Dictionary<string, List<Tuple<double, double>>> signalData, SpiSettings settings) {
        this.signalData = signalData;
        this.settings = settings;
        hasMiso = signalData.ContainsKey("CH3") && signalData["CH3"].Count > 0;

        matchSearcher = new SpiMatchSearcher(DecodedBytes);
        exporter = new SpiExporter(DecodedBytes, hasMiso);
    }

    /// <summary>
    /// Provede analyzu SPI komunikace.
    /// </summary>
    public void Analyze() {
        DecodedBytes.Clear();

        var csAnalyzer = new DigitalSignalAnalyzer(signalData, "CH0");
        var sclkAnalyzer = new DigitalSignalAnalyzer(signalData, "CH1");
        var mosiAnalyzer = new DigitalSignalAnalyzer(signalData, "CH2");

        DigitalSignalAnalyzer? misoAnalyzer = null;
        hasMiso = signalData.ContainsKey("CH3") && signalData["CH3"].Count > 0;
        if (hasMiso)
            misoAnalyzer = new DigitalSignalAnalyzer(signalData, "CH3");

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

    /// <summary>
    /// Analyzuje jednotlive SPI prenosy v ramci jednoho CS aktivniho segmentu.
    /// </summary>
    private void AnalyzeTransfer(double startTime, double endTime, List<DigitalTransition> sclkEdges, DigitalSignalAnalyzer mosiAnalyzer, DigitalSignalAnalyzer? misoAnalyzer) {
        var edgesInTransfer = sclkEdges.Where(e => e.Time >= startTime && e.Time <= endTime).ToList();
        var bitsMosi = new List<bool>();
        var bitsMiso = new List<bool>();

        // Pokud neni zadna hrana v ramci CS LOW, zaznamename chybu
        if (edgesInTransfer.Count == 0) {
            DecodedBytes.Add(new SpiDecodedByte {
                Timestamp = startTime,
                ValueMOSI = 0,
                ValueMISO = 0,
                Error = "Prilis kratky prenos (zadne hrany SCLK)"
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

        // Pokud po prenosu zbyvaji nejake nenahrane bity
        if (bitsMosi.Count > 0) {
            DecodedBytes.Add(new SpiDecodedByte {
                Timestamp = endTime,
                ValueMOSI = PackBits(bitsMosi),
                ValueMISO = hasMiso ? PackBits(bitsMiso) : (byte)0x00,
                Error = "Nekompletni bajt (mene bitu nez ocekavano)"
            });
        }

        // Kontrola souhlasu poctu hran a bitu
        if (edgesInTransfer.Count != settings.BitsPerWord && bitsMosi.Count == 0) {
            DecodedBytes.Add(new SpiDecodedByte {
                Timestamp = endTime,
                ValueMOSI = 0,
                ValueMISO = 0,
                Error = $"Nesoulad poctu hran ({edgesInTransfer.Count}) a ocekavanych bitu ({settings.BitsPerWord})"
            });
        }
    }

    /// <summary>
    /// Vrati stav signalu v danem case.
    /// </summary>
    private bool GetBitAtTime(List<SignalSample> samples, double time) {
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

    /// <inheritdoc/>
    public void Search(byte value) => matchSearcher.Search(value);

    /// <inheritdoc/>
    public bool HasMatches() => matchSearcher.HasMatches();

    /// <inheritdoc/>
    public int MatchCount => matchSearcher.MatchCount;

    /// <inheritdoc/>
    public string GetMatchDisplay(int index) => matchSearcher.GetMatchDisplay(index);

    /// <inheritdoc/>
    public double GetMatchTimestamp(int index) => matchSearcher.GetMatchTimestamp(index);

    /// <inheritdoc/>
    public void ExportResults(string path) => exporter.ExportToCsv(path);
}
