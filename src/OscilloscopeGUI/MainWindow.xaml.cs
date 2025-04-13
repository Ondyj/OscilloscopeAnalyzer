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
            var cts = new CancellationTokenSource(); // token pro zruseni
            var progressDialog = new ProgressDialog(); // dialog s prubehem nacitani
            progressDialog.Show();

            // progres se reportuje do dialogu
            var progress = new Progress<int>(value => {
                progressDialog.ReportProgress(value);
            });

            // akce pri kliknuti na tlacitko "Zrusit"
            progressDialog.OnCanceled = () => {
                cts.Cancel(); // vyvola zruseni v dalsim behu
            };

            try {
                // pokus o nacteni CSV dat asynchronne
                bool success = await fileService.LoadFromCsvAsync(loader, progress, cts.Token);

                // uzivatel zavrel dialog bez vyberu souboru
                if (!success && loader.SignalData.Count == 0 && !cts.IsCancellationRequested) {
                    progressDialog.Finish("Nebyly vybrany zadne soubory.", autoClose: true);
                    return;
                }

                // pokud byl token zrusen (napr. uzivatel klikl na "Zrusit")
                if (cts.IsCancellationRequested) {
                    progressDialog.Finish("Nacitani bylo zruseno uzivatelem.", autoClose: true);
                    return;
                }

                
                // pokud nacitani probehlo uspesne
                if (success) {
                    await PlotSignalGraphAsync();
                    progressDialog.Finish(); // zobrazi tlacitko OK
                    progressDialog.OnOkClicked = () => {};
                } else {
                    // nacitani se nezdarilo (napr. prazdny soubor, spatny format, atd.)
                    progressDialog.Finish("Nacitani se nezdarilo.", autoClose: false);
                    progressDialog.OnOkClicked = () => { };
                }
            }
            // pokud doslo ke zruseni tokenem
            catch (OperationCanceledException) {
                progressDialog.Finish("Nacitani bylo zruseno.", autoClose: false);
            }
            // pokud se stala jina necekana chyba
            catch (Exception ex) {
                if (progressDialog == null) {
                    progressDialog = new ProgressDialog();
                    progressDialog.Show();
                }
                progressDialog.Finish($"Chyba pri nacitani: {ex.Message}", autoClose: false);
                progressDialog.OnOkClicked = () => { };
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
                        // Otevreni dialogu pro rucni zadani nastaveni UART
                        var dialog = new UartSettingsDialog();
                        bool? confirmed = dialog.ShowDialog();

                        if (confirmed == true) {
                            var settings = dialog.Settings;
                            result = uartService.AnalyzeWithSettings(loader.SignalData, settings);
                            MessageBox.Show(result, "Vystup UART", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    } else {
                        result = uartService.AnalyzeAuto(loader.SignalData);
                        MessageBox.Show(result, "Vystup UART", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
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