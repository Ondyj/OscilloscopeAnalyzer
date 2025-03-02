using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ScottPlot;
using OscilloscopeCLI.Data; // Pridani CLI knihovny pro nacitani souboru

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
                    // Nacteni dat
                    SignalLoader loader = new SignalLoader();
                    await Task.Run(() => loader.LoadCsvFile(openFileDialog.FileName));

                    if (loader.SignalData.Count == 0) {
                        MessageBox.Show("Soubor neobsahuje zadna platna data.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Vymazani predchoziho grafu
                    plot.Plot.Clear();

                    // Vykresleni vsech signalu pomoci Add.Signal()
                    foreach (var channel in loader.SignalData) {
                        List<double> voltage = channel.Value.Select(v => v.Item2).ToList();

                        // Vypocet vzorkovaci frekvence z casoveho kroku
                        double sampleRate = 1.0 / loader.TimeIncrement; 

                        // Pridani signalu do grafu
                        var signalPlot = plot.Plot.Add.Signal(voltage.ToArray(), sampleRate);
                        signalPlot.LegendText = channel.Key; // Nastaveni legendy
                    }

                    plot.Plot.Legend.IsVisible = true; // Zapnuti legendy
                    plot.Refresh();
                }
                catch (Exception ex) {
                    MessageBox.Show($"Chyba pri nacitani souboru: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
