using System;
using System.Collections.Generic;

namespace OscilloscopeCLI.Signal {
    /// <summary>
    /// Trida pro analyzu digitalnich signalu - detekce hran, mereni pulzu, identifikace protokolu.
    /// </summary>
    public class DigitalSignalAnalyzer {
        /// <summary>
        /// Struktura reprezentujici vzorek signalu v case.
        /// </summary>
        public struct SignalSample {
            public double Timestamp; // Cas vzorku v sekundach
            public bool State;       // Digitalni hodnota (0 nebo 1)

            public SignalSample(double timestamp, bool state) {
                Timestamp = timestamp;
                State = state;
            }
        }

        /// <summary>
        /// Seznam vzorku signalu.
        /// </summary>
        private List<SignalSample> _samples;

        /// <summary>
        /// Konstruktor tridy.
        /// </summary>
        public DigitalSignalAnalyzer(Dictionary<string, List<Tuple<double, double>>> signalData, string channelName) {
                    if (!signalData.ContainsKey(channelName))
                        throw new ArgumentException($"Kanal {channelName} nebyl nalezen.");

                    _samples = signalData[channelName]
                        .Select(t => new SignalSample(t.Item1, t.Item2 > 0)) // Prevod na bool (0 = LOW, jinak HIGH)
                        .ToList();
                }



        /// <summary>
        /// Detekuje hrany signalu (stoupajici a klesajici).
        /// </summary>
        public List<double> DetectEdges() {
            List<double> edges = new List<double>();

            for (int i = 1; i < _samples.Count; i++) {
                if (_samples[i].State != _samples[i - 1].State) {
                    edges.Add(_samples[i].Timestamp);
                }
            }

            return edges;
        }

        /// <summary>
        /// Meri sirku pulzu (HIGH a LOW).
        /// </summary>
        public List<(double Start, double End, double Width, bool State)> MeasurePulses() {
            List<(double, double, double, bool)> pulses = new List<(double, double, double, bool)>();
            double startTime = _samples[0].Timestamp;
            bool currentState = _samples[0].State;

            for (int i = 1; i < _samples.Count; i++) {
                if (_samples[i].State != currentState) {
                    double endTime = _samples[i].Timestamp;
                    pulses.Add((startTime, endTime, endTime - startTime, currentState));

                    startTime = endTime;
                    currentState = _samples[i].State;
                }
            }

            return pulses;
        }

        /// <summary>
        /// Detekuje anomalie v sirkach pulzu (napr. prilis dlouhe nebo kratke pulzy).
        /// </summary>
        public List<(double Start, double End, double Width)> DetectAnomalies(double minWidth, double maxWidth) {
            List<(double, double, double)> anomalies = new List<(double, double, double)>();

            foreach (var pulse in MeasurePulses()) {
                if (pulse.Width < minWidth || pulse.Width > maxWidth) {
                    anomalies.Add((pulse.Start, pulse.End, pulse.Width));
                }
            }

            return anomalies;
        }

        /// <summary>
        /// Analyzuje casovani hran signalu a odhaduje rychlost komunikace.
        /// </summary>
        /// <returns>Min, max, prumerna mezera mezi hranami a odhadovana baud rate.</returns>
        public (double MinInterval, double MaxInterval, double AvgInterval, double EstimatedBaudRate) AnalyzeTiming() {
            var edges = DetectEdges();
            Console.WriteLine($"[DEBUG] Pocet detekovanych hran: {edges.Count}");

            if (edges.Count < 2) {
                Console.WriteLine("[DEBUG] Nedostatek hran pro vypocet casovani.");
                return (0, 0, 0, 0);
            }

            List<double> intervals = new List<double>();
            for (int i = 1; i < edges.Count; i++) {
                double interval = edges[i] - edges[i - 1];
                intervals.Add(interval);
                Console.WriteLine($"[DEBUG] Interval {i}: {interval} s");
            }

            double min = intervals.Min();
            double max = intervals.Max();
            double avg = intervals.Average();
            double baudRate = avg > 0 ? 1.0 / avg : 0;

            Console.WriteLine($"[DEBUG] Min interval: {min} s");
            Console.WriteLine($"[DEBUG] Max interval: {max} s");
            Console.WriteLine($"[DEBUG] Avg interval: {avg} s");
            Console.WriteLine($"[DEBUG] Odhad baud rate: {baudRate} baud");

            return (min, max, avg, baudRate);
        }

        /// <summary>
        /// Vypise souhrn casovani signalu do konzole.
        /// </summary>
        public void PrintTimingSummary() {
            var (min, max, avg, baud) = AnalyzeTiming();
            Console.WriteLine("Casovani signalu:");
            Console.WriteLine($" - Min interval mezi hranami: {min * 1_000_000:F3} µs"); // mikrosekundy
            Console.WriteLine($" - Max interval mezi hranami: {max * 1_000_000:F3} µs");
            Console.WriteLine($" - Prumerna mezera: {avg * 1_000_000:F3} µs");
            Console.WriteLine($" - Odhadovana baud rate: {baud:F0} baud"); // pocet bitu za 1s
        }
    }
}
