using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.WPF;
using OscilloscopeCLI.Signal;
using System.Windows.Controls;
using OscilloscopeCLI.Protocols;
using OscilloscopeGUI.Plotting;
using OscilloscopeGUI.Services;
using OscilloscopeGUI.Services.Protocols;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Globalization;

namespace OscilloscopeGUI {
    public partial class MainWindow : Window {
        private SignalLoader loader = new SignalLoader(); // Ulozeni dat do tridy
        private SignalPlotter plotter;
        private UartAnalysisService uartService = new UartAnalysisService();
        private SignalFileService fileService = new SignalFileService();
        private PlotNavigationService navService;

        private SpiProtocolAnalyzer? spiAnalyzer;
        private bool isDragging = false;
        private Point lastMousePosition;

        private List<SpiDecodedByte> matches = new();
        private int currentMatchIndex = 0;
        private byte? searchedValue = null; // ulozena hledana hodnota


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

            plot.MouseDown += (s, e) => {
                if (e.ChangedButton == MouseButton.Middle) {
                    navService.ResetView();  // zavolani resetu kamery
                    e.Handled = true;
                }
            };

            plot.MouseLeftButtonDown += (s, e) => {
                isDragging = true;
                lastMousePosition = e.GetPosition(plot);
                plot.CaptureMouse(); // zachytime mys
            };

            plot.MouseLeftButtonUp += (s, e) => {
                isDragging = false;
                plot.ReleaseMouseCapture(); // uvolnime mys
            };

            plot.MouseMove += (s, e) => {
                if (isDragging) {
                    Point currentPos = e.GetPosition(plot);
                    double deltaX = currentPos.X - lastMousePosition.X;
                    navService.PanByPixelDelta(deltaX);
                    lastMousePosition = currentPos;
                }
            };
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

            ResetState(); // vymaze se stav

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
            navService.ResetView();
        }

        /// <summary>
        /// Obsluha klavesovych vstupu pro ovladani zoomu
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e) {
            if (matches.Count > 0) {
                if (e.Key == Key.Left) {
                    PrevResult_Click(sender, e);
                    e.Handled = true; // zakazani jakekoliv funkce sipky
                    return;
                }
                if (e.Key == Key.Right) {
                    NextResult_Click(sender, e);
                    e.Handled = true; // zakazani jakekoliv funkce sipky
                    return;
                }
            }

            if (e.Key == Key.Up || e.Key == Key.Down) {
                navService.HandleKey(e.Key);
                // zakazani sipky nahoru a dolu
                e.Handled = true;
                return;
            }

            // normalne ovladame zoom
            navService.HandleKey(e.Key);
        }


        /// <summary>
        /// Obsluha pohybu kolecka mysi pro zoom
        /// </summary>
        private void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e){
            navService.HandleMouseWheel(e);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                SearchButton_Click(sender, e);
                plot.Focus();
            }
        }


        private void SearchButton_Click(object sender, RoutedEventArgs e) {
            string query = SearchBox.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(query)) {
                MessageBox.Show("Zadejte hodnotu (např. FF)", "Vyhledávání", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!byte.TryParse(query, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte targetValue)) {
                MessageBox.Show("Neplatný hexadecimální vstup. Zadejte např. 'A5'", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (spiAnalyzer == null || spiAnalyzer.DecodedBytes.Count == 0) {
                MessageBox.Show("Výsledky nejsou dostupné. Nejprve proveď analýzu.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            searchedValue = targetValue; // ulozeni hledane hodnoty

            matches = spiAnalyzer.DecodedBytes
                .Where(b => b.ValueMOSI == searchedValue)
                .ToList();

            if (matches.Count > 0) {
                currentMatchIndex = 0;
                ShowMatch();
                ResultNavigationPanel.Visibility = Visibility.Visible;
            } else {
                MessageBox.Show($"Hodnota 0x{targetValue:X2} nebyla nalezena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                ResultNavigationPanel.Visibility = Visibility.Collapsed;
            }
        }
        private void ShowMatch() {
            if (matches.Count == 0) return;

            var match = matches[currentMatchIndex];

            string asciiChar = (match.ValueMOSI >= 32 && match.ValueMOSI <= 126) 
                ? ((char)match.ValueMOSI).ToString() 
                : $"\\x{match.ValueMOSI:X2}";

            string error = match.Error ?? "žádný";
            string timestamp = match.Timestamp.ToString("F9", CultureInfo.InvariantCulture);

            ResultInfo.Text = $"Time: {timestamp}s | ASCII: {asciiChar} | Error: {error}";


            if (currentMatchIndex == 0) {
                // posune i priblizi
                navService.CenterOn(match.Timestamp);
            } else {
                // pouze posune
                navService.MoveTo(match.Timestamp);
            }
        }

        private void NextResult_Click(object sender, RoutedEventArgs e) {
            if (searchedValue == null || spiAnalyzer == null) return;

            if (matches.Count == 0) {
                matches = spiAnalyzer.DecodedBytes
                    .Where(b => b.ValueMOSI == searchedValue || b.ValueMISO == searchedValue)
                    .ToList();
                if (matches.Count == 0) return;
                currentMatchIndex = 0;
            }

            currentMatchIndex = (currentMatchIndex + 1) % matches.Count;
            ShowMatch();
        }

        private void PrevResult_Click(object sender, RoutedEventArgs e) {
            if (searchedValue == null || spiAnalyzer == null) return;

            if (matches.Count == 0) {
                matches = spiAnalyzer.DecodedBytes
                    .Where(b => b.ValueMOSI == searchedValue || b.ValueMISO == searchedValue)
                    .ToList();
                if (matches.Count == 0) return;
                currentMatchIndex = 0;
            }

            currentMatchIndex = (currentMatchIndex - 1 + matches.Count) % matches.Count;
            ShowMatch();
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
                    if (isManual) {
                        var dialog = new SpiSettingsDialog();
                        bool? confirmed = dialog.ShowDialog();

                        if (confirmed == true) {
                            var settings = dialog.Settings;
                            spiAnalyzer = new SpiProtocolAnalyzer(loader.SignalData, settings);
                            spiAnalyzer.Analyze();
                            MessageBox.Show("SPI analýza dokončena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    } else {
                        var spiSettings = new SpiSettings {
                            Cpol = false,
                            Cpha = false,
                            BitsPerWord = 8
                        };

                        spiAnalyzer = new SpiProtocolAnalyzer(loader.SignalData, spiSettings);
                        spiAnalyzer.Analyze();
                        MessageBox.Show("SPI analýza dokončena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                case "I2C":
                    MessageBox.Show($"{selectedProtocol} zatim neni implementovano.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;

                default:
                    MessageBox.Show("Neni vybran platny protokol.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
        }

        private void ResetState() {
            if (spiAnalyzer != null) {
                spiAnalyzer.DecodedBytes.Clear();
                spiAnalyzer = null;
            }

            ResultNavigationPanel.Visibility = Visibility.Collapsed;
            ResultInfo.Text = "";
            matches.Clear();
            searchedValue = null;
            currentMatchIndex = 0;

            plot.Plot.Clear();
            plot.Refresh();
        }
    }
}