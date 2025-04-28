namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Nastaveni pro analyzator SPI protokolu.
/// </summary>
public class SpiSettings : IProtocolSettings {
    /// <summary>
    /// Nazev protokolu.
    /// </summary>
    public string ProtocolName => "SPI";

    /// <summary>
    /// Hodnota CPOL (Clock Polarity).
    /// True = inverzni logika hodinoveho signalu.
    /// </summary>
    public bool Cpol { get; set; } = false;

    /// <summary>
    /// Hodnota CPHA (Clock Phase).
    /// Udava, zda se data nactou na predni nebo zadni hrane.
    /// </summary>
    public bool Cpha { get; set; } = false;

    /// <summary>
    /// Pocet bitu v jednom prenosu (defaultne 8 bitu).
    /// </summary>
    public int BitsPerWord { get; set; } = 8;
}
