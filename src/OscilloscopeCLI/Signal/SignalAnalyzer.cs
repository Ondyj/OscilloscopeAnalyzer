using System;
using System.Collections.Generic;
using System.Linq;

namespace OscilloscopeCLI.Signal {
    public enum SignalType { Analog, Digital }

    public class SignalAnalyzer {
        public List<Tuple<double, double>> SignalData { get; private set; }

        public SignalAnalyzer(List<Tuple<double, double>> signalData) {
            if (signalData == null || signalData.Count == 0)
                throw new ArgumentException("SignalData nemuze byt prazdny.");

            SignalData = signalData;
        }

        /// <summary>
        /// Detekuje, zda je signal digitalni nebo analogovy.
        /// Digitalni signal obsahuje pouze dve unikatni hodnoty (např. 0 a 1).
        /// </summary>
        public SignalType DetectSignalType() {
            var uniqueValues = new HashSet<double>(SignalData.Select(t => t.Item2));
            return uniqueValues.Count <= 2 ? SignalType.Digital : SignalType.Analog;
        }

        /// <summary>
        /// Vrati minimalni a maximalni hodnotu signalu.
        /// </summary>
        public (double Min, double Max) GetMinMaxValues() {
            double min = SignalData.Min(t => t.Item2);
            double max = SignalData.Max(t => t.Item2);
            return (min, max);
        }

        /// <summary>
        /// Detekuje pulzy v signalu, ktere jsou siroke nad stanovenou mez.
        /// </summary>
        public List<Tuple<double, double>> DetectPulses(double threshold) {
            List<Tuple<double, double>> pulses = new();
            bool inPulse = false;
            double pulseStart = 0;

            for (int i = 1; i < SignalData.Count; i++) {
                double time = SignalData[i].Item1;
                double prevValue = SignalData[i - 1].Item2;
                double value = SignalData[i].Item2;

                if (!inPulse && value > threshold) {
                    pulseStart = time;
                    inPulse = true;
                }
                else if (inPulse && value <= threshold) {
                    pulses.Add(new Tuple<double, double>(pulseStart, time));
                    inPulse = false;
                }
            }

            return pulses;
        }

        /// <summary>
        /// Vypocita prumernou sirku pulzu.
        /// </summary>
        /// <param name="pulses">Seznam pulzu, kde kazdy prvek obsahuje dvojici hodnot: zacatek a konec pulzu.</param>
        /// <returns>Prumerna sirka pulzu, nebo 0 pokud seznam neobsahuje zadne pulzy.</returns>
        public double CalculateAveragePulseWidth(List<Tuple<double, double>> pulses) {
            if (pulses.Count == 0) return 0;
            
            double totalWidth = pulses.Sum(p => p.Item2 - p.Item1);
            return totalWidth / pulses.Count;
        }

    }
}
