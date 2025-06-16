using System.Globalization;

namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Trida pro export dekodovanych UART bajtu do CSV souboru.
/// </summary>
public class UartExporter {
    private readonly List<UartDecodedByte> decodedBytes;
    private readonly Dictionary<string, string>? channelRenameMap;
    private readonly UartProtocolAnalyzer? analyzer;

    /// <summary>
    /// Vytvori novou instanci tridy UartExporter.
    /// </summary>
    /// <param name="decodedBytes">Seznam dekodovanych bajtu k exportu.</param>
    /// <param name="channelRenameMap">Volitelna mapa prejmenovani kanalu (napr. CH0 → TX).</param>
    public UartExporter(List<UartDecodedByte> decodedBytes, Dictionary<string, string>? channelRenameMap = null, UartProtocolAnalyzer? analyzer = null) {
        this.decodedBytes = decodedBytes;
        this.channelRenameMap = channelRenameMap;
        this.analyzer = analyzer;
    }

    /// <summary>
    /// Exportuje dekodovana UART data do CSV souboru na zadane ceste.
    /// </summary>
    /// <param name="path">Cesta k vystupnimu CSV souboru.</param>
    public void ExportToCsv(string path) {
        using var writer = new StreamWriter(path);

        // Statistiky
        if (analyzer != null) {
            writer.WriteLine("# Statistiky UART analýzy");
            writer.WriteLine($"# Celkový počet bajtů: {analyzer.TotalBytes}");
            writer.WriteLine($"# Počet bajtů s chybou: {analyzer.ErrorCount}");
            writer.WriteLine($"# Průměrná délka bajtu: {analyzer.AvgDurationUs:F1} µs");
            writer.WriteLine($"# Délka bajtu (min/max): {analyzer.MinDurationUs:F1} / {analyzer.MaxDurationUs:F1} µs");
            writer.WriteLine($"# Odhadovaná rychlost: {analyzer.EstimatedBaudRate:F0} baud");
            writer.WriteLine($"# Odhadovaná délka bitu: {analyzer.EstimatedBitTimeUs:F2} µs");
            writer.WriteLine($"# Počet přenosů: {analyzer.TransferCount}");
            writer.WriteLine($"# Průměrná mezera mezi bajty: {analyzer.AvgGapUs:F1} µs");
            writer.WriteLine($"# Mezera mezi bajty (min/max): {analyzer.MinGapUs:F1} / {analyzer.MaxGapUs:F1} µs");
            writer.WriteLine(); 
        }

        writer.WriteLine("Timestamp [s];Channel;Byte (hex);Byte (dec);ASCII;Error");

        foreach (var b in decodedBytes) {
            string timestamp = b.Timestamp.ToString("F9", CultureInfo.InvariantCulture);
            string originalChannel = b.Channel ?? "Unknown";
            string renamedChannel = channelRenameMap != null && channelRenameMap.ContainsKey(originalChannel)
                ? channelRenameMap[originalChannel]
                : originalChannel;
            string hex = $"0x{b.Value:X2}";
            string dec = b.Value.ToString();
            string ascii = (b.Value >= 32 && b.Value <= 126)
                ? ((char)b.Value).ToString()
                : $"\\x{b.Value:X2}";
            string error = b.Error ?? "";

            writer.WriteLine($"{timestamp};{renamedChannel};{hex};{dec};{ascii};{error}");
        }
    }
}
