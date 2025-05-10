namespace OscilloscopeCLI.Protocols {

    public enum ByteFilterMode {
        All,
        OnlyErrors,
        NoErrors
    }
    /// <summary>
    /// Rozhrani pro analyzatory, ktere umoznuji hledani a praci s nalezenymi shodami.
    /// </summary>
    public interface ISearchableAnalyzer {
        /// <summary>
        /// Vyhleda vsechny shody pro zadanou hodnotu.
        /// </summary>
        /// <param name="value">Hledana hodnota bajtu.</param>
        void Search(byte[] sequence, ByteFilterMode filterMode);

        /// <summary>
        /// Vrati, zda existuji nejake nalezene shody.
        /// </summary>
        bool HasMatches();

        /// <summary>
        /// Vrati pocet nalezenych shod.
        /// </summary>
        int MatchCount { get; }

        /// <summary>
        /// Vrati textovou reprezentaci nalezene shody pro zobrazeni.
        /// </summary>
        /// <param name="index">Index shody.</param>
        /// <returns>Formatovany retezec s informacemi o shode.</returns>
        string GetMatchDisplay(int index);

        /// <summary>
        /// Vrati casovou znacku nalezene shody.
        /// </summary>
        /// <param name="index">Index shody.</param>
        /// <returns>Casova znacka v sekundach.</returns>
        double GetMatchTimestamp(int index);
    }
}
