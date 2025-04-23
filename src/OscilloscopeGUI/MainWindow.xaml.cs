using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.WPF;
using OscilloscopeCLI.Signal;
using System.Windows.Controls;
using OscilloscopeCLI.ProtocolSettings;
using OscilloscopeCLI.Protocols;
using OscilloscopeGUI.Plotting;
using OscilloscopeGUI.Services;
using OscilloscopeGUI.Services.Protocols;
using System.Windows.Input;
using System.Runtime.InteropServices;

namespace OscilloscopeGUI {
    public partial class MainWindow : Window {
        private SignalLoader loader = new SignalLoader(); // Ulozeni dat do tridy
        private SignalPlotter plotter;
        private UartAnalysisService uartService = new UartAnalysisService();
        private SignalFileService fileService = new SignalFileService();
        private PlotNavigationService navService;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        public MainWindow() {
            AllocConsole(); // Otevreni konzole pro debug vypisy

            InitializeComponent();

            plot.UserInputProcessor.Disable(); // zakazani cyhovzi chovani

            this.KeyDown += MainWindow_KeyDown; // Pripojeni obsluhy klavesnice

            this.MouseWheel += MainWindow_MouseWheel;

            plotter = new SignalPlotter(plot); // Inicializace tridy pro vykreslovani
            navService = new PlotNavigationService(plot);
        }

        /// <summary>
        /// Handler pro kliknuti na tlacitko "Nacist CSV"
        /// </summary>
        private async void LoadCsv_Click(object sender, RoutedEventArgs e) {
            var cts = new CancellationTokenSource();

            // dialog pro vyber souboru
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            bool fileSelected = openFileDialog.ShowDialog() == true;
            if (!fileSelected) {
                return; // uzivatel zrusil vyber
            }

            // vytvoreni progressbaru
            var progressDialog = new ProgressDialog();
            progressDialog.Show();

            var progress = new Progress<int>(value => {
                progressDialog.ReportProgress(value);
            });

            progressDialog.OnCanceled = () => {
                cts.Cancel();
            };

            try {
                // Nacteme data z vybraneho souboru
                bool success = await Task.Run(() =>
                    {
                        loader.LoadCsvFile(openFileDialog.FileName, progress, cts.Token);
                        return loader.SignalData.Count > 0;
                    }, cts.Token);

                if (cts.IsCancellationRequested) {
                    progressDialog.Finish("Načítání bylo zrušeno uživatelem.", autoClose: false);
                    progressDialog.OnOkClicked = () => progressDialog.Close();
                    return;
                }

                if (!success) {
                    progressDialog.SetErrorState();
                    progressDialog.Finish("Načítání selhalo nebo byl načten poškozený soubor.", autoClose: false);
                    progressDialog.OnOkClicked = () => progressDialog.Close();
                    return;
                }

                // pokud uspesne nacteno
                await PlotSignalGraphAsync();
                progressDialog.Finish("Načítání dokončeno.", autoClose: false);
                progressDialog.OnOkClicked = () => progressDialog.Close();
            }
            catch (OperationCanceledException) {
                progressDialog.Finish("Načítání bylo zrušeno.", autoClose: false);
                progressDialog.OnOkClicked = () => progressDialog.Close();
            }
            catch (Exception ex) {
                progressDialog.SetErrorState();
                progressDialog.Finish($"Chyba při načítání: {ex.Message}", autoClose: false);
                progressDialog.OnOkClicked = () => {
                    progressDialog.Close();
                };
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
        /// Obsluha pohybu kolecka mysi pro zoom
        /// </summary>
        private void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e){
            navService.HandleMouseWheel(e);
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