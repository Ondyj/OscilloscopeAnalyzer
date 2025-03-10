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
    }
}
