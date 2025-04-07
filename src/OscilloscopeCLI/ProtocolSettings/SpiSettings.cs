namespace OscilloscopeCLI.ProtocolSettings {
    public class SpiSettings : IProtocolSettings {
        public string ProtocolName => "SPI";

        public double BitRate { get; set; } // Bitova rychlost v bitech za sekundu
        public bool ClockPolarity { get; set; } = false; // CPOL: false = idle LOW, true = idle HIGH
        public bool ClockPhase { get; set; } = false; // CPHA: false = vzorkuje na prvni hrane, true = na druhe
        public bool MSBFirst { get; set; } = true; // true = nejvyznamnejsi bit jako prvni
        public int BitsPerWord { get; set; } = 8; // Pocet bitu v jednom slove (napr. 8 nebo 16)
    }
}
