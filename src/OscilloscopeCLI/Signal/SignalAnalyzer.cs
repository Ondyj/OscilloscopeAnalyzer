using System;
using System.Collections.Generic;
using System.Linq;

namespace OscilloscopeCLI.Signal {
    public enum SignalType { Analog, Digital } // Enum pro rozliseni typu signalu.

    /// <summary>
    /// Trida pro analyzu jednoho analogoveho nebo digitalniho signalu.
    /// </summary>
    public class SignalAnalyzer {
        public List<Tuple<double, double>> SignalData { get; private set; } // Vstupni signalova data (cas, hodnota).

        /// <summary>
        /// Vytvori novou instanci analyzatoru signalu.
        /// </summary>
        /// <param name="signalData">Seznam dvojic (cas, hodnota) predstavujicich signal.</param>
        public SignalAnalyzer(List<Tuple<double, double>> signalData) {
            if (signalData == null || signalData.Count == 0)
                throw new ArgumentException("SignalData nemůže být prázdny.");

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
        /// Detekuje pulzy v signalu, ktere presahuji danou prahovou hodnotu.
        /// </summary>
        /// <param name="threshold">Prahovy limit pro detekci pulzu.</param>
        /// <returns>Seznam pulzu ve forme dvojic (zacatek, konec).</returns>
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
        /// <param name="pulses">Seznam pulzu ve forme dvojic (zacatek, konec).</param>
        /// <returns>Prumerna sirka pulzu nebo 0, pokud zadny pulz nebyl nalezen.</returns>
        public double CalculateAveragePulseWidth(List<Tuple<double, double>> pulses) {
            if (pulses.Count == 0) return 0;
            
            double totalWidth = pulses.Sum(p => p.Item2 - p.Item1);
            return totalWidth / pulses.Count;
        }

    }
}
