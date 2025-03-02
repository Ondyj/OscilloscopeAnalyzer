using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ScottPlot;
using OscilloscopeCLI.Data; // Přidání CLI knihovny pro načítání souborů

namespace OscilloscopeGUI {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        private void LoadCsv_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true) {
                try {
                    // Načtení dat ze souboru pomocí SignalLoader
                    SignalLoader loader = new SignalLoader();
                    loader.LoadCsvFile(openFileDialog.FileName);

                    // Připravení dat pro vykreslení
                    List<double> time = loader.SignalData.Select(t => t.Item1).ToList();
                    List<double> voltage = loader.SignalData.Select(v => v.Item2).ToList();

                    if (time.Count == 0 || voltage.Count == 0) {
                        MessageBox.Show("Soubor neobsahuje žádná platná data.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Vykreslení signálu
                    plot.Plot.Clear();
                    plot.Plot.Add.Scatter(time.ToArray(), voltage.ToArray());
                    plot.Refresh();
                }
                catch (Exception ex) {
                    MessageBox.Show($"Chyba při načítání souboru: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
