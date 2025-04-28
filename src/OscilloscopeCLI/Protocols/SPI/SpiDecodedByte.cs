namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Predstavuje dekodovany bajt zachyceny behem SPI komunikace.
/// </summary>
public class SpiDecodedByte {
    /// <summary>
    /// Cas zachyceni bajtu v sekundach.
    /// </summary>
    public double Timestamp { get; set; }

    /// <summary>
    /// Hodnota prijata na lince MOSI (Master Out Slave In).
    /// </summary>
    public byte ValueMOSI { get; set; }

    /// <summary>
    /// Hodnota prijata na lince MISO (Master In Slave Out).
    /// </summary>
    public byte ValueMISO { get; set; }

    /// <summary>
    /// Popis chyby, pokud pri dekodovani doslo k chybe; jinak null.
    /// </summary>
    public string? Error { get; set; }
}