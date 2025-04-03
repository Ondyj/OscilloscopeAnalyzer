using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols {
    public interface IProtocolAnalyzer {
        string ProtocolName { get; }
        void Analyze(List<DigitalSignalAnalyzer.SignalSample> samples, double baudRate);
        void ExportResults(string outputPath);
    }
}