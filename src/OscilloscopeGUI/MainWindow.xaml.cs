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
                    //DisplayDataInTable();

                    // Vykresleni signalu na pozadi
                    await PlotSignalGraphAsync();
                }
                catch (Exception ex) {
                    MessageBox.Show($"Chyba pri nacitani souboru: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Zobrazi nactena data v tabulce (DataGrid)
        /// </summary>
        /*private void DisplayDataInTable() {
            var tableData = loader.SignalData
                .SelectMany(kv => kv.Value.Select(v => new { Cas = v.Item1, Kanal = kv.Key, Napeti = v.Item2 }))
                .ToList();

            dataGrid.ItemsSource = tableData;
        }*/

        /// <summary>
        /// Vykresli signaly do grafu s adaptivnim vertikalnim posunutim
        /// </summary>
        private async Task PlotSignalGraphAsync() {
            await Task.Run(() => {
                Dispatcher.Invoke(() => {
                    plot.Plot.Clear();

                    double offset = 0; // Pocatecni posun na ose Y
                    double spacing = 0.001; // Velikost mezery mezi kanaly

                    // Pro kazdy kanal zjistime jeho rozsah
                    foreach (var channel in loader.SignalData) {
                        string channelName = channel.Key;
                        double[] times = channel.Value.Select(v => v.Item1).ToArray();
                        double[] voltages = channel.Value.Select(v => v.Item2).ToArray();

                        // Zjisteni minima a maxima aktualniho kanalu
                        double minValue = voltages.Min();
                        double maxValue = voltages.Max();

                        // Posun signalu nahoru, aby se neprekryval s predchozim
                        double[] adjustedVoltages = voltages.Select(v => v + offset - minValue).ToArray();

                        // Vykresleni signalu
                        var signal = plot.Plot.Add.Signal(adjustedVoltages);
                        signal.LegendText = channelName;

                        // Pridani horni a dolni ohranicujici cary
                        double lowerBound = offset - spacing;  // Dolni hranice kanalu
                        double upperBound = offset + (maxValue - minValue) + spacing;  // Horni hranice kanalu
                        
                        var lowerLine = plot.Plot.Add.HorizontalLine(lowerBound);
                        lowerLine.Color = new ScottPlot.Color(128, 128, 128, 128); // Sediva barva

                        var upperLine = plot.Plot.Add.HorizontalLine(upperBound);
                        upperLine.Color = new ScottPlot.Color(128, 128, 128, 128);

                        // Posuneme osu Y pro dalsi kanal (max hodnota + mezera)
                        offset += (maxValue - minValue) + 2 * spacing;
                    }

                    plot.Plot.Axes.AutoScale();
                    plot.Plot.ShowLegend();
                    plot.Refresh();
                });
            });
        }

        private void DisplayMinMaxValues() {
            if (loader.SignalData.Count == 0) {
                MessageBox.Show("Není načten žádný signál.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string result = "Min/Max hodnoty kanálu:\n";

            foreach (var channel in loader.SignalData) {
                var analyzer = new AnalyzeSignal(channel.Value.Select(v => v.Item2).ToList());
                var (min, max) = analyzer.GetMinMaxValues();
                result += $"{channel.Key}: Min = {min} V, Max = {max} V\n";
            }

            MessageBox.Show(result, "Min/Max hodnoty", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DisplayEdges() {
            if (loader.SignalData.Count == 0) {
                MessageBox.Show("Není načten žádný signál.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double threshold = 0.5; // Nastavena prahova hodnota pro detekci hran
            string result = "Detekované hrany:\n";

            foreach (var channel in loader.SignalData) {
                var analyzer = new AnalyzeSignal(channel.Value.Select(v => v.Item2).ToList());
                var edges = analyzer.DetectEdges(threshold);

                result += $"{channel.Key}: {edges.Count} hran nalezeno\n";
            }

            MessageBox.Show(result, "Detekovane hrany", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EdgeDetectionButton_Click(object sender, RoutedEventArgs e) {
            DisplayEdges();
        }

        private void MinMaxButton_Click(object sender, RoutedEventArgs e) {
            DisplayMinMaxValues();
        }
    }
}
