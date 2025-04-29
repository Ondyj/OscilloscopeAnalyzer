namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Nastaveni pro analyzator SPI protokolu.
/// </summary>
public class SpiSettings : IProtocolSettings {
    public string ProtocolName => "SPI"; // Nazev protokolu

    public bool Cpol { get; set; } = false; // Hodnota CPOL (inverzni logika hodinoveho signalu)

    public bool Cpha { get; set; } = false; // Hodnota CPHA (urcuje, kdy se nacitaji data)

    public int BitsPerWord { get; set; } = 8; // Pocet bitu v jednom prenosu (vychozi 8)
}
