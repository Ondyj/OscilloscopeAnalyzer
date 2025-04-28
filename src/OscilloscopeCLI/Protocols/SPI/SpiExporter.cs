using System.Globalization;

namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Trida pro export dekodovanych SPI bajtu do CSV souboru.
/// </summary>
public class SpiExporter {
    /// <summary>
    /// Seznam dekodovanych SPI bajtu.
    /// </summary>
    private readonly List<SpiDecodedByte> decodedBytes;

    /// <summary>
    /// Priznak, zda byl dostupny kanal MISO.
    /// </summary>
    private readonly bool hasMiso;

    /// <summary>
    /// Vytvori novou instanci tridy SpiExporter.
    /// </summary>
    /// <param name="decodedBytes">Seznam dekodovanych bajtu.</param>
    /// <param name="hasMiso">Priznak, zda je pouzit kanal MISO.</param>
    public SpiExporter(List<SpiDecodedByte> decodedBytes, bool hasMiso) {
        this.decodedBytes = decodedBytes;
        this.hasMiso = hasMiso;
    }

    /// <summary>
    /// Exportuje dekodovana data do CSV souboru na zadane ceste.
    /// </summary>
    /// <param name="path">Cesta k vystupnimu souboru CSV.</param>
    public void ExportToCsv(string path) {
        using var writer = new StreamWriter(path);

        if (hasMiso)
            writer.WriteLine("Timestamp [s];MOSI (hex);MISO (hex);ASCII;Error");
        else
            writer.WriteLine("Timestamp [s];MOSI (hex);ASCII;Error");

        foreach (var b in decodedBytes) {
            string timestamp = b.Timestamp.ToString("F9", CultureInfo.InvariantCulture);
            string mosiHex = $"0x{b.ValueMOSI:X2}";
            string asciiChar = (b.ValueMOSI >= 32 && b.ValueMOSI <= 126)
                ? ((char)b.ValueMOSI).ToString()
                : $"\\x{b.ValueMOSI:X2}";
            string error = b.Error ?? "";

            if (hasMiso) {
                string misoHex = $"0x{b.ValueMISO:X2}";
                writer.WriteLine($"{timestamp};{mosiHex};{misoHex};{asciiChar};{error}");
            } else {
                writer.WriteLine($"{timestamp};{mosiHex};{asciiChar};{error}");
            }
        }
    }
}
