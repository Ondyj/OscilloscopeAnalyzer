namespace OscilloscopeCLI.Protocols {
    /// <summary>
    /// Rozhrani pro analyzatory, ktere umoznuji export vysledku do souboru.
    /// </summary>
    public interface IExportableAnalyzer {
        /// <summary>
        /// Nazev protokolu, ktery analyzator zpracovava.
        /// Tento nazev je pouzit pro identifikaci protokolu pri exportu vysledku.
        /// </summary>
        string ProtocolName { get; }

        /// <summary>
        /// Exportuje vysledky analyzy do specifikovaneho souboru.
        /// </summary>
        /// <param name="outputPath">Cesta k vystupnimu souboru, kam budou exportovany vysledky.</param>
        void ExportResults(string outputPath);
    }
}
