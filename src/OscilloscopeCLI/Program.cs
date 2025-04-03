using System;
using OscilloscopeCLI.Signal;
using System.Collections.Generic;
using System.Linq;

class Program {
    // POUZE TESTOVACI TRIDA NA ODLADENI FUKNCI!!
    static void Main() {
        string filePathCSV = "testData/DSLogic U2Pro16-la-250307-130617_spi.csv"; // nacitany soubor rs115200_scr
        // string filePathCSV = "testData/DSLogic U2Pro16-la-250307-130617_spi.csv";

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

            // Detekce hran
            List<double> edges = analyzer.DetectEdges();
            Console.WriteLine($"Detekovane hrany: {edges.Count}");
            foreach (var edge in edges.Take(10)) {
                Console.WriteLine($"Hrana v {edge}s");
            }

            // Mereni pulzu
            var pulses = analyzer.MeasurePulses();
            Console.WriteLine($"Detekovane pulzy: {pulses.Count}");
            foreach (var pulse in pulses.Take(10)) {
                Console.WriteLine($"Pulz {pulse.State} od {pulse.Start}s do {pulse.End}s, sirka: {pulse.Width}s");
            }

            analyzer.PrintTimingSummary();
        }
        catch (Exception ex) {
            Console.WriteLine($"CHYBA: {ex.Message}");
        }
    }
}
