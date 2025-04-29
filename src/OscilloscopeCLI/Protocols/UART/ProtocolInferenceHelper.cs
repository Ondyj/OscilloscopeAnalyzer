using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols {
    /// <summary>
    /// Pomocne metody pro odhad parametru komunikacnich protokolu.
    /// </summary>
    public static class ProtocolInferenceHelper {
        /// <summary>
        /// Odhadne zakladni nastaveni UART protokolu ze vzorku signalu.
        /// </summary>
        public static UartSettings InferUartSettings(List<SignalSample> samples) {
            var transitions = DetectTransitions(samples);

            if (transitions.Count < 5)
                throw new InvalidOperationException("Nedostatek přechodů pro odhad rychlosti.");

            var bitDurations = new List<double>();
            for (int i = 1; i < Math.Min(transitions.Count, 10); i++) {
                bitDurations.Add(transitions[i].Timestamp - transitions[i - 1].Timestamp);
            }

            double averageBitTime = bitDurations.Average();
            int baudRate = (int)Math.Round(1.0 / averageBitTime);

            int highCount = samples.Count(s => s.State);
            bool idleLevelHigh = highCount > samples.Count / 2;

            return new UartSettings {
                BaudRate = baudRate,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = 1,
                IdleLevelHigh = idleLevelHigh
            };
        }

        /// <summary>
        /// Detekuje prechody v logickem signalu.
        /// </summary>
        private static List<SignalSample> DetectTransitions(List<SignalSample> samples) {
            var transitions = new List<SignalSample>();
            for (int i = 1; i < samples.Count; i++) {
                if (samples[i].State != samples[i - 1].State) {
                    transitions.Add(samples[i]);
                }
            }
            return transitions;
        }
    }
}