namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Reprezentuje jeden dekodovany bajt prenoseny po protokolu UART.
/// Obsahuje cas, kanal, hodnotu bajtu a pripadnou chybu.
/// </summary>
public class UartDecodedByte {
    public double Timestamp { get; set; } // Cas zacatku bajtu (start bit)
    public double StartTime { get; set; } // = Timestamp
    public double EndTime { get; set; }   // Cas konce bajtu
    public string? Channel { get; set; } // Nazev kanalu ze ktereho byl bajt nacten
    public byte Value { get; set; } // Hodnota bajtu nactena z linky
    public string? Error { get; set; } // Popis chyby spojene s bajtem (nebo null)
}
