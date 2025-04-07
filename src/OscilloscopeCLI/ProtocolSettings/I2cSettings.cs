namespace OscilloscopeCLI.ProtocolSettings {
    public class I2cSettings : IProtocolSettings {
        public string ProtocolName => "I2C";

        public double ClockRate { get; set; } // Rychlost sbernice v Hz (napr. 100000)
        public int? DeviceAddress { get; set; } = null; // Adresa zarizeni pro filtrovani (nepovinne)
        public bool AnalyzeACK { get; set; } = true; // true = analyzovat ACK bity
        public bool StrictStartStop { get; set; } = true; // true = vyzadovat korektni START/STOP sekvence
    }
}
