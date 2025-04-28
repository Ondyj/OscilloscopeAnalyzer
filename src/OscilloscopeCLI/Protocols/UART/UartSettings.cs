namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Nastaveni pro analyzator UART protokolu.
/// </summary>
public class UartSettings : IProtocolSettings {
    /// <summary>
    /// Nazev protokolu (UART).
    /// </summary>
    public string ProtocolName => "UART";

    /// <summary>
    /// Baud rate (rychlost prenosu dat).
    /// </summary>
    public int BaudRate { get; set; }

    /// <summary>
    /// Pocet datovych bitu v jednom prenosu.
    /// </summary>
    public int DataBits { get; set; }

    /// <summary>
    /// Parita (None, Even, Odd).
    /// </summary>
    public Parity Parity { get; set; }

    /// <summary>
    /// Pocet stop bitu.
    /// </summary>
    public int StopBits { get; set; }

    /// <summary>
    /// Udava, zda je idle uroven vysoká (true = vysoká, false = nízká).
    /// </summary>
    public bool IdleLevelHigh { get; set; }
}

/// <summary>
/// Typy parit (None, Even, Odd) pro UART.
/// </summary>
public enum Parity {
    /// <summary>
    /// Bez parity.
    /// </summary>
    None,

    /// <summary>
    /// Parita typu "Even" (suda).
    /// </summary>
    Even,

    /// <summary>
    /// Parita typu "Odd" (lichá).
    /// </summary>
    Odd
}
