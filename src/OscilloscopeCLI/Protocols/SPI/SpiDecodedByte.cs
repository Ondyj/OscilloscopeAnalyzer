namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Predstavuje dekodovany bajt zachyceny behem SPI komunikace.
/// </summary>
public class SpiDecodedByte {
    public double Timestamp { get; set; } // Cas zachyceni bajtu v sekundach
    public byte ValueMOSI { get; set; }   // Hodnota prijata na lince MOSI (Master Out Slave In)
    public byte ValueMISO { get; set; }   // Hodnota prijata na lince MISO (Master In Slave Out)
    public string? Error { get; set; }    // Popis chyby pri dekodovani, nebo null
}