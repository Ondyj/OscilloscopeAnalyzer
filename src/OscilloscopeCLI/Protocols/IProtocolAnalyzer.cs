namespace OscilloscopeCLI.Protocols {
    /// <summary>
    /// Rozhrani pro analyzator protokolu.
    /// </summary>
    public interface IProtocolAnalyzer {
        /// <summary>
        /// Nazev analyzovaneho protokolu.
        /// </summary>
        string ProtocolName { get; }

        /// <summary>
        /// Metoda pro provedení analýzy signálu podle daného protokolu.
        /// </summary>
        void Analyze();
    }
}