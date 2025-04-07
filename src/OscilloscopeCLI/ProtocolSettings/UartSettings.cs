using OscilloscopeCLI.ProtocolSettings;

public class UartSettings : IProtocolSettings {
    public string ProtocolName => "UART";

    public double BaudRate { get; set; } // Prenosova rychlost v bitech za sekundu
    public int DataBits { get; set; } = 8; // Pocet datovych bitu (napr. 8)
    public bool ParityEnabled { get; set; } = false; // Zda je pouzita parita
    public bool ParityEven { get; set; } = true; // true = suda parita, false = licha
    public int StopBits { get; set; } = 1; // Pocet stop bitu (obvykle 1 nebo 2)
    public bool IdleHigh { get; set; } = false; // true = klidova uroven je HIGH, false = LOW
}
