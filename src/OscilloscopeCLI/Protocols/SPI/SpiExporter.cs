using System.Globalization;

namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Trida pro export dekodovanych SPI bajtu do CSV souboru.
/// </summary>
public class SpiExporter {
    private readonly List<SpiDecodedByte> decodedBytes; // Seznam dekodovanych SPI bajtu
    private readonly bool hasMiso; // Priznak, zda byl dostupny kanal MISO

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
            writer.WriteLine("Timestamp [s];MOSI (hex);MOSI (dec);MISO (hex);MISO (dec);ASCII;Error");
        else
            writer.WriteLine("Timestamp [s];MOSI (hex);MOSI (dec);ASCII;Error");

        foreach (var b in decodedBytes) {
            string timestamp = b.Timestamp.ToString("F9", CultureInfo.InvariantCulture);
            string mosiHex = $"0x{b.ValueMOSI:X2}";
            string mosiDec = b.ValueMOSI.ToString();
            string asciiChar = (b.ValueMOSI >= 32 && b.ValueMOSI <= 126)
                ? ((char)b.ValueMOSI).ToString()
                : $"\\x{b.ValueMOSI:X2}";
            string error = b.Error ?? "";

            if (hasMiso) {
                string misoHex = $"0x{b.ValueMISO:X2}";
                string misoDec = b.ValueMISO.ToString();
                writer.WriteLine($"{timestamp};{mosiHex};{mosiDec};{misoHex};{misoDec};{asciiChar};{error}");
            } else {
                writer.WriteLine($"{timestamp};{mosiHex};{mosiDec};{asciiChar};{error}");
            }
        }
    }
}
