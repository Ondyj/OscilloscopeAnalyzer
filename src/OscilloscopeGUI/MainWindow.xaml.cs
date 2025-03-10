using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
                        MessageBox.Show("Soubor neobsahuje platna data.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Vymazani predchoziho grafu
                    plot.Plot.Clear();

                    int channelIndex = 0;
                    double verticalOffset = 2.0; // Posun mezi signaly pro prehlednost

                    // Pridani signalu do grafu
                    foreach (var channel in loader.SignalData) {
                        string channelName = channel.Key;
                        double[] times = channel.Value.Select(v => v.Item1).ToArray();
                        double[] voltages = channel.Value.Select(v => v.Item2).ToArray();

                        // Posun signalu na ose Y, aby nebyly prekryte
                        double[] adjustedVoltages = voltages.Select(v => v + channelIndex * verticalOffset).ToArray();

                        // Pokud je signal binarni, pouzij Add.SignalXY() (step-like vykresleni)
                        if (voltages.Distinct().Count() <= 3) { 
                            plot.Plot.Add.SignalXY(times, adjustedVoltages);
                        }
                        else {
                            plot.Plot.Add.Scatter(times, adjustedVoltages);
                        }

                        channelIndex++;
                    }

                    // Nastaveni vzhledu grafu
                    plot.Plot.Title("Osciloskopicky signal");
                    plot.Plot.Axes.AutoScale();
                    plot.Plot.ShowLegend();

                    // Obnoveni vykresleni grafu
                    plot.Refresh();
                }
                catch (Exception ex) {
                    MessageBox.Show($"Chyba pri nacitani souboru: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
