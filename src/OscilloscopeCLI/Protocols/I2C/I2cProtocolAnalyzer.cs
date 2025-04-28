using System;
using System.Collections.Generic;
using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols {
    public class I2cDecodedPacket {
        public double StartTimestamp { get; set; }   // cas startu prenosu
        public double StopTimestamp { get; set; }    // cas konce prenosu
        public byte Address { get; set; }             // adresa zarizeni
        public bool IsRead { get; set; }              // true = cteni, false = zapis
        public List<byte> Data { get; set; } = new();
        public List<bool> Acknowledges { get; set; } = new();
        public string? Error { get; set; }            // chyba pokud nastane
    }

    public class I2cProtocolAnalyzer {
        private readonly Dictionary<string, List<Tuple<double, double>>> signalData;
        private readonly List<I2cDecodedPacket> decodedPackets = new();

        private List<Tuple<double, bool>> sclTransitions = new();
        private List<Tuple<double, bool>> sdaTransitions = new();

        public IReadOnlyList<I2cDecodedPacket> DecodedPackets => decodedPackets;

        public I2cProtocolAnalyzer(Dictionary<string, List<Tuple<double, double>>> signalData) {
            this.signalData = signalData;
        }

        public void Analyze() {
            // TODO: Implementace hlavni analyzy I2C signalu
        }

    }
}
