namespace OscilloscopeCLI.Protocols {
    /// <summary>
    /// Reprezentuje mapovani SPI signalu na nazvy kanalu (napr. CH0, CH1, ...).
    /// </summary>
    public class SpiChannelMapping {
        public string ChipSelect { get; set; } = "CH0";  // CS - chip select
        public string Clock { get; set; } = "CH1";       // SCLK - clock
        public string Mosi { get; set; } = "CH2";        // MOSI - master out, slave in
        public string? Miso { get; set; } = "CH3";       // MISO - master in, slave out (volitelny)
    }
}