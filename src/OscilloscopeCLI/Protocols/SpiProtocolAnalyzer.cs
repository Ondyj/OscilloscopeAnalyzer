using System;
using System.Collections.Generic;
using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols {
    public class SpiSettings : IProtocolSettings {
        public string ProtocolName => "SPI";
        public bool Cpol { get; set; } = false;
        public bool Cpha { get; set; } = false;
        public int BitsPerWord { get; set; } = 8;
    }

    public class SpiDecodedByte {
        public double Timestamp { get; set; }
        public byte ValueMOSI { get; set; }
        public byte ValueMISO { get; set; }
    }

    public class SpiProtocolAnalyzer : IProtocolAnalyzer {
        private readonly List<SignalSample> sclk;
        private readonly List<SignalSample> cs;
        private readonly List<SignalSample> mosi;
        private readonly List<SignalSample> miso;
        private SpiSettings settings;

        public List<SpiDecodedByte> DecodedBytes { get; private set; } = new();

        public string ProtocolName => "SPI";

        public SpiProtocolAnalyzer(List<SignalSample> sclk, List<SignalSample> cs,
                                   List<SignalSample> mosi, List<SignalSample> miso,
                                   SpiSettings settings) {
            this.sclk = sclk;
            this.cs = cs;
            this.mosi = mosi;
            this.miso = miso;
            this.settings = settings;
        }

        public void Analyze(List<SignalSample> samples, IProtocolSettings settings) {
            // TODO
            throw new NotImplementedException("SPI analyza vyzaduje SCLK, CS, MOSI, MISO.");
        }

        public void Analyze() {
            // TODO:

        }

        public void ExportResults(string outputPath) {
            // TODO: Export CSV s Timestamp, MOSI, MISO
        }
    }
}