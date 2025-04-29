using System.Globalization;

namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Trida pro export dekodovanych UART bajtu do CSV souboru.
/// </summary>
public class UartExporter {
    private readonly List<UartDecodedByte> decodedBytes; // Seznam dekodovanych UART bajtu

    /// <summary>
    /// Vytvori novou instanci tridy UartExporter.
    /// </summary>
    /// <param name="decodedBytes">Seznam dekodovanych bajtu k exportu.</param>
    public UartExporter(List<UartDecodedByte> decodedBytes) {
        this.decodedBytes = decodedBytes;
    }

    /// <summary>
    /// Exportuje dekodovana UART data do CSV souboru na zadane ceste.
    /// </summary>
    /// <param name="path">Cesta k vystupnimu CSV souboru.</param>
    public void ExportToCsv(string path) {
        using var writer = new StreamWriter(path);
        writer.WriteLine("Timestamp [s];Channel;Byte (hex);ASCII;Error");

        foreach (var b in decodedBytes) {
            string timestamp = b.Timestamp.ToString("F9", CultureInfo.InvariantCulture);
            string channel = b.Channel ?? "Unknown";
            string hex = $"0x{b.Value:X2}";
            string ascii = (b.Value >= 32 && b.Value <= 126)
                ? ((char)b.Value).ToString()
                : $"\\x{b.Value:X2}";
            string error = b.Error ?? "";

            writer.WriteLine($"{timestamp};{channel};{hex};{ascii};{error}");
        }
    }
}
