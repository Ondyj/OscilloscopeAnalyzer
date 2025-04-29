using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols {
    /// <summary>
    /// Pomocne metody pro odhad nastaveni SPI protokolu.
    /// </summary>
    public static class SpiInferenceHelper {
        /// <summary>
        /// Odhadne nastaveni SPI protokolu ze signalovych dat.
        /// </summary>
        public static SpiSettings InferSettings(Dictionary<string, List<Tuple<double, double>>> signalData) {
            if (!signalData.ContainsKey("CH0") || !signalData.ContainsKey("CH1"))
                throw new InvalidOperationException("CH0 (CS) nebo CH1 (SCLK) chybí.");

            var csSamples = signalData["CH0"]
                .Select(t => new SignalSample(t.Item1, t.Item2 > 0.5)).ToList();
            var sclkSamples = signalData["CH1"]
                .Select(t => new SignalSample(t.Item1, t.Item2 > 0.5)).ToList();

            if (sclkSamples.Count < 10)
                throw new InvalidOperationException("Nedostatek dat pro odhad SPI nastavení.");

            var tempData = new Dictionary<string, List<Tuple<double, double>>> {
                { "CH0", csSamples.Select(s => Tuple.Create(s.Timestamp, s.State ? 1.0 : 0.0)).ToList() }
            };

            var csAnalyzer = new DigitalSignalAnalyzer(tempData, "CH0");
            var csSegments = csAnalyzer.GetConstantLevelSegments();
            var csActive = csSegments.FirstOrDefault(s => s.Value == 0);

            if (csActive == null)
                throw new InvalidOperationException("Nebyly nalezeny aktivní CS segmenty.");

            var transitions = DetectTransitions(sclkSamples);
            var edgesInActive = transitions
                .Where(t => t.Timestamp >= csActive.StartTime && t.Timestamp <= csActive.EndTime)
                .ToList();

            int bitsPerWordEstimate = edgesInActive.Count switch {
                <= 4 => 4,
                <= 8 => 8,
                <= 16 => 8,
                <= 24 => 8,
                <= 32 => 16,
                _ => 8
            };

            return new SpiSettings {
                Cpol = false, // napevno
                Cpha = false, // napevno
                BitsPerWord = bitsPerWordEstimate // odhad pocetu bitu na prenos na zaklade poctu prechodu
            };
        }

        /// <summary>
        /// Detekuje prechody v signalu.
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
