using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ScottPlot;
using OscilloscopeCLI.Data;

namespace OscilloscopeGUI {
    public partial class MainWindow : Window {
        private SignalLoader loader = new SignalLoader(); // Ulozeni dat do tridy
        public MainWindow() {
            InitializeComponent();
        }

        /// <summary>
        /// Handler pro kliknuti na tlacitko "Nacist CSV"
        /// </summary>
        private async void LoadCsv_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true) {
                try {
                    // Nacteni dat ze souboru CSV
                    await Task.Run(() => loader.LoadCsvFile(openFileDialog.FileName));

                    if (loader.SignalData.Count == 0) {
                        MessageBox.Show("Soubor neobsahuje platna data.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Aktualizace GUI
                    DisplayDataInTable();
                    PlotSignalGraph();
                }
                catch (Exception ex) {
                    MessageBox.Show($"Chyba pri nacitani souboru: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Zobrazi nactena data v tabulce (DataGrid)
        /// </summary>
        private void DisplayDataInTable() {
            var tableData = loader.SignalData
                .SelectMany(kv => kv.Value.Select(v => new { Cas = v.Item1, Kanal = kv.Key, Napeti = v.Item2 }))
                .ToList();

            dataGrid.ItemsSource = tableData;
        }

        /// <summary>
        /// Vykresli signaly do grafu
        /// </summary>
        private void PlotSignalGraph() {
            plot.Plot.Clear();

            int channelIndex = 0;
            double verticalOffset = 2.0; // Posun mezi signály pro prehlednost

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
            plot.Plot.Title("Osciloskopický signál");
            plot.Plot.Axes.AutoScale();
            plot.Plot.ShowLegend();
            plot.Refresh();
        }
    }
}
