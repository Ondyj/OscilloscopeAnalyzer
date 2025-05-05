using System;
using System.IO;
using System.Windows;
using OscilloscopeCLI.Protocols;

namespace OscilloscopeGUI.Services {
    /// <summary>
    /// Sluzba pro nacitani CSV souboru se signalovymi daty s podporou zruseni a indikace pokroku.
    /// </summary>
    public class ExportService {

        /// <summary>
        /// Provede export vysledku z analyzatoru do CSV souboru s unikatnim nazvem na zaklade parametru protokolu.
        /// </summary>
        public void Export(IProtocolAnalyzer? analyzer, string? loadedFilePath) {
            if (analyzer is not IExportableAnalyzer exportable) {
                MessageBox.Show("Aktivní analyzátor nepodporuje export.", "Chyba exportu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (loadedFilePath == null) {
                MessageBox.Show("Není k dispozici cesta k původnímu souboru.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string inputFileName = Path.GetFileNameWithoutExtension(loadedFilePath);
            string outputDir = "Vysledky";
            Directory.CreateDirectory(outputDir);

            string paramInfo = analyzer switch {
                UartProtocolAnalyzer uart => $"UART_{uart.Settings.BaudRate}_{uart.Settings.DataBits}{(uart.Settings.Parity == Parity.None ? 'N' : uart.Settings.Parity.ToString()[0])}{uart.Settings.StopBits}",
                SpiProtocolAnalyzer spi => $"SPI_{spi.Settings.BitsPerWord}b_{(spi.Settings.Cpol ? "CPOL1" : "CPOL0")}_{(spi.Settings.Cpha ? "CPHA1" : "CPHA0")}",
                _ => exportable.ProtocolName
            };

            string outputFileName = $"{inputFileName}_{paramInfo}.csv";
            string outputPath = GetUniqueFilePath(outputDir, outputFileName);

            exportable.ExportResults(outputPath);

            MessageBox.Show($"Výsledky byly exportovány do:\n{outputPath}", "Export dokončen", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Vrati unikatni cestu k vystupnimu souboru. Pokud soubor jiz existuje, prida cislovany suffix.
        /// </summary>
        private string GetUniqueFilePath(string directory, string baseFileName) {
            string baseName = Path.GetFileNameWithoutExtension(baseFileName);
            string extension = Path.GetExtension(baseFileName);
            string path = Path.Combine(directory, baseFileName);
            int counter = 2;

            while (File.Exists(path)) {
                path = Path.Combine(directory, $"{baseName}_{counter}{extension}");
                counter++;
            }

            return path;
        }
    }
}