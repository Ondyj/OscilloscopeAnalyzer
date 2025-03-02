using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;  // PRIDANO - pro File.Exists()
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ScottPlot;
using OscilloscopeCLI.Data; 

namespace OscilloscopeGUI {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        // Handler pro kliknuti na tlacitko "Nacist CSV"
        private async void LoadCsv_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true) {
                try {
                    // Nacteni dat ze souboru CSV
                    SignalLoader loader = new SignalLoader();
                    await Task.Run(() => loader.LoadCsvFile(openFileDialog.FileName));

                    if (loader.SignalData.Count == 0) {
                        MessageBox.Show("Soubor neobsahuje zadna platna data.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Automaticke nalezeni odpovidajiciho TXT souboru
                    string csvFilePath = openFileDialog.FileName;
                    string txtFilePath = csvFilePath.Replace(".csv", "_csv.txt");

                    // Nacteni konfiguracniho souboru, pokud existuje
                    OscilloscopeConfig config = new OscilloscopeConfig();
                    if (File.Exists(txtFilePath)) {  // Opraveno - File je ted rozpoznano
                        config.LoadTxtFile(txtFilePath);

                        // Aktualizace informaci o osciloskopu v GUI
                        OscilloscopeInfo.Text = $"Model: {config.Model}, Vzorkovaní: {config.SamplingRate} Sa/s, Časový rozsah: {config.TimeScale}s";
                    } else {
                        OscilloscopeInfo.Text = "Konfiguracni soubor nenalezen.";
                    }

                    // Vymazani predchoziho grafu
                    plot.Plot.Clear();

                    // Vykresleni vsech signalu pomoci Add.Signal()
                    foreach (var channel in loader.SignalData) {
                        List<double> voltage = channel.Value.Select(v => v.Item2).ToList();
                        double sampleRate = 1.0 / loader.TimeIncrement;

                        var signalPlot = plot.Plot.Add.Signal(voltage.ToArray(), sampleRate);
                        signalPlot.LegendText = channel.Key;
                    }

                    plot.Plot.Legend.IsVisible = true;
                    plot.Refresh();
                }
                catch (Exception ex) {
                    MessageBox.Show($"Chyba pri nacitani souboru: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
