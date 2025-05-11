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
                        if (delta > 0)
                            bitDurations.Add(delta);
                    }

                    if (bitDurations.Count == 0)
                        throw new InvalidOperationException("Nelze určit průměrnou délku bitu.");

                    double averageBitTime = bitDurations.Average();
                    int baudRate = (int)Math.Round(1.0 / averageBitTime);

                    // Odhad idle urovne (HIGH pokud je vetsina vzorku log. 1)
                    int highCount = samples.Count(s => s.State);
                    bool idleHigh = highCount > (samples.Count / 2);

                    return new UartSettings {
                        BaudRate = baudRate,
                        DataBits = 8, // defaultni hodnota
                        Parity = Parity.None,
                        StopBits = 1,
                        IdleLevelHigh = idleHigh
                    };
                }

        /// <summary>
        /// Detekuje prechody v logickem signalu.
        /// </summary>
        private static List<SignalSample> DetectTransitions(List<SignalSample> samples) {
            var transitions = new List<SignalSample>();
            for (int i = 1; i < samples.Count; i++) {
                if (samples[i].State != samples[i - 1].State)
                    transitions.Add(samples[i]);
            }
            return transitions;
        }
    }
}