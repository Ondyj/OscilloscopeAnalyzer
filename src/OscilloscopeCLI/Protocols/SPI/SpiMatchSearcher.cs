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
    public void Search(byte[] sequence, ByteFilterMode filterMode) {
        matches = new List<SpiDecodedByte>();
        if (sequence == null || sequence.Length == 0) return;

        var filtered = decodedBytes
            .Where(b =>
                filterMode == ByteFilterMode.All ||
                (filterMode == ByteFilterMode.OnlyErrors && !string.IsNullOrEmpty(b.Error)) ||
                (filterMode == ByteFilterMode.NoErrors && string.IsNullOrEmpty(b.Error)))
            .ToList();

        for (int i = 0; i <= filtered.Count - sequence.Length; i++) {
            bool match = true;
            for (int j = 0; j < sequence.Length; j++) {
                byte valMosi = filtered[i + j].ValueMOSI;
                byte valMiso = filtered[i + j].ValueMISO;

                if (valMosi != sequence[j] && valMiso != sequence[j]) {
                    match = false;
                    break;
                }
            }
            if (match) {
                matches.Add(filtered[i]); // přidáme první bajt sekvence jako výsledek
            }
        }
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