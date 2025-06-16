namespace OscilloscopeCLI.Protocols {
    /// <summary>
    /// Reprezentuje mapovani UART signalu na nazvy kanalu.
    /// </summary>
    public class UartChannelMapping {
        public string Tx { get; set; } = ""; // Vystupni signal (transmit)
        public string Rx { get; set; } = ""; // Vstupni signal (receive)

        public bool IsValid() {
            if (!string.IsNullOrEmpty(Tx) && !string.IsNullOrEmpty(Rx))
                return Tx != Rx; // obe ruzne OK
            if (!string.IsNullOrEmpty(Tx) || !string.IsNullOrEmpty(Rx))
                return true; // aspon jedna OK
            return false; // zadna prirazena
        }
    }
}