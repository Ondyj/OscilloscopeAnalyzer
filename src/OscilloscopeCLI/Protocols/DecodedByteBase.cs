namespace OscilloscopeCLI.Protocols {

    /// <summary>
    /// Abstraktni zaklad pro dekodovane bajty (SPI, UART).
    /// </summary>
    public abstract class DecodedByteBase {
        public abstract byte Value { get; }
        public abstract double Timestamp { get; }
        public abstract string? Error { get; }
    }
    }