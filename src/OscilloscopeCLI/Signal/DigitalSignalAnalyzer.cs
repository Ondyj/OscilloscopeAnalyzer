using System;
using System.Collections.Generic;

namespace OscilloscopeCLI.Signal {
    public class SignalSample {
        public double Timestamp { get; set; }
        public bool State { get; set; }

        public SignalSample(double timestamp, bool state) {
            Timestamp = timestamp;
            State = state;
        }
    }

    public class DigitalTransition {
        public double Time { get; set; }
        public int From { get; set; }
        public int To { get; set; }
    }

    public class DigitalLevelSegment {
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public int Value { get; set; }

        public double Duration => EndTime - StartTime;
    }

    public class DigitalSignalAnalyzer {
        private readonly List<SignalSample> samples;

        // konstruktor z Dictionary + klic kanalu
        public DigitalSignalAnalyzer(Dictionary<string, List<Tuple<double, double>>> signalData, string channelKey) {
            samples = new List<SignalSample>();
            if (signalData.TryGetValue(channelKey, out var rawSamples)) {
                foreach (var (timestamp, value) in rawSamples) {
                    samples.Add(new SignalSample(timestamp, value != 0));
                }
            }
        }

        /// <summary>
        /// Detekuje vsechny prechody (hrany) v digitalnim signalu,
        /// tj. zmenu stavu z 0 na 1 nebo z 1 na 0.
        /// </summary>
        /// <returns>Seznam detekovanych prechodu s casem a zmenou stavu.</returns>
        public List<DigitalTransition> DetectTransitions() {
            var transitions = new List<DigitalTransition>();
            for (int i = 1; i < samples.Count; i++) {
                int prev = samples[i - 1].State ? 1 : 0;
                int current = samples[i].State ? 1 : 0;
                if (prev != current) {
                    transitions.Add(new DigitalTransition {
                        Time = samples[i].Timestamp,
                        From = prev,
                        To = current
                    });
                }
            }
            return transitions;
        }

        /// <summary>
        /// Vrati segmenty, kde mel signal konstantni hodnotu (0 nebo 1),
        /// vcetne jejich casoveho trvani.
        /// </summary>
        public List<DigitalLevelSegment> GetConstantLevelSegments() {
            var segments = new List<DigitalLevelSegment>();
            if (samples.Count == 0) return segments;

            bool currentValue = samples[0].State;
            double startTime = samples[0].Timestamp;

            for (int i = 1; i < samples.Count; i++) {
                bool val = samples[i].State;
                if (val != currentValue) {
                    segments.Add(new DigitalLevelSegment {
                        StartTime = startTime,
                        EndTime = samples[i].Timestamp,
                        Value = currentValue ? 1 : 0
                    });
                    startTime = samples[i].Timestamp;
                    currentValue = val;
                }
            }

            segments.Add(new DigitalLevelSegment {
                StartTime = startTime,
                EndTime = samples[^1].Timestamp,
                Value = currentValue ? 1 : 0
            });

            return segments;
        }

        /// <summary>
        /// Vrati vsechny vzorky signalu pro dalsi analyzu nebo zobrazeni.
        /// </summary>
        /// <returns>Seznam vzorku obsahujici cas a stav signalu.</returns>
        public List<SignalSample> GetSamples() {
            return samples;
        }

        // TODO jednoducha analzza pro auto-detekci rychlosti (dummy prozatim)
        public (double min, double max, double avg, double estimatedBaudRate) AnalyzeTiming() {
            var times = new List<double>();
            for (int i = 1; i < samples.Count; i++) {
                if (samples[i].State != samples[i - 1].State) {
                    times.Add(samples[i].Timestamp - samples[i - 1].Timestamp);
                }
            }

            if (times.Count == 0) return (0, 0, 0, 0);

            double min = double.MaxValue;
            double max = double.MinValue;
            double sum = 0;

            foreach (var dt in times) {
                if (dt < min) min = dt;
                if (dt > max) max = dt;
                sum += dt;
            }

            double avg = sum / times.Count;
            double baud = avg > 0 ? 1.0 / avg : 0;

            return (min, max, avg, baud);
        }
    }
}
