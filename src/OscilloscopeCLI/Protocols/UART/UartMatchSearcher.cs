using System.Globalization;

namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Trida pro vyhledavani UART bajtu podle hodnoty.
/// </summary>
public class UartMatchSearcher {
    private readonly List<UartDecodedByte> decodedBytes; // Seznam dekodovanych UART bajtu
    private List<UartDecodedByte> matches = new(); // Seznam nalezenych vysledku odpovidajicich hledane hodnote


    /// <summary>
    /// Vytvori novou instanci tridy UartMatchSearcher.
    /// </summary>
    /// <param name="decodedBytes">Seznam dekodovanych bajtu pro vyhledavani.</param>
    public UartMatchSearcher(List<UartDecodedByte> decodedBytes) {
        this.decodedBytes = decodedBytes;
    }

    /// <summary>
    /// Vyhleda vsechny bajty, ktere odpovidaji zadane hodnote.
    /// </summary>
    /// <param name="value">Hledana hodnota bajtu.</param>
    public void Search(byte value, ByteFilterMode filterMode) {
        matches = decodedBytes
            .Where(b => b.Value == value)
            .Where(b =>
                filterMode == ByteFilterMode.All ||
                (filterMode == ByteFilterMode.OnlyErrors && !string.IsNullOrEmpty(b.Error)) ||
                (filterMode == ByteFilterMode.NoErrors && string.IsNullOrEmpty(b.Error))
            )
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
    /// <returns>UartDecodedByte odpovidajici hledanemu indexu.</returns>
    public UartDecodedByte GetMatch(int index) {
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
        var match = matches[index];
        string ascii = (match.Value >= 32 && match.Value <= 126) ? ((char)match.Value).ToString() : $"\\x{match.Value:X2}";
        string error = match.Error ?? "žádný";
        string hex = $"0x{match.Value:X2}";
        string dec = match.Value.ToString();
        string timestamp = match.Timestamp.ToString("F9", CultureInfo.InvariantCulture);

        return $"Time: {timestamp}s | HEX: {hex} | DEC: {dec} | ASCII: {ascii} | Error: {error}";
    }

    /// <summary>
    /// Vrati casovou znacku nalezeneho vysledku.
    /// </summary>
    /// <param name="index">Index vysledku.</param>
    /// <returns>Casova znacka v sekundach.</returns>
    public double GetMatchTimestamp(int index) => GetMatch(index).Timestamp;
}