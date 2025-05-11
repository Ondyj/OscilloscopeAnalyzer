using OscilloscopeCLI.Signal;
using System.Diagnostics;

namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Analyzer pro dekodovani UART komunikace ze signalovych dat.
/// </summary>
public class UartProtocolAnalyzer : IProtocolAnalyzer, ISearchableAnalyzer, IExportableAnalyzer {
    private readonly Dictionary<string, List<(double Timestamp, bool State)>> channelSamples;  // Vstupni signalova data (timestamp, logicka hodnota)
    private Dictionary<string, string>? channelRenameMap;
    private readonly UartSettings settings; // Nastaveni UART analyzy (baud rate, data bits, parita, stop bity, idle uroven)
    public List<UartDecodedByte> DecodedBytes { get; private set; } = new(); // Seznam dekodovanych bajtu
    public string ProtocolName => "UART"; // Nazev analyzovaneho protokolu
    private UartMatchSearcher matchSearcher; // Vyhledavani shod v dekodovanych datech
    private UartExporter exporter; // Export dekodovanych dat do souboru
    public UartSettings Settings => settings;
    

    /// <summary>
    /// Vytvori novou instanci analyzatoru UART protokolu.
    /// </summary>
    /// <param name="signalData">Signalova data.</param>
    /// <param name="settings">Nastaveni UART analyzatoru.</param>
    public UartProtocolAnalyzer(Dictionary<string, List<(double Time, double Value)>> signalData, UartSettings settings, UartChannelMapping? mapping = null) {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        if (mapping is not null) {
            channelRenameMap = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(mapping.Tx)) channelRenameMap[mapping.Tx] = "TX";
            if (!string.IsNullOrEmpty(mapping.Rx)) channelRenameMap[mapping.Rx] = "RX";
        }
        this.channelSamples = signalData.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(p => (p.Time, p.Value > 0.5)).ToList()
        );

        matchSearcher = new UartMatchSearcher(DecodedBytes);
        exporter = new UartExporter(DecodedBytes);
    }

    public void SetChannelRenameMap(Dictionary<string, string> renameMap) {
        channelRenameMap = renameMap;
        exporter = new UartExporter(DecodedBytes, channelRenameMap); // obnovit exporter
    }

    /// <summary>
    /// Provede analyzu UART komunikace.
    /// </summary>
    public void Analyze() {
        DecodedBytes.Clear();
        if (channelSamples.Count == 0) return;

        double bitTime = 1.0 / settings.BaudRate;
        bool idleLevel = settings.IdleLevelHigh;

        foreach (var (channelName, samples) in channelSamples) {
            AnalyzeChannel(channelName, samples, bitTime, idleLevel);
        }

        matchSearcher = new UartMatchSearcher(DecodedBytes);
        exporter = new UartExporter(DecodedBytes, channelRenameMap);
    }

    /// <summary>
    /// Analyzuje jednotlive signaly v kanale.
    /// </summary>
    private void AnalyzeChannel(string channelName, List<(double Timestamp, bool State)> samples, double bitTime, bool idleLevel) {
        int i = 1;
        while (i < samples.Count) {
            var previous = samples[i - 1];
            var current = samples[i];

            if (previous.State == idleLevel && current.State != idleLevel) {
                var decodedByte = DecodeByte(samples, current.Timestamp, bitTime, idleLevel);
                decodedByte.Channel = channelName;
                DecodedBytes.Add(decodedByte);

                double stopBitTime = GetStopBitTime(decodedByte.Timestamp, bitTime);
                while (i < samples.Count && samples[i].Timestamp < stopBitTime)
                    i++;

                continue;
            }
            i++;
        }
    }

    /// <summary>
    /// Dekoduje jeden UART bajt ze vzorku.
    /// </summary>
    private UartDecodedByte DecodeByte(List<(double Timestamp, bool State)> samples, double startTime, double bitTime, bool idleLevel) {
        byte value = 0;
        string? error = null;

        // Overeni start bitu
        bool expectedStartBit = !idleLevel;
        bool actualStartBit = GetBitAtTime(samples, startTime + 0.5 * bitTime);
        if (actualStartBit != expectedStartBit)
            error = "chybný start bit";

        for (int bitIndex = 0; bitIndex < settings.DataBits; bitIndex++) {
            double sampleTime = startTime + ((bitIndex + 1.5) * bitTime);
            if (GetBitAtTime(samples, sampleTime))
                value |= (byte)(1 << bitIndex);
        }

        if (settings.Parity != Parity.None) {
            if (!CheckParity(samples, startTime, value, bitTime))
                error = (error != null ? error + " + " : "") + "chyba parity";
        }

        if (!CheckStopBit(samples, startTime, bitTime, idleLevel))
            error = (error != null ? error + " + " : "") + "chyba stop bitu";

        return new UartDecodedByte {
            Timestamp = startTime,
            StartTime = startTime,
            EndTime = GetStopBitTime(startTime, bitTime),
            Value = value,
            Error = error
        };
    }

    /// <summary>
    /// Zjisti hodnotu signalu v urcitem case.
    /// </summary>
    private bool GetBitAtTime(List<(double Timestamp, bool State)> samples, double timestamp) {
        int low = 0;
        int high = samples.Count - 1;

        while (low <= high) {
            int mid = (low + high) / 2;
            if (samples[mid].Timestamp < timestamp) {
                low = mid + 1;
            } else {
                high = mid - 1;
            }
        }

        int index = Math.Max(0, low - 1);
        return samples[index].State;
    }

    /// <summary>
    /// Overi spravnost parity bajtu.
    /// </summary>
    private bool CheckParity(List<(double Timestamp, bool State)> samples, double startTime, byte value, double bitTime) {
        double parityTime = startTime + ((settings.DataBits + 1.5) * bitTime);
        bool parityBit = GetBitAtTime(samples, parityTime);
        bool calculatedParity = CalculateParity(value);

        return settings.Parity switch {
            Parity.Even => calculatedParity == parityBit,
            Parity.Odd => calculatedParity != parityBit,
            _ => true
        };
    }

    /// <summary>
    /// Overi spravnost vsech stop bitu.
    /// </summary>
    private bool CheckStopBit(List<(double Timestamp, bool State)> samples, double startTime, double bitTime, bool idleLevel) {
        int dataBits = settings.DataBits;
        int parityBits = settings.Parity != Parity.None ? 1 : 0;
        int stopBits = settings.StopBits;

        for (int i = 0; i < stopBits; i++) {
            double stopBitTime = startTime + (dataBits + parityBits + i + 1.5) * bitTime;
            if (GetBitAtTime(samples, stopBitTime) != idleLevel) {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Vypocita casovou znacku pro stop bit.
    /// </summary>
    private double GetStopBitTime(double startTime, double bitTime) {
        int offset = settings.DataBits + (settings.Parity != Parity.None ? 1 : 0) + settings.StopBits;
        return startTime + offset * bitTime;
    }

    /// <summary>
    /// Spocita paritu bajtu.
    /// </summary>
    private bool CalculateParity(byte value) {
        int ones = 0;
        for (int i = 0; i < 8; i++) {
            if ((value & (1 << i)) != 0)
                ones++;
        }
        return (ones % 2) != 0;
    }

    // --- IMPLEMENTACE INTERFACU ---

    public void Search(byte[] sequence, ByteFilterMode filterMode) => matchSearcher.Search(sequence, filterMode);
    public bool HasMatches() => matchSearcher.HasMatches();
    public int MatchCount => matchSearcher.MatchCount;
    public string GetMatchDisplay(int index) => matchSearcher.GetMatchDisplay(index);
    public double GetMatchTimestamp(int index) => matchSearcher.GetMatchTimestamp(index);
    public void ExportResults(string outputPath) => exporter.ExportToCsv(outputPath);
}
