namespace OscilloscopeCLI.Protocols {
    /// <summary>
    /// Reprezentuje mapovani SPI signalu na nazvy kanalu (napr. CH0, CH1, ...).
    /// </summary>
    public class SpiChannelMapping {
        public string ChipSelect { get; set; } = "";  // CS - chip select
        public string Clock { get; set; } = "";       // SCLK - clock
        public string Mosi { get; set; } = "";        // MOSI - master out, slave in
        public string Miso { get; set; } = "";    // MISO - 
    }
}