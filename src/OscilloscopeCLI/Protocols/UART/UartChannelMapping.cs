namespace OscilloscopeCLI.Protocols {
    /// <summary>
    /// Reprezentuje mapovani UART signalu na nazvy kanalu.
    /// </summary>
    public class UartChannelMapping {
        public string Tx { get; set; } = ""; // Vystupni signal (transmit)
        public string Rx { get; set; } = ""; // Vstupni signal (receive)

        public bool IsValid() {
            // Vraci true, pokud jsou obe role nastaveny a nejsou shodne
            return !string.IsNullOrEmpty(Tx) &&
                   !string.IsNullOrEmpty(Rx) &&
                   Tx != Rx;
        }
    }
}