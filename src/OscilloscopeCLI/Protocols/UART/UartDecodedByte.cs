namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Reprezentuje jeden dekodovany bajt prenoseny po protokolu UART.
/// Obsahuje cas, kanal, hodnotu bajtu a pripadnou chybu.
/// </summary>
public class UartDecodedByte {
    /// <summary>
    /// Casova znacka kdy byl bajt detekovan (v sekundach).
    /// </summary>
    public double Timestamp { get; set; }

    /// <summary>
    /// Nazev kanalu ze ktereho byl bajt nacten.
    /// </summary>
    public string? Channel { get; set; }

    /// <summary>
    /// Hodnota bajtu nactena z datove linky.
    /// </summary>
    public byte Value { get; set; }

    /// <summary>
    /// Popis chyby spojene s timto bajtem (pokud existuje), jinak null.
    /// </summary>
    public string? Error { get; set; }
}
