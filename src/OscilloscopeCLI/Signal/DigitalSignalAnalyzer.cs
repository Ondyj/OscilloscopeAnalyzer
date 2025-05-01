using System;
using System.Collections.Generic;

namespace OscilloscopeCLI.Signal {

    /// <summary>
    /// Jeden vzorek digitalniho signalu s casovou znackou a stavem (0 nebo 1).
    /// </summary>
    public class SignalSample {
        public double Timestamp { get; set; }
        public bool State { get; set; }

        public SignalSample(double timestamp, bool state) {
            Timestamp = timestamp;
            State = state;
        }
    }

    /// <summary>
    /// Reprezentuje prechod v digitalnim signalu (zmena stavu z 0 na 1 nebo naopak).
    /// </summary>
    public class DigitalTransition {
        public double Time { get; set; }
        public int From { get; set; }
        public int To { get; set; }
    }

    /// <summary>
    /// Segment signalu s konstantni hodnotou v danem casovem rozsahu.
    /// </summary>
    public class DigitalLevelSegment {
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public int Value { get; set; }

        public double Duration => EndTime - StartTime; // Delka trvani segmentu v sekundach.
    }

    /// <summary>
    /// Trida pro analyzu digitalniho signalu – detekce prechodu, konstantnich segmentu a casovani.
    /// </summary>
    public class DigitalSignalAnalyzer {
        private readonly List<(double Timestamp, bool State)> samples;

         /// <summary>
        /// Vytvori analyzator pro dany kanal z poskytnutych signalovych dat.
        /// </summary>
        /// <param name="signalData">Slovnik vsech signalu podle nazvu kanalu.</param>
        /// <param name="channelKey">Nazev kanalu, ktery ma byt analyzovan.</param>
        public DigitalSignalAnalyzer(Dictionary<string, List<(double Time, double Value)>> signalData, string channelKey) {
            samples = new List<(double, bool)>();
            if (signalData.TryGetValue(channelKey, out var rawSamples)) {
                foreach (var (timestamp, value) in rawSamples) {
                    samples.Add((timestamp, value != 0));
                }
            }
        }

        /// <summary>
        /// Detekuje vsechny prechody (hrany) v digitalnim signalu.
        /// </summary>
        /// <returns>Seznam prechodu s casem a zmenou stavu.</returns>
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
        /// Vrati seznam segmentu, kde mel signal konstantni hodnotu.
        /// </summary>
        /// <returns>Seznam segmentu se zacatkem, koncem a hodnotou (0 nebo 1).</returns>
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
        /// Vrati vsechny vzorky signalu pro dalsi zpracovani nebo vizualizaci.
        /// </summary>
        /// <returns>Seznam vzorku (cas, stav).</returns>
        public List<(double Timestamp, bool State)> GetSamples() {
            return samples;
        }
    }
}
