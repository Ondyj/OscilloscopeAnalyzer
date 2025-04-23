using OscilloscopeCLI.Signal;
using OscilloscopeCLI.ProtocolSettings;

namespace OscilloscopeCLI.Protocols {
    public interface IProtocolAnalyzer {
        string ProtocolName { get; }

        /// <summary>
        /// Spusti analyzu signalu s nastavenim protokolu
        /// </summary>
        public void Analyze(List<SignalSample> samples, IProtocolSettings settings);

        /// <summary>
        /// Exportuje vysledky analyzy do souboru
        /// </summary>
        void ExportResults(string outputPath);
    }
}
