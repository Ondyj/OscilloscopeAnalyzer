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
    public int TransferCount { get; private set; } = 0;
    public double AvgTransferDurationUs { get; private set; } = 0.0;

    public string ProtocolName => "SPI"; // Nazev analyzovaneho protokolu

    private SpiMatchSearcher matchSearcher; // Vyhledavac podle hodnoty
    private SpiExporter exporter; // Exporter do CSV
    public SpiSettings Settings => settings;

    private readonly SpiChannelMapping mapping;

    public int TotalBytes { get; private set; }
    public int ErrorCount { get; private set; }
    public double AvgDurationUs { get; private set; }
    public double MinDurationUs { get; private set; }
    public double MaxDurationUs { get; private set; }
    public double EstimatedBitRate { get; private set; }
    public double EstimatedBitTimeUs { get; private set; }
    public int MisoByteCount { get; private set; }
    public double MinCsGapUs { get; private set; }
    public double MaxCsGapUs { get; private set; }
    public double AvgDelayToFirstEdgeUs { get; private set; }
    public double MinDelayToFirstEdgeUs { get; private set; }
    public double MaxDelayToFirstEdgeUs { get; private set; }
    public double AvgCsGapUs { get; private set; } = 0.0;
    public double AvgEdgeDelayUs { get; private set; } = 0.0;

    /// <summary>
    /// Vytvori novou instanci analyzatoru SPI protokolu.
    /// </summary>
    /// <param name="signalData">Vstupni signalova data ve formatu: kanal -> seznam (cas, hodnota).</param>
    /// <param name="settings">Nastaveni parametru SPI komunikace (CPOL, CPHA, BitsPerWord).</param>
    /// <param name="mapping">Mapovani fyzickych kanalu na funkce protokolu (CS, SCLK, MOSI, MISO).</param>
    public SpiProtocolAnalyzer(Dictionary<string, List<(double Time, double Value)>> signalData, SpiSettings settings, SpiChannelMapping mapping)
    {
        this.signalData = signalData;
        this.settings = settings;
        this.mapping = mapping;
        hasMiso = !string.IsNullOrEmpty(mapping.Miso) && signalData.ContainsKey(mapping.Miso);

        matchSearcher = new SpiMatchSearcher(DecodedBytes);
        exporter = new SpiExporter(DecodedBytes, hasMiso, this);
    }

    /// <summary>
    /// Provede analyzu SPI komunikace.
    /// </summary>
    public void Analyze() {
        var globalWatch = Stopwatch.StartNew();
        DecodedBytes.Clear();

        var sclkAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.Clock);
        var mosiAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.Mosi);

        DigitalSignalAnalyzer? csAnalyzer = null;
        if (!string.IsNullOrEmpty(mapping.ChipSelect) && signalData.ContainsKey(mapping.ChipSelect))
            csAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.ChipSelect);

        DigitalSignalAnalyzer? misoAnalyzer = null;
        hasMiso = !string.IsNullOrEmpty(mapping.Miso) && signalData.ContainsKey(mapping.Miso);
        if (hasMiso)
            misoAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.Miso!);

        // Detekuj pouze relevantní hrany dle CPOL/CPHA
        var sclkEdges = sclkAnalyzer.DetectTransitions().Where(t => {
            if (!settings.Cpha)
                return settings.Cpol ? t.From == 1 && t.To == 0 : t.From == 0 && t.To == 1;
            else
                return settings.Cpol ? t.From == 0 && t.To == 1 : t.From == 1 && t.To == 0;
        }).ToList();

        // Získání CS přenosových oken
        List<(double StartTime, double EndTime)> transferWindows;
        if (csAnalyzer != null) {
            transferWindows = csAnalyzer.GetConstantLevelSegments().Where(seg => seg.Value == 0)
                .Select(seg => (seg.StartTime, seg.EndTime)).ToList();
        } else {
            double start = signalData[mapping.Clock].First().Time;
            double end = signalData[mapping.Clock].Last().Time;
            transferWindows = new() { (start, end) };
        }

        int edgeIndex = 0;
        var mosiReader = new SignalReader(mosiAnalyzer.GetSamples());
        SignalReader? misoReader = hasMiso && misoAnalyzer != null ? new SignalReader(misoAnalyzer.GetSamples()) : null;

        List<double> delaysToFirstEdge = new();
        List<double> csGaps = new();
        double? lastCsEnd = null;

        foreach (var (startTime, endTime) in transferWindows) {
            if (lastCsEnd.HasValue) {
                double gap = startTime - lastCsEnd.Value;
                if (gap > 0)
                    csGaps.Add(gap);
            }
            lastCsEnd = endTime;

            var edges = new List<DigitalTransition>();
            while (edgeIndex < sclkEdges.Count && sclkEdges[edgeIndex].Time < startTime)
                edgeIndex++;
            while (edgeIndex < sclkEdges.Count && sclkEdges[edgeIndex].Time <= endTime)
                edges.Add(sclkEdges[edgeIndex++]);

            if (edges.Count > 0) {
                double delay = edges.First().Time - startTime;
                if (delay >= 0)
                    delaysToFirstEdge.Add(delay);
            }

            AnalyzeTransfer(edges, mosiReader, misoReader, startTime, endTime);
        }

        // Obecné statistiky
        TransferCount = transferWindows.Count;
        AvgTransferDurationUs = transferWindows.Count > 0 ? transferWindows.Average(t => (t.EndTime - t.StartTime) * 1e6) : 0.0;

        TotalBytes = DecodedBytes.Count;
        ErrorCount = DecodedBytes.Count(b => !string.IsNullOrEmpty(b.Error));
        AvgDurationUs = TotalBytes > 0 ? DecodedBytes.Average(b => (b.EndTime - b.StartTime) * 1e6) : 0;
        MinDurationUs = TotalBytes > 0 ? DecodedBytes.Min(b => (b.EndTime - b.StartTime) * 1e6) : 0;
        MaxDurationUs = TotalBytes > 0 ? DecodedBytes.Max(b => (b.EndTime - b.StartTime) * 1e6) : 0;
        EstimatedBitTimeUs = AvgDurationUs > 0 ? (AvgDurationUs / 8.0) : 0;
        EstimatedBitRate = EstimatedBitTimeUs > 0 ? (1_000_000.0 / EstimatedBitTimeUs) : 0;
        MisoByteCount = DecodedBytes.Count(b => b.HasMISO);

        // Statistiky mezer mezi CS
        AvgCsGapUs = csGaps.Count > 0 ? csGaps.Average() * 1e6 : 0.0;
        MinCsGapUs = csGaps.Count > 0 ? csGaps.Min() * 1e6 : 0.0;
        MaxCsGapUs = csGaps.Count > 0 ? csGaps.Max() * 1e6 : 0.0;

        // Statistiky zpozdeni prvni hrany
        AvgDelayToFirstEdgeUs = delaysToFirstEdge.Count > 0 ? delaysToFirstEdge.Average() * 1e6 : 0.0;
        MinDelayToFirstEdgeUs = delaysToFirstEdge.Count > 0 ? delaysToFirstEdge.Min() * 1e6 : 0.0;
        MaxDelayToFirstEdgeUs = delaysToFirstEdge.Count > 0 ? delaysToFirstEdge.Max() * 1e6 : 0.0;
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
        // 1 Zadne hrany – prenos je zcela neaktivni
        if (edges.Count == 0) {
            DecodedBytes.Add(new SpiDecodedByte {
                Timestamp = startTime,
                StartTime = startTime,
                EndTime = endTime,
                ValueMOSI = 0,
                ValueMISO = 0,
                Error = "žádné přechody – neaktivní hodiny",
                HasMISO = hasMiso
            });
            return;
        }

        // 2 Normalni dekodovani bajtu
        var bitsMosi = new List<bool>();
        var bitsMiso = new List<bool>();
        double? currentByteStart = null;
        int completeBytes = 0;

        foreach (var edge in edges) {
            if (currentByteStart == null)
                currentByteStart = edge.Time;

            bitsMosi.Add(mosiReader.GetStateAt(edge.Time));
            if (hasMiso && misoReader != null)
                bitsMiso.Add(misoReader.GetStateAt(edge.Time));

            if (bitsMosi.Count == settings.BitsPerWord) {
                DecodedBytes.Add(new SpiDecodedByte {
                    Timestamp = edge.Time,
                    StartTime = currentByteStart.Value,
                    EndTime = edge.Time,
                    ValueMOSI = PackBits(bitsMosi),
                    ValueMISO = hasMiso && bitsMiso.Count == settings.BitsPerWord ? PackBits(bitsMiso) : (byte)0x00,
                    Error = null,
                    HasMISO = hasMiso && bitsMiso.Count == settings.BitsPerWord
                });
                bitsMosi.Clear();
                bitsMiso.Clear();
                currentByteStart = null;
                completeBytes++;
            }
        }

        // 3 Neuplny bajt na konci prenosu
        if (bitsMosi.Count > 0) {
            DecodedBytes.Add(new SpiDecodedByte {
                Timestamp = endTime,
                StartTime = currentByteStart ?? startTime,
                EndTime = endTime,
                ValueMOSI = PackBits(bitsMosi),
                ValueMISO = hasMiso ? PackBits(bitsMiso) : (byte)0x00,
                Error = "nekompletní bajt (méně bitů než očekáváno)",
                HasMISO = hasMiso
            });
        }

        // 4 Hrany byly pritomne, ale zadny bajt se nedekodoval 
        if (completeBytes == 0 && edges.Count > 0 && bitsMosi.Count == 0) {
            DecodedBytes.Add(new SpiDecodedByte {
                Timestamp = endTime,
                StartTime = startTime,
                EndTime = endTime,
                ValueMOSI = 0,
                ValueMISO = 0,
                Error = $"nesoulad počtu hran ({edges.Count}) a očekávaných bitů ({settings.BitsPerWord})",
                HasMISO = hasMiso
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

    public void Search(byte[] sequence, ByteFilterMode filterMode) => matchSearcher.Search(sequence, filterMode);
    public bool HasChipSelect => !string.IsNullOrEmpty(mapping.ChipSelect) && signalData.ContainsKey(mapping.ChipSelect);   
    public bool HasMatches() => matchSearcher.HasMatches();
    public int MatchCount => matchSearcher.MatchCount;
    public string GetMatchDisplay(int index) => matchSearcher.GetMatchDisplay(index);
    public double GetMatchTimestamp(int index) => matchSearcher.GetMatchTimestamp(index);
    public void ExportResults(string path) => exporter.ExportToCsv(path);
}
