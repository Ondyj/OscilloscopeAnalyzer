namespace OscilloscopeCLI.Protocols {
    /// <summary>
    /// Rozhrani pro nastaveni protokolu, ktere definuje nazev protokolu.
    /// </summary>
    public interface IProtocolSettings {
        /// <summary>
        /// Nazev protokolu.
        /// </summary>
        string ProtocolName { get; }
    }
}
