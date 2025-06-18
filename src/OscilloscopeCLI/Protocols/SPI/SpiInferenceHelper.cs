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

            var allEdges = DetectTransitions(sclkSamples);
            var cpolValues = new List<bool>();
            var cphaEstimates = new List<bool>();
            var bitsPerTransfer = new List<int>();

            List<(double StartTime, double EndTime)> activeWindows;

            if (!string.IsNullOrEmpty(mapping.ChipSelect) && signalData.ContainsKey(mapping.ChipSelect)) {
                var csAnalyzer = new DigitalSignalAnalyzer(signalData, mapping.ChipSelect);
                var csSegments = csAnalyzer.GetConstantLevelSegments();

                activeWindows = csSegments
                    .Where(s => s.Value == 0)
                    .Select(s => (s.StartTime, s.EndTime))
                    .ToList();
            } else {
                double start = sclkSamples.First().Timestamp;
                double end = sclkSamples.Last().Timestamp;
                activeWindows = new() { (start, end) };
            }

            int edgeIdx = 0;
            foreach (var window in activeWindows) {
                while (edgeIdx < allEdges.Count && allEdges[edgeIdx].Timestamp < window.StartTime)
                    edgeIdx++;

                int idx = edgeIdx;
                var edgesInWindow = new List<SignalSample>();
                while (idx < allEdges.Count && allEdges[idx].Timestamp <= window.EndTime)
                    edgesInWindow.Add(allEdges[idx++]);

                if (edgesInWindow.Count < 4 || (window.EndTime - window.StartTime) > 0.01)
                    continue;

                int clkStartIdx = sclkSamples.BinarySearchTimestamp(window.StartTime);
                bool firstState = sclkSamples[Math.Max(0, clkStartIdx)].State;
                cpolValues.Add(firstState);

                double delay = edgesInWindow.First().Timestamp - window.StartTime;
                double estimatedBitTime = (window.EndTime - window.StartTime) / edgesInWindow.Count;
                double windowDuration = window.EndTime - window.StartTime;
                double position = (edgesInWindow.First().Timestamp - window.StartTime) / windowDuration;
                bool cphaEstimate = position > 0.4; // > 40 % = CPHA = 1, jinak 0
                cphaEstimates.Add(cphaEstimate);

                int bitsInThisWindow = EstimateBitsPerTransfer(edgesInWindow);
                bitsPerTransfer.Add(bitsInThisWindow);

            }

            // Fallback pro pripad, ze zadne validni okno nebylo nalezeno
            if (bitsPerTransfer.Count == 0)
            {
                var fallbackStart = sclkSamples.First().Timestamp;
                var fallbackEnd = sclkSamples.Last().Timestamp;
                bool fallbackCpol = sclkSamples.First().State;
                int fallbackBits = allEdges.Count;

                return new SpiSettings {
                    Cpol = fallbackCpol,
                    Cpha = false,
                    BitsPerWord = fallbackBits > 0 && fallbackBits < 64 ? fallbackBits : 8
                };
            }

            bool finalCpol = cpolValues.Count(v => v) > cpolValues.Count / 2.0;
            bool finalCpha = cphaEstimates.Count(v => v) > cphaEstimates.Count / 2.0;
            int bitsPerWordEstimate = (int)Math.Round(bitsPerTransfer.Average());

            return new SpiSettings {
                Cpol = finalCpol,
                Cpha = finalCpha,
                BitsPerWord = bitsPerWordEstimate
            };
        }

        /// <summary>
        /// Odhadne pocet bitu na prenos ze seznamu hran hodinoveho signalu
        /// </summary>
        private static int EstimateBitsPerTransfer(List<SignalSample> edges) {
            if (edges.Count < 8)
                return 8; // vychozi hodnota, pokud malo hran

            var intervals = new List<double>();
            for (int i = 1; i < edges.Count; i++) {
                double delta = edges[i].Timestamp - edges[i - 1].Timestamp;
                if (delta > 0)
                    intervals.Add(delta);
            }

            if (intervals.Count == 0)
                return 8;

            // Detekuj nejcastejsi (modalne) cas mezi hranami
            double modeInterval = intervals
                .GroupBy(x => Math.Round(x, 6))
                .OrderByDescending(g => g.Count())
                .First().Key;

            // Urci prumerne trvani prenosoveho okna
            double avgWindowDuration = edges.Last().Timestamp - edges.First().Timestamp;
            int bits = (int)Math.Round(avgWindowDuration / modeInterval);

            if (bits >= 1 && bits <= 64)
                return bits;

            return 8; // fallback
        }

        /// <summary>
        /// Vyhleda index vzorku s nejblizsi casovou znackou mensi nebo rovnou zadanemu timestampu
        /// </summary>
        public static int BinarySearchTimestamp(this List<SignalSample> samples, double timestamp) {
            int low = 0, high = samples.Count - 1;
            while (low <= high) {
                int mid = (low + high) / 2;
                if (samples[mid].Timestamp < timestamp)
                    low = mid + 1;
                else
                    high = mid - 1;
            }
            return Math.Max(0, low - 1);
        }

        /// <summary>
        /// Detekuje prechody v digitalnim signalu (zmena stavu 0 ↔ 1)
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
