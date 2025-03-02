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
        private void LoadCsv_Click(object sender, RoutedEventArgs e) {
            // Otevreni dialogu pro vyber CSV souboru
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true) {
                try {
                    // Nacteni dat ze souboru pomoci SignalLoader
                    SignalLoader loader = new SignalLoader();
                    loader.LoadCsvFile(openFileDialog.FileName);

                    // Priprava dat pro vykresleni
                    List<double> time = loader.SignalData.Select(t => t.Item1).ToList();
                    List<double> voltage = loader.SignalData.Select(v => v.Item2).ToList();

                    if (time.Count == 0 || voltage.Count == 0) {
                        MessageBox.Show("Soubor neobsahuje zadna platna data.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Vykresleni signalu v grafu
                    plot.Plot.Clear();
                    plot.Plot.Add.Scatter(time.ToArray(), voltage.ToArray());
                    plot.Refresh();
                }
                catch (Exception ex) {
                    MessageBox.Show($"Chyba pri nacitani souboru: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
