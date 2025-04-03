using System;
using OscilloscopeCLI.Signal;
using System.Collections.Generic;
using System.Linq;
using OscilloscopeCLI.Protocols;

class Program {
    // POUZE TESTOVACI TRIDA NA ODLADENI FUKNCI!!
    static void Main() {
        string filePathCSV = "testData/DSLogic U2Pro16-la-250307-130617_spi.csv"; // nacitany soubor rs115200_scr
        // string filePathCSV = "testData/DSLogic U2Pro16-la-250307-130617_spi.csv";
        string exportPath = "uart_output.csv";

        try {
            // Nacteni signalu
            SignalLoader loader = new SignalLoader();
            loader.LoadCsvFile(filePathCSV);

            if (loader.SignalData.Count == 0) {
                Console.WriteLine("Zadna signalni data nebyla nactena.");
                return;
            }

            // Vyber prvni kanal
            var firstChannel = loader.SignalData.First();
            string channelName = firstChannel.Key;
            Console.WriteLine($"Analyzujeme kanal: {channelName}");

            // Inicializace analyzatoru
            DigitalSignalAnalyzer analyzer = new DigitalSignalAnalyzer(loader.SignalData, channelName);

            // Vypocet baud rate
            var (_, _, avgInterval, baudRate) = analyzer.AnalyzeTiming();
            if (baudRate <= 0) {
                Console.WriteLine("Nepodarilo se spocitat baud rate.");
                return;
            }

            Console.WriteLine($"Odhad baud rate: {baudRate:F0} baud");

            // Inicializace UART analyzeru
            IProtocolAnalyzer uartAnalyzer = new UartProtocolAnalyzer();
            uartAnalyzer.Analyze(analyzer.GetSamples(), baudRate);

            // Export vysledku
            uartAnalyzer.ExportResults(exportPath);
            Console.WriteLine($"Vysledky byly ulozeny do: {exportPath}");

            analyzer.PrintTimingSummary();

            bool isUART = ProtocolDetector.DetectUARTProtocol(analyzer.GetSamples(), baudRate);
            Console.WriteLine(isUART ? "Detekovan UART protokol." : "UART pravdepodobne nebyl detekovan.");
        }
        catch (Exception ex) {
            Console.WriteLine($"CHYBA: {ex.Message}");
        }
    }
}
