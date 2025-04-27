using System;
using System.Collections.Generic;
using System.Globalization;
using OscilloscopeCLI.Protocols;
using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols {

    public class UartSettings : IProtocolSettings {
        public string ProtocolName => "UART";
        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public Parity Parity { get; set; } // None, Even, Odd
        public int StopBits { get; set; }
        public bool IdleLevelHigh { get; set; } // true = idle 1, false = idle 0
    }

    public enum Parity {
        None,
        Even,
        Odd
    }
    public class UartDecodedByte {
        public double Timestamp { get; set; }
        public string? Channel { get; set; }
        public byte Value { get; set; }
        public string? Error { get; set; }
    }
    public class UartProtocolAnalyzer : IProtocolAnalyzer {
        private Dictionary<string, List<SignalSample>> channelSamples;
        private readonly UartSettings settings;

        public List<UartDecodedByte> DecodedBytes { get; private set; } = new();

        public string ProtocolName => "UART";

        public UartProtocolAnalyzer(Dictionary<string, List<Tuple<double, double>>> signalData, UartSettings settings) {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.channelSamples = new Dictionary<string, List<SignalSample>>();

            foreach (var kvp in signalData) {
                var samples = new List<SignalSample>();
                foreach (var (timestamp, value) in kvp.Value) {
                    samples.Add(new SignalSample(timestamp, value != 0));
                }
                channelSamples[kvp.Key] = samples;
            }
        }

        /// <summary>
        /// Analyzuje vsechny dostupne kanaly
        /// </summary>
        public void Analyze() {
            double bitTime = CalculateBitTime();
            Console.WriteLine($"[DEBUG] bitTime = {bitTime * 1_000_000:F4} mikrosekund");

            DecodedBytes.Clear();
            if (channelSamples.Count == 0)
                return;

            bool idleLevel = settings.IdleLevelHigh;
            Console.WriteLine($"[DEBUG] IdleLevelHigh = {(idleLevel ? "1" : "0")}");

            foreach (var kvp in channelSamples) {
                string channelName = kvp.Key;
                var samples = kvp.Value;

                Console.WriteLine($"[DEBUG] Analyzuji kanal: {channelName}");

                int i = 1;
                while (i < samples.Count) {
                    var previous = samples[i - 1];
                    var current = samples[i];

                    bool fromIdle = previous.State == idleLevel;
                    bool toActive = current.State != idleLevel;

                    if (fromIdle && toActive) {
                        double startTime = current.Timestamp;
                        Console.WriteLine($"[DEBUG] {channelName} - Start bit v case {startTime:F9} sekund.");

                        byte value = 0;

                        // Cteme datove bity
                        for (int bitIndex = 0; bitIndex < settings.DataBits; bitIndex++) {
                            double sampleTime = startTime + ((bitIndex + 1.5) * bitTime);
                            bool bit = GetBitAtTime(samples, sampleTime);

                            if (bit)
                                value |= (byte)(1 << bitIndex);

                            Console.WriteLine($"[DEBUG] {channelName} - bitIndex: {bitIndex}, sampleTime: {sampleTime:F9}, bit: {(bit ? 1 : 0)}");
                        }

                        string? error = null;

                        // Parita
                        if (settings.Parity != Parity.None) {
                            double parityTime = startTime + ((settings.DataBits + 1.5) * bitTime);
                            bool parityBit = GetBitAtTime(samples, parityTime);

                            bool calculatedParity = CalculateParity(value);

                            if (settings.Parity == Parity.Even && calculatedParity != parityBit)
                                error = "Chyba paritniho bitu (ocekavana suda parita)";
                            if (settings.Parity == Parity.Odd && calculatedParity == parityBit)
                                error = "Chyba paritniho bitu (ocekavana licha parita)";

                            Console.WriteLine($"[DEBUG] {channelName} - parityBit: {(parityBit ? 1 : 0)}, calculatedParity: {(calculatedParity ? 1 : 0)}, error: {error}");
                        }

                        // Stop bit
                        double stopBitTime = startTime + ((settings.DataBits + (settings.Parity != Parity.None ? 1 : 0) + settings.StopBits) * bitTime);
                        bool stopBitOk = GetBitAtTime(samples, stopBitTime) == idleLevel;

                        if (!stopBitOk) {
                            error = (error != null ? error + " + " : "") + "Chyba stop bitu";
                            Console.WriteLine($"[DEBUG] {channelName} - Chyba stop bitu v case {stopBitTime:F9}");
                        }

                        // Ulozime vysledek
                        DecodedBytes.Add(new UartDecodedByte {
                            Timestamp = startTime,
                            Channel = channelName,
                            Value = value,
                            Error = error
                        });

                        Console.WriteLine($"[DEBUG] {channelName} - Dekodovany byte: 0x{value:X2} na case {startTime:F9} {(error != null ? "[CHYBA]" : "[OK]")}");

                        // Posun za konec prenosu
                        while (i < samples.Count && samples[i].Timestamp < stopBitTime)
                            i++;

                        continue;
                    }

                    i++;
                }
            }
        }

        /// <summary>
        /// Spocita paritu (even parity) pro dany byte.
        /// Vraci true, pokud je pocet jednicek lichy, jinak false (sudy).
        /// </summary>
        private bool CalculateParity(byte value) {
            int ones = 0;
            for (int i = 0; i < 8; i++)
            {
                if ((value & (1 << i)) != 0)
                    ones++;
            }
            return (ones % 2) != 0; // true = lichy pocet
        }

        /// <summary>
        /// Najde hodnotu signalu v danem case - vraci stav pred nejblizsim vzorkem se stejnym nebo vetsim casem.
        /// Pokud cas presahuje vsechny vzorky, vraci stav posledniho vzorku.
        /// </summary>
        private bool GetBitAtTime(List<SignalSample> samples, double timestamp) {
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].Timestamp >= timestamp)
                    return samples[i - 1].State;
            }
            return samples[^1].State;
        }

        /// <summary>
        /// Vypocita dobu trvani jednoho bitu v sekundach podle baudrate.
        /// </summary>
        private double CalculateBitTime() {
            if (settings == null)
                throw new InvalidOperationException("Settings nejsou inicializovany.");

            if (settings.BaudRate <= 0)
                throw new ArgumentException("BaudRate musi byt vetsi nez 0.");

            return 1.0 / settings.BaudRate;
        }

        /// <summary>
        /// Spocita pocet nastavenych bitu (1) v bytu.
        /// </summary>
        private int CountBitsSet(byte b) {
            int count = 0;
            while (b != 0) {
                count += b & 1;
                b >>= 1;
            }
            return count;
        }

        public void ExportResults(string outputPath) {
            using var writer = new StreamWriter(outputPath);

            // hlavicka CSV
            writer.WriteLine("Timestamp [s];Channel;Byte (hex);ASCII;Error");

            foreach (var b in DecodedBytes)
            {
                string timestamp = b.Timestamp.ToString("F9", CultureInfo.InvariantCulture);
                string channel = b.Channel ?? "Unknown"; // kdyz tam nemame, tak defaultne Unknown
                string hex = $"0x{b.Value:X2}";
                string asciiChar = (b.Value >= 32 && b.Value <= 126) ? ((char)b.Value).ToString() : $"\\x{b.Value:X2}";
                string error = b.Error ?? "";

                writer.WriteLine($"{timestamp};{channel};{hex};{asciiChar};{error}");
            }

            Console.WriteLine($"[DEBUG] Vysledky UART byly exportovany do: {outputPath}");
        }
    }
}
