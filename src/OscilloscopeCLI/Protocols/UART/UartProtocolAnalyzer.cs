using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Analyzer pro dekodovani UART komunikace ze signalovych dat.
/// </summary>
public class UartProtocolAnalyzer : IProtocolAnalyzer, ISearchableAnalyzer, IExportableAnalyzer {
    /// <summary>
    /// Vstupni signalova data (timestamp, logicka hodnota).
    /// </summary>
    private readonly Dictionary<string, List<SignalSample>> channelSamples;

    /// <summary>
    /// Nastaveni UART analyzy (baud rate, data bits, parita, stop bity, idle uroven).
    /// </summary>
    private readonly UartSettings settings;

    /// <summary>
    /// Seznam dekodovanych bajtu.
    /// </summary>
    public List<UartDecodedByte> DecodedBytes { get; private set; } = new();

    /// <summary>
    /// Nazev analyzovaneho protokolu.
    /// </summary>
    public string ProtocolName => "UART";

    private UartMatchSearcher matchSearcher;
    private UartExporter exporter;

    /// <summary>
    /// Vytvori novou instanci analyzatoru UART protokolu.
    /// </summary>
    /// <param name="signalData">Signalova data.</param>
    /// <param name="settings">Nastaveni UART analyzatoru.</param>
    public UartProtocolAnalyzer(Dictionary<string, List<Tuple<double, double>>> signalData, UartSettings settings) {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.channelSamples = signalData.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(pair => new SignalSample(pair.Item1, pair.Item2 != 0)).ToList()
        );

        matchSearcher = new UartMatchSearcher(DecodedBytes);
        exporter = new UartExporter(DecodedBytes);
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
        exporter = new UartExporter(DecodedBytes);
    }

    /// <summary>
    /// Analyzuje jednotlive signaly v kanale.
    /// </summary>
    private void AnalyzeChannel(string channelName, List<SignalSample> samples, double bitTime, bool idleLevel) {
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
    private UartDecodedByte DecodeByte(List<SignalSample> samples, double startTime, double bitTime, bool idleLevel) {
        byte value = 0;
        string? error = null;

        for (int bitIndex = 0; bitIndex < settings.DataBits; bitIndex++) {
            double sampleTime = startTime + ((bitIndex + 1.5) * bitTime);
            if (GetBitAtTime(samples, sampleTime))
                value |= (byte)(1 << bitIndex);
        }

        if (settings.Parity != Parity.None) {
            if (!CheckParity(samples, startTime, value, bitTime))
                error = "Chyba parity";
        }

        if (!CheckStopBit(samples, startTime, bitTime, idleLevel))
            error = (error != null ? error + " + " : "") + "Chyba stop bitu";

        return new UartDecodedByte {
            Timestamp = startTime,
            Value = value,
            Error = error
        };
    }

    /// <summary>
    /// Zjisti hodnotu signalu v urcitem case.
    /// </summary>
    private bool GetBitAtTime(List<SignalSample> samples, double timestamp) {
        for (int i = 1; i < samples.Count; i++) {
            if (samples[i].Timestamp >= timestamp)
                return samples[i - 1].State;
        }
        return samples[^1].State;
    }

    /// <summary>
    /// Overi spravnost parity bajtu.
    /// </summary>
    private bool CheckParity(List<SignalSample> samples, double startTime, byte value, double bitTime) {
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
    /// Overi spravnost stop bitu.
    /// </summary>
    private bool CheckStopBit(List<SignalSample> samples, double startTime, double bitTime, bool idleLevel) {
        double stopBitTime = GetStopBitTime(startTime, bitTime);
        return GetBitAtTime(samples, stopBitTime) == idleLevel;
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
    public void ExportResults(string outputPath) => exporter.ExportToCsv(outputPath);
}
