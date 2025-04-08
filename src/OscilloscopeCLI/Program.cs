using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OscilloscopeCLI.Signal;
using OscilloscopeCLI.Protocols;

class Program {
    // POUZE TESTOVACI TRIDA NA ODLADENI FUNKCI
    static void Main() {
        string filePathCSV = "testData/DSLogic U2Pro16-la-250307-130617_spi.csv";

        try {
            // Nacteni signalu
            SignalLoader loader = new SignalLoader();
            loader.LoadCsvFile(filePathCSV);

            if (loader.SignalData.Count == 0) {
                Console.WriteLine("Zadna signalni data nebyla nactena.");
                return;
            }

            Console.WriteLine($"Celkem kanalu: {loader.SignalData.Count}");

            // Projdi vsechny kanaly
            foreach (var channel in loader.SignalData) {
                string channelName = channel.Key;
                Console.WriteLine($"\n--- Analyza kanalu {channelName} ---");

                // Detekce typu signalu
                var typeDetector = new SignalAnalyzer(channel.Value);
                var signalType = typeDetector.DetectSignalType();

                if (signalType == SignalType.Analog) {
                    Console.WriteLine(" -> Analogovy signal (preskoceno)");
                    continue;
                }

                // Digitalni signal -> spust analyzator
                var analyzer = new DigitalSignalAnalyzer(loader.SignalData, channelName);
                var (_, _, avgInterval, baudRate) = analyzer.AnalyzeTiming();

                if (baudRate <= 0) {
                    Console.WriteLine(" -> Nepodarilo se spocitat baud rate");
                    continue;
                }

                // Detekce UART
                bool isUART = ProtocolDetector.DetectUARTProtocol(analyzer.GetSamples(), baudRate);

                if (isUART) {
                    Console.WriteLine($" -> Detekovan UART ({baudRate:F0} baud)");

                    var uart = new UartProtocolAnalyzer();
                    //uart.Analyze(analyzer.GetSamples(), baudRate);

                    string exportPath = $"uart_output_{channelName}.csv";
                    uart.ExportResults(exportPath);
                    Console.WriteLine($" -> UART vystup ulozen do: {exportPath}");
                } else {
                    Console.WriteLine(" -> UART pravdepodobne nebyl detekovan");
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"CHYBA: {ex.Message}");
        }
    }
}
