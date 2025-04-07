using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ScottPlot;
using OscilloscopeCLI.Signal;
using System.Windows.Controls;
using OscilloscopeCLI.ProtocolSettings;
using OscilloscopeCLI.Protocols;
using OscilloscopeGUI.Plotting;
using OscilloscopeGUI.Services;
using OscilloscopeGUI.Services.Protocols;

namespace OscilloscopeGUI {
    public partial class MainWindow : Window {
        private SignalLoader loader = new SignalLoader(); // Ulozeni dat do tridy
        private SignalPlotter plotter;
        private UartAnalysisService uartService = new UartAnalysisService();
        private SignalFileService fileService = new SignalFileService();
        private PlotNavigationService navService;

        public MainWindow() {
            InitializeComponent();
            this.KeyDown += MainWindow_KeyDown; // Pripojeni obsluhy klavesnice
            plotter = new SignalPlotter(plot); // Inicializace tridy pro vykreslovani
            navService = new PlotNavigationService(plot);
        }

        /// <summary>
        /// Handler pro kliknuti na tlacitko "Nacist CSV"
        /// </summary>
        private async void LoadCsv_Click(object sender, RoutedEventArgs e) {
            try {
                bool success = await fileService.LoadFromCsvAsync(loader);

                if (!success) {
                    MessageBox.Show("Soubor neobsahuje platna data.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await PlotSignalGraphAsync();
            }
            catch (Exception ex) {
                MessageBox.Show(ex.Message, "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Asynchronne vykresli vsechny signaly pomoci tridy SignalPlotter
        /// </summary>
        private async Task PlotSignalGraphAsync() {
            await plotter.PlotSignalsAsync(loader.SignalData); // SignalPlotter
        }

        /// <summary>
        /// Obsluha klavesovych vstupu pro ovladani zoomu
        /// </summary>
        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            navService.HandleKey(e.Key);
        }


        /// <summary>
        /// Handler pro kliknuti na tlacitko "Analyzovat"
        /// </summary>
        private void AnalyzeButton_Click(object sender, RoutedEventArgs e) {
            if (loader.SignalData.Count == 0) {
                MessageBox.Show("Neni nacten zadny signal.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string selectedProtocol = (ProtocolComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            bool isManual = ManualRadio.IsChecked == true;

            string result;

            switch (selectedProtocol) {
                case "UART":
                    if (isManual) {
                        var settings = new UartSettings {
                            BaudRate = 9600,
                            DataBits = 8,
                            ParityEnabled = false,
                            ParityEven = true,
                            StopBits = 1,
                            IdleHigh = false
                        };
                        result = uartService.AnalyzeWithSettings(loader.SignalData, settings);
                    } else {
                        result = uartService.AnalyzeAuto(loader.SignalData);
                    }

                    MessageBox.Show(result, "Vystup UART", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;

                case "SPI":
                case "I2C":
                    MessageBox.Show($"{selectedProtocol} zatim neni implementovano.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;

                default:
                    MessageBox.Show("Neni vybran platny protokol.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
        }
    }
}