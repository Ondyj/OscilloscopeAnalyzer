using System;
using System.Collections.Generic;
using System.Linq;

namespace OscilloscopeCLI.Data
{
    public enum SignalType { Analog, Digital }

    public class AnalyzeSignal
    {
        public List<double> SignalData { get; private set; }

        public AnalyzeSignal(List<double> signalData)
        {
            if (signalData == null || signalData.Count == 0)
                throw new ArgumentException("SignalData cannot be null or empty.");

            SignalData = signalData;
        }

        /// <summary>
        /// Detekuje, zda je signal digitalni nebo analogovy.
        /// Digitalni signal obsahuje pouze dve unikatni hodnoty (např. 0 a 1).
        /// </summary>
        public SignalType DetectSignalType()
        {
            var uniqueValues = new HashSet<double>(SignalData);
            return uniqueValues.Count <= 2 ? SignalType.Digital : SignalType.Analog;
        }

        /// <summary>
        /// Vrati minimalni a maximalni hodnotu signalu.
        /// </summary>
        public (double Min, double Max) GetMinMaxValues() {
            double min = SignalData.Min();
            double max = SignalData.Max();
            return (min, max);
        }

        /// <summary>
        /// Detekuje hrany v signalu na zaklade prahove hodnoty.
        /// </summary>
        /// <param name="threshold">Pravova hodnota pro detekci hrany.</param>
        /// <returns>Seznam indexu, kde doslo k hrane.</returns>
        public List<int> DetectEdges(double threshold) {
            List<int> edgeIndexes = new List<int>();

            for (int i = 1; i < SignalData.Count; i++) {
                if (Math.Abs(SignalData[i] - SignalData[i - 1]) >= threshold) {
                    edgeIndexes.Add(i);
                }
            }

            return edgeIndexes;
        }

    }
}
