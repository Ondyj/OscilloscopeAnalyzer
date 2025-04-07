using System;
using System.Collections.Generic;
using System.Linq;
using OscilloscopeCLI.ProtocolSettings;
using OscilloscopeCLI.Protocols;
using OscilloscopeCLI.Signal;

namespace OscilloscopeGUI.Services.Protocols {
    /// <summary>
    /// Trida zodpovedna za analyzu UART signalu - jak automatickou, tak rucni
    /// </summary>
    public class UartAnalysisService {

        /// <summary>
        /// Provede automatickou detekci UART signalu a jeho analyzu
        /// </summary>
        /// <param name="signalData">Vstupni digitalni data z analyzatoru</param>
        /// <returns>Textovy vystup nebo chybova hlaska</returns>
        public string AnalyzeAuto(Dictionary<string, List<Tuple<double, double>>> signalData) {
            var channel = signalData.First();
            var analyzer = new DigitalSignalAnalyzer(signalData, channel.Key);
            var samples = analyzer.GetSamples();

            var (_, _, avgInterval, baudRate) = analyzer.AnalyzeTiming();
            bool isUart = ProtocolDetector.DetectUARTProtocol(samples, baudRate);

            if (!isUart)
                return "Signal neodpovida UART komunikaci.";

            var uart = new UartProtocolAnalyzer();
            uart.Analyze(samples, baudRate);

            return "TODO: dodelat export vysledku";
        }

        /// <summary>
        /// Provede analyzu UART signalu s rucne zadanym nastavenim
        /// </summary>
        /// <param name="signalData">Vstupni digitalni data</param>
        /// <param name="settings">Nastaveni protokolu UART</param>
        /// <returns>Textovy vystup analyzy</returns>
        public string AnalyzeWithSettings(Dictionary<string, List<Tuple<double, double>>> signalData, UartSettings settings) {
            var channel = signalData.First();
            var analyzer = new DigitalSignalAnalyzer(signalData, channel.Key);
            var samples = analyzer.GetSamples();

            var uart = new UartProtocolAnalyzer();
            uart.Analyze(samples, settings.BaudRate); // Pozdeji rozsirit o dalsi nastaveni

            return "TODO: dodelat export vysledku";
        }
    }
}