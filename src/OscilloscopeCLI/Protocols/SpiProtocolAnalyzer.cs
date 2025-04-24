using System;
using System.Collections.Generic;
using System.Globalization;
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
        public string? Error { get; set; } 
    }

    public class SpiProtocolAnalyzer : IProtocolAnalyzer {
        private readonly Dictionary<string, List<Tuple<double, double>>> signalData;
        private SpiSettings settings;

        public List<SpiDecodedByte> DecodedBytes { get; private set; } = new();

        public string ProtocolName => "SPI";
        private bool hasMiso = false;

        public SpiProtocolAnalyzer(Dictionary<string, List<Tuple<double, double>>> signalData, SpiSettings settings) {
            this.signalData = signalData;
            this.settings = settings;
        }

        public void Analyze(List<SignalSample> samples, IProtocolSettings settings) {
            throw new NotImplementedException("SPI analyza vyzaduje SCLK, CS, MOSI, MISO.");
        }


        /// <summary>
        /// Provede SPI analyzu nad digitalnimi signaly SCLK, CS, MOSI a volitelne MISO.
        /// Detekuje aktivni prenosy, sbira bity na hranach hodin a dekoduje bajty.
        /// Vysledky jsou ulozeny v DecodedBytes a exportovany do CSV.
        /// </summary>
        public void Analyze() {
            DecodedBytes.Clear();

            var csAnalyzer = new DigitalSignalAnalyzer(signalData, "CH0");
            var sclkAnalyzer = new DigitalSignalAnalyzer(signalData, "CH1");
            var mosiAnalyzer = new DigitalSignalAnalyzer(signalData, "CH2");

            DigitalSignalAnalyzer? misoAnalyzer = null;
            hasMiso = signalData.ContainsKey("CH3") && signalData["CH3"].Count > 0;
            if (hasMiso) {
                misoAnalyzer = new DigitalSignalAnalyzer(signalData, "CH3");
            }

            var activeTransfers = csAnalyzer.GetConstantLevelSegments()
                .Where(seg => seg.Value == 0)
                .ToList();

            var sclkEdges = sclkAnalyzer.DetectTransitions()
                .Where(t => settings.Cpha ? t.From == 0 && t.To == 1 : t.From == 0 && t.To == 1)
                .ToList();

            foreach (var transfer in activeTransfers) {
                var bitsMosi = new List<bool>();
                var bitsMiso = new List<bool>();

                foreach (var edge in sclkEdges) {
                    if (edge.Time < transfer.StartTime || edge.Time > transfer.EndTime)
                        continue;

                    bool bitMosi = GetBitAtTime(mosiAnalyzer.GetSamples(), edge.Time);
                    bitsMosi.Add(bitMosi);

                    if (hasMiso && misoAnalyzer != null) {
                        bool bitMiso = GetBitAtTime(misoAnalyzer.GetSamples(), edge.Time);
                        bitsMiso.Add(bitMiso);
                    }

                    if (bitsMosi.Count == settings.BitsPerWord) {
                        byte valueMosi = PackBits(bitsMosi);
                        byte valueMiso = hasMiso && bitsMiso.Count == settings.BitsPerWord
                            ? PackBits(bitsMiso)
                            : (byte)0x00;

                        DecodedBytes.Add(new SpiDecodedByte {
                            Timestamp = edge.Time,
                            ValueMOSI = valueMosi,
                            ValueMISO = valueMiso,
                            Error = null
                        });

                        bitsMosi.Clear();
                        bitsMiso.Clear();
                    }
                }

                // Kontrola nekompletniho bajtu po skonceni prenosu
                if (bitsMosi.Count > 0 && bitsMosi.Count < settings.BitsPerWord) {
                    DecodedBytes.Add(new SpiDecodedByte {
                        Timestamp = transfer.EndTime,
                        ValueMOSI = PackBits(bitsMosi),
                        ValueMISO = 0x00,
                        Error = "Nedostatecny pocet bitu"
                    });

                    bitsMosi.Clear();
                    bitsMiso.Clear();
                }
            }

            string outputDir = "Vysledky";
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, "spi.csv");

            ExportResults(outputPath);
        }

        /// <summary>
        /// Vrati hodnotu bitu v danem case. Vyhleda nejblizsi vzorek podle casove znacky.
        /// </summary>
        /// <param name="samples">Seznam vzorku</param>
        /// <param name="time">Cas, ve kterem chceme znat stav signalu</param>
        /// <returns>Stav signalu v danem case (true = log. 1, false = log. 0)</returns>
        private bool GetBitAtTime(List<SignalSample> samples, double time) {
            for (int i = 1; i < samples.Count; i++) {
                if (samples[i].Timestamp >= time)
                    return samples[i - 1].State;
            }
            return samples[^1].State;
        }

        /// <summary>
        /// Prevede seznam bitu na bajt (LSB first).
        /// </summary>
        /// <param name="bits">Seznam logickych hodnot (bitu)</param>
        /// <returns>Vysledny bajt slozeny z bitu</returns>
        private byte PackBits(List<bool> bits) {
            byte result = 0;
            for (int i = 0; i < bits.Count; i++) {
                if (bits[i])
                    result |= (byte)(1 << i); // LSB first
            }
            return result;
        }

        /// <summary>
        /// Exportuje vysledky SPI analyzy do CSV souboru. Vystup obsahuje timestamp, MOSI, volitelne MISO, ASCII reprezentaci a chybu.
        /// </summary>
        /// <param name="outputPath">Cesta k vystupnimu CSV souboru</param>
        public void ExportResults(string outputPath) {
            using var writer = new StreamWriter(outputPath);

            if (hasMiso)
                writer.WriteLine("Timestamp [s];MOSI (hex);MISO (hex);ASCII;Error");
            else
                writer.WriteLine("Timestamp [s];MOSI (hex);ASCII;Error");

            foreach (var b in DecodedBytes) {
                string timestamp = b.Timestamp.ToString("F6", CultureInfo.InvariantCulture);
                string mosiHex = $"0x{b.ValueMOSI:X2}";
                string asciiChar = (b.ValueMOSI >= 32 && b.ValueMOSI <= 126)
                    ? ((char)b.ValueMOSI).ToString()
                    : $"\\x{b.ValueMOSI:X2}";
                string error = b.Error ?? "";

                if (hasMiso && b.ValueMISO > 0) {
                    string misoHex = $"0x{b.ValueMISO:X2}";
                    writer.WriteLine($"{timestamp};{mosiHex};{misoHex};{asciiChar};{error}");
                } else {
                    writer.WriteLine($"{timestamp};{mosiHex};{asciiChar};{error}");
                }
            }

            Console.WriteLine($"Výsledky SPI exportovány do souboru: {outputPath}");
        }
    }
}