using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols {
    public static class SpiInferenceHelper {
        /// <summary>
        /// Odhadne nastaveni SPI protokolu ze signalovych dat a zadaného mapování.
        /// </summary>
      public static SpiSettings InferSettings(
            Dictionary<string, List<(double Time, double Value)>> signalData,
            SpiChannelMapping mapping) {

            if (!signalData.ContainsKey(mapping.Clock))
                throw new InvalidOperationException("Zadaný kanál SCLK nebyl nalezen v datech.");

            var sclkSamples = signalData[mapping.Clock]
                .Select(t => new SignalSample(t.Time, t.Value > 0.5)).ToList();

            if (sclkSamples.Count < 10)
                throw new InvalidOperationException("Nedostatek dat pro odhad SPI nastavení.");

            List<(double StartTime, double EndTime)> activeWindows;

            if (!string.IsNullOrEmpty(mapping.ChipSelect) && signalData.ContainsKey(mapping.ChipSelect)) {
                var csAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.ChipSelect);
                var csSegments = csAnalyzer.GetConstantLevelSegments();
                var csActive = csSegments.FirstOrDefault(s => s.Value == 0);

                if (csActive == null)
                    throw new InvalidOperationException("Nebyly nalezeny aktivní CS segmenty.");

                activeWindows = new() { (csActive.StartTime, csActive.EndTime) };
            } else {
                // Bez CS: 
                double start = sclkSamples.First().Timestamp;
                double end = sclkSamples.Last().Timestamp;
                activeWindows = new() { (start, end) };
            }

            var transitions = DetectTransitions(sclkSamples);

            // Najdi prechody v ramci prenosoveho okna
            var edgesInActive = transitions
                .Where(t => t.Timestamp >= activeWindows[0].StartTime && t.Timestamp <= activeWindows[0].EndTime)
                .ToList();

            if (edgesInActive.Count == 0)
                throw new InvalidOperationException("Žádné přechody hodin v rámci aktivního přenosu.");

            int bitsPerWordEstimate = edgesInActive.Count switch {
                <= 4 => 4,
                <= 8 => 8,
                <= 16 => 8,
                <= 24 => 8,
                <= 32 => 16,
                _ => 8
            };

            bool firstState = sclkSamples.First(s => s.Timestamp >= activeWindows[0].StartTime).State;
            bool cpol = firstState;

            double delayAfterStart = edgesInActive.First().Timestamp - activeWindows[0].StartTime;
            bool cpha = delayAfterStart > (1.5 * (activeWindows[0].EndTime - activeWindows[0].StartTime) / bitsPerWordEstimate);

            return new SpiSettings {
                Cpol = cpol,
                Cpha = cpha,
                BitsPerWord = bitsPerWordEstimate
            };
        }


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
