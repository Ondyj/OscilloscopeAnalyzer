using System.Globalization;

namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Trida pro vyhledavani SPI bajtu podle hodnoty.
/// </summary>
public class SpiMatchSearcher {
  private readonly List<SpiDecodedByte> decodedBytes; // Seznam dekodovanych SPI bajtu
    private List<SpiDecodedByte> matches = new(); // Nalezene vysledky odpovidajici hledane hodnote

    /// <summary>
    /// Vytvori novou instanci tridy SpiMatchSearcher.
    /// </summary>
    /// <param name="decodedBytes">Seznam dekodovanych bajtu pro vyhledavani.</param>
    public SpiMatchSearcher(List<SpiDecodedByte> decodedBytes) {
        this.decodedBytes = decodedBytes;
    }

    /// <summary>
    /// Vyhleda vsechny bajty, ktere odpovidaji zadane hodnote na MOSI nebo MISO.
    /// </summary>
    /// <param name="value">Hledana hodnota bajtu.</param>
    public void Search(byte value) {
        matches = decodedBytes
            .Where(b => b.ValueMOSI == value || b.ValueMISO == value)
            .ToList();
    }

    /// <summary>
    /// Vrati, zda existuji nejake nalezene vysledky.
    /// </summary>
    public bool HasMatches() => matches.Count > 0;

    /// <summary>
    /// Vrati pocet nalezenych vysledku.
    /// </summary>
    public int MatchCount => matches.Count;

    /// <summary>
    /// Vrati nalezeny vysledek podle indexu.
    /// </summary>
    /// <param name="index">Index vysledku.</param>
    /// <returns>SpiDecodedByte odpovidajici hledanemu indexu.</returns>
    public SpiDecodedByte GetMatch(int index) {
        if (index < 0 || index >= matches.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return matches[index];
    }

    /// <summary>
    /// Vrati textovou reprezentaci nalezeneho vysledku pro zobrazeni.
    /// </summary>
    /// <param name="index">Index vysledku.</param>
    /// <returns>Formatovany retezec s informacemi o vysledku.</returns>
    public string GetMatchDisplay(int index) {
        var match = GetMatch(index);

        string time = match.Timestamp.ToString("F9", CultureInfo.InvariantCulture);

        string hexMosi = $"0x{match.ValueMOSI:X2}";
        string decMosi = match.ValueMOSI.ToString();

        string hexMiso = $"0x{match.ValueMISO:X2}";
        string decMiso = match.ValueMISO.ToString();

        string ascii = (match.ValueMOSI >= 32 && match.ValueMOSI <= 126)
            ? ((char)match.ValueMOSI).ToString()
            : $"\\x{match.ValueMOSI:X2}";

        string error = match.Error ?? "žádný";

        return $"Time: {time}s | MOSI: {hexMosi} ({decMosi}) | MISO: {hexMiso} ({decMiso}) | ASCII: {ascii} | Error: {error}";
    }


    /// <summary>
    /// Vrati casovou znacku nalezeneho vysledku.
    /// </summary>
    /// <param name="index">Index vysledku.</param>
    /// <returns>Casova znacka v sekundach.</returns>
    public double GetMatchTimestamp(int index) => GetMatch(index).StartTime;
}