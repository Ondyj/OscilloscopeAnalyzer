using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols {
    /// <summary>
    /// Pomocne metody pro odhad nastaveni UART protokolu.
    /// </summary>
    public static class UartInferenceHelper {
        /// <summary>
        /// Odhadne zakladni nastaveni UART protokolu ze vzorku signalu.
        /// </summary>
        public static UartSettings InferUartSettings(List<SignalSample> samples) {
            var transitions = DetectTransitions(samples);


            if (transitions.Count < 5)
                throw new InvalidOperationException("Nedostatek přechodů pro odhad přenosové rychlosti.");

            // Odhad delky bitu (z prvnich nekolika prechodu)
            var bitDurations = new List<double>();
            for (int i = 1; i < Math.Min(transitions.Count, 10); i++) {
                double delta = transitions[i].Timestamp - transitions[i - 1].Timestamp;
                if (delta > 0) {
                    bitDurations.Add(delta);
                }
            }

            if (bitDurations.Count == 0)
                throw new InvalidOperationException("Nelze určit průměrnou délku bitu.");

            double averageBitTime = EstimateBitTimeFiltered(transitions);
            int baudRate = (int)Math.Round(1.0 / averageBitTime);

            // Odhad idle urovne (HIGH pokud je vetsina vzorku log. 1)
            int highCount = samples.Count(s => s.State);
            bool idleHigh = highCount > (samples.Count / 2);

            // Odhad bitu
            int dataBits = InferDataBits(samples, averageBitTime, idleHigh);

            // Odhad parity
            Parity parity = InferParity(samples, averageBitTime, dataBits, idleHigh);
            //Console.WriteLine($"[UART][Inference] Odhadnuta parita: {parity}");

            // Odhad stopbitu
            int stopBits = InferStopBits(samples, averageBitTime, dataBits, parity, idleHigh);
            //Console.WriteLine($"[UART][Inference] Odhadnuty pocet stop bitu: {stopBits}");

            return new UartSettings {
                BaudRate = baudRate,
                DataBits = dataBits, // defaultni hodnota
                Parity = parity,
                StopBits = stopBits,
                IdleLevelHigh = idleHigh
            };
        }

        /// <summary>
        /// Odhadne prumernou delku bitu na zaklade kratkych casovych intervalu mezi prechody.
        /// Filtruje pouze hodnoty v realistickem rozsahu.
        /// </summary>
        private static double EstimateBitTimeFiltered(List<SignalSample> transitions) {
            var bitDurations = new List<double>();

            for (int i = 1; i < transitions.Count; i++) {
                double delta = transitions[i].Timestamp - transitions[i - 1].Timestamp;

                // Odfiltrujeme nesmyslne dlouhe prechody
                if (delta > 6e-6 && delta < 11e-6) // akceptuj jen 7–11 µs
                    bitDurations.Add(delta);
            }

            if (bitDurations.Count == 0)
                throw new InvalidOperationException("Nelze odhadnout délku bitu – žádné krátké přechody.");

            double average = bitDurations.Average();

            return average;
        }

        /// <summary>
        /// Odhadne pocet datovych bitu podle validace stop bitu pro ruzne mozne delky.
        /// </summary>
        private static int InferDataBits(List<SignalSample> samples, double bitTime, bool idleLevelHigh) {
            Dictionary<int, int> validCounts = new();

            for (int candidateBits = 5; candidateBits <= 9; candidateBits++)
                validCounts[candidateBits] = 0;

            for (int i = 1; i < samples.Count; i++) {
                var prev = samples[i - 1];
                var curr = samples[i];

                if (prev.State == idleLevelHigh && curr.State != idleLevelHigh) {
                    double startTime = curr.Timestamp;

                    for (int candidateBits = 5; candidateBits <= 9; candidateBits++) {
                        double stopBitTime = startTime + ((candidateBits + 1.5) * bitTime);
                        bool stopBitLevel = GetBitAtTime(samples, stopBitTime);

                        if (stopBitLevel == idleLevelHigh) {
                            validCounts[candidateBits]++;
                        }
                    }
                }
            }

            int bestBits = validCounts.OrderByDescending(kvp => kvp.Value).First().Key;
            return bestBits;
        }

        /// <summary>
        /// Ziska logicky stav signalu v danem case (binary search).
        /// </summary>
        private static bool GetBitAtTime(List<SignalSample> samples, double timestamp) {
            int low = 0, high = samples.Count - 1;
            while (low <= high) {
                int mid = (low + high) / 2;
                if (samples[mid].Timestamp < timestamp) low = mid + 1;
                else high = mid - 1;
            }
            return samples[Math.Max(0, low - 1)].State;
        }

        /// <summary>
        /// Odhadne paritu (None, Even, Odd) podle porovnani skutecneho a ocekavaneho paritniho bitu.
        /// </summary>
        private static Parity InferParity(List<SignalSample> samples, double bitTime, int dataBits, bool idleLevelHigh) {
            int evenOk = 0, oddOk = 0, noneOk = 0;
            int checkedFrames = 0;

            for (int i = 1; i < samples.Count; i++) {
                var prev = samples[i - 1];
                var curr = samples[i];

                if (prev.State == idleLevelHigh && curr.State != idleLevelHigh) {
                    double startTime = curr.Timestamp;
                    byte value = 0;

                    // nacti datove bity
                    for (int bit = 0; bit < dataBits; bit++) {
                        double t = startTime + ((bit + 1.5) * bitTime);
                        if (GetBitAtTime(samples, t))
                            value |= (byte)(1 << bit);
                    }

                    // ziskat paritni bit
                    double parityTime = startTime + ((dataBits + 1.5) * bitTime);
                    bool parityBit = GetBitAtTime(samples, parityTime);

                    // vypocet parity
                    int ones = 0;
                    for (int b = 0; b < 8; b++) if ((value & (1 << b)) != 0) ones++;
                    bool expectedEven = (ones % 2) == 0;

                    if (parityBit == expectedEven) evenOk++;
                    if (parityBit != expectedEven) oddOk++;

                    // taky zkusit NONE (ignorujeme paritni bit a zkusime validaci stop bitu)
                    double stopTimeIfNone = startTime + ((dataBits + 1.5) * bitTime); // stop bit ihned po datech
                    bool stopBitNone = GetBitAtTime(samples, stopTimeIfNone);
                    if (stopBitNone == idleLevelHigh) noneOk++;

                    checkedFrames++;
                    if (checkedFrames >= 100) break;
                }
            }

            if (evenOk >= oddOk && evenOk >= noneOk) return Parity.Even;
            if (oddOk >= evenOk && oddOk >= noneOk) return Parity.Odd;
            return Parity.None;
        }

        /// <summary>
        /// Odhadne pocet stop bitu podle delky logicke urovne po paritnim/datum bitu.
        /// </summary>
        private static int InferStopBits(List<SignalSample> samples, double bitTime, int dataBits, Parity parity, bool idleLevelHigh) {
            List<int> stopBitCounts = new();

            for (int i = 1; i < samples.Count; i++) {
                var prev = samples[i - 1];
                var curr = samples[i];

                if (prev.State == idleLevelHigh && curr.State != idleLevelHigh) {
                    double startTime = curr.Timestamp;

                    int parityOffset = parity == Parity.None ? 0 : 1;
                    double stopStart = startTime + ((dataBits + parityOffset + 1.5) * bitTime);

                    int count = 0;
                    while (true) {
                        double t = stopStart + (count * bitTime);
                        if (t >= samples[^1].Timestamp) break;

                        bool bit = GetBitAtTime(samples, t);
                        if (bit != idleLevelHigh)
                            break;

                        count++;
                    }

                    if (count > 0 && count <= 3)
                        stopBitCounts.Add(count);

                    if (stopBitCounts.Count >= 100) break;
                }
            }

            if (stopBitCounts.Count == 0) {
                return 1;
            }

            int mostCommon = stopBitCounts
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .First().Key;

            return mostCommon;
        }

        /// <summary>
        /// Detekuje prechody v logickem signalu.
        /// </summary>
        private static List<SignalSample> DetectTransitions(List<SignalSample> samples) {
            var transitions = new List<SignalSample>();
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].State != samples[i - 1].State)
                {
                    transitions.Add(samples[i]);
                }
            }
            return transitions;
        }
    }
}