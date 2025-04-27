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

        /// <summary>
        /// Provede SPI analyzu nad digitalnimi signaly SCLK, CS, MOSI a volitelne MISO.
        /// Detekuje aktivni prenosy, sbira bity na hranach hodin a dekoduje bajty.
        /// Vysledky jsou ulozeny v DecodedBytes a exportovany do CSV.
        /// </summary>
        public void Analyze() {
            //AutoDetectSpiMode(); //TODO

            // DEBUG: vypis detekovaneho SPI modu
            //Console.WriteLine($"[DEBUG] SPI mod: CPOL={(settings.Cpol ? 1 : 0)}, CPHA={(settings.Cpha ? 1 : 0)}, BitsPerWord={settings.BitsPerWord}");

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

            // na ktere hrane hodin se maji sbirat data
            var sclkEdges = sclkAnalyzer.DetectTransitions()
                .Where(t => {
                    if (!settings.Cpha) {
                        // CPHA = 0 → data se nacitaji na prvni hrane
                        // pokud je CPOL = 1 → prvni hrana je sestupna (1 -> 0)
                        // pokud je CPOL = 0 → prvni hrana je nabezna (0 -> 1)
                        return settings.Cpol ? t.From == 1 && t.To == 0 : t.From == 0 && t.To == 1;
                    } else {
                        // CPHA = 1 → data se nacitaji na druhe hrane
                        // pokud je CPOL = 1 → druha hrana je nabezna (0 -> 1)
                        // pokud je CPOL = 0 → druha hrana je sestupna (1 -> 0)
                        return settings.Cpol ? t.From == 0 && t.To == 1 : t.From == 1 && t.To == 0;
                    }
                }).ToList();

                foreach (var transfer in activeTransfers) {
                    var bitsMosi = new List<bool>();
                    var bitsMiso = new List<bool>();

                    var edgesInTransfer = sclkEdges.Where(e => e.Time >= transfer.StartTime && e.Time <= transfer.EndTime).ToList();

                    foreach (var edge in edgesInTransfer) {
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

                    CheckShortTransfer(transfer.StartTime, transfer.EndTime, sclkEdges);
                    CheckEdgeBitMismatch(transfer.EndTime, edgesInTransfer.Count, bitsMosi.Count);
                    CheckInactiveLine(transfer.EndTime, bitsMosi, "MOSI");
                    if (hasMiso) CheckInactiveLine(transfer.EndTime, bitsMiso, "MISO");
                    CheckIncompleteByte(transfer.EndTime, bitsMosi, bitsMiso);
                }
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
        /// Zkontroluje, zda prenos obsahuje alespon jednu hranu hodin.
        /// Pokud ne, jedna se o prilis kratky prenos a je oznacen jako chybny.
        /// </summary>
        /// <param name="startTime">Cas zacatku prenosu (CS LOW)</param>
        /// <param name="endTime">Cas konce prenosu (CS HIGH)</param>
        /// <param name="sclkEdges">Seznam hran hodinoveho signalu SCLK</param>
        private void CheckShortTransfer(double startTime, double endTime, List<DigitalTransition> sclkEdges) {
            int clockEdgesInTransfer = sclkEdges.Count(e => e.Time >= startTime && e.Time <= endTime);
            if (clockEdgesInTransfer < 1) {
                DecodedBytes.Add(new SpiDecodedByte {
                    Timestamp = endTime,
                    ValueMOSI = 0,
                    ValueMISO = 0,
                    Error = "Prilis kratky prenos (CS LOW < 1 hrana)"
                });
            }
        }

        /// <summary>
        /// Porovna pocet nactenych hran a ocekavanych bitu v jednom prenosu.
        /// Pokud se hodnoty neshoduji, zaznamena chybu.
        /// </summary>
        /// <param name="endTime">Cas konce prenosu</param>
        /// <param name="edgeCount">Pocet detekovanych hran v prenosu</param>
        /// <param name="bitCount">Pocet skutecne nactenych bitu</param>
        private void CheckEdgeBitMismatch(double endTime, int edgeCount, int bitCount) {
            if (edgeCount != bitCount) {
                DecodedBytes.Add(new SpiDecodedByte {
                    Timestamp = endTime,
                    ValueMOSI = 0,
                    ValueMISO = 0,
                    Error = $"Nesoulad poctu hran ({edgeCount}) a bitu ({bitCount})"
                });
            }
        }

        /// <summary>
        /// Zkontroluje, zda behem prenosu byl signal na danem kanale konstantni (0 nebo 1).
        /// To muze signalizovat chybu v zapojeni nebo komunikaci.
        /// </summary>
        /// <param name="endTime">Cas konce prenosu</param>
        /// <param name="bits">Seznam hodnot na signalu behem prenosu</param>
        /// <param name="lineName">Nazev signalu ("MOSI", "MISO")</param>
        private void CheckInactiveLine(double endTime, List<bool> bits, string lineName) {
            if (bits.Distinct().Count() == 1) {
                DecodedBytes.Add(new SpiDecodedByte {
                    Timestamp = endTime,
                    ValueMOSI = 0,
                    ValueMISO = 0,
                    Error = $"{lineName} byl behem prenosu konstantni ({(bits[0] ? "1" : "0")})"
                });
            }
        }

        /// <summary>
        /// Zkontroluje, zda behem prenosu doslo k nacteni nekompletniho bajtu.
        /// Pokud neni dosazen pocet bitu podle nastaveni, zaznamena chybu.
        /// </summary>
        /// <param name="endTime">Cas konce prenosu</param>
        /// <param name="bitsMosi">Seznam bitu z MOSI</param>
        /// <param name="bitsMiso">Seznam bitu z MISO</param>
        private void CheckIncompleteByte(double endTime, List<bool> bitsMosi, List<bool> bitsMiso) {
            if (bitsMosi.Count > 0 && bitsMosi.Count < settings.BitsPerWord) {
                DecodedBytes.Add(new SpiDecodedByte {
                    Timestamp = endTime,
                    ValueMOSI = PackBits(bitsMosi),
                    ValueMISO = hasMiso ? PackBits(bitsMiso) : (byte)0x00,
                    Error = "Nedostatecny pocet bitu"
                });
            }
        }


        /*public void AutoDetectSpiMode() {
            var csAnalyzer = new DigitalSignalAnalyzer(signalData, "CH0");
            var sclkAnalyzer = new DigitalSignalAnalyzer(signalData, "CH1");

            var activeTransfers = csAnalyzer.GetConstantLevelSegments()
                .Where(seg => seg.Value == 0)
                .ToList();

            var sclkEdges = sclkAnalyzer.DetectTransitions();

            if (activeTransfers.Count == 0 || sclkEdges.Count == 0) {
                Console.WriteLine("Neni dostatek dat pro detekci SPI modu.");
                return;
            }

            // Prvni prenosovy ramec
            var transfer = activeTransfers.First();
            var edgesInTransfer = sclkEdges
                .Where(e => e.Time >= transfer.StartTime && e.Time <= transfer.EndTime)
                .ToList();

            if (edgesInTransfer.Count == 0) {
                Console.WriteLine("Nebyly detekovany zadne hranove prechody behem CS aktivniho stavu.");
                return;
            }

            // Urci CPOL podle vychoziho stavu SCLK
            var initialSclkLevel = sclkAnalyzer.GetSamples()
                .FirstOrDefault(s => s.Timestamp >= transfer.StartTime)?.State ?? false;

            // Prvni hrana v ramci aktivniho prenosu
            var firstEdge = edgesInTransfer.First();

            // Pokud hrana je 0->1 a zacinali jsme v 0, tak CPOL = 0 (inicialni stav = 0)
            // Pokud hrana je 1->0 a zacinali jsme v 1, tak CPOL = 1
            if (initialSclkLevel == false && firstEdge.From == 0 && firstEdge.To == 1) {
                settings.Cpol = false;
                settings.Cpha = false;
            } else if (initialSclkLevel == true && firstEdge.From == 1 && firstEdge.To == 0) {
                settings.Cpol = true;
                settings.Cpha = false;
            } else {
                // Alternativni vysvetleni: data se cist az na druhou hranu
                settings.Cpha = true;

                // Predpoklad: CPOL se odvozuje od vychoziho stavu hodin
                settings.Cpol = initialSclkLevel;
            }

            Console.WriteLine($"Detekovany SPI mod: CPOL={(settings.Cpol ? 1 : 0)}, CPHA={(settings.Cpha ? 1 : 0)}");
        }*/

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
                string timestamp = b.Timestamp.ToString("F9", CultureInfo.InvariantCulture);
                string mosiHex = $"0x{b.ValueMOSI:X2}";
                string asciiChar = (b.ValueMOSI >= 32 && b.ValueMOSI <= 126)
                    ? ((char)b.ValueMOSI).ToString()
                    : $"\\x{b.ValueMOSI:X2}";
                string error = b.Error ?? "";

                if (hasMiso) {
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