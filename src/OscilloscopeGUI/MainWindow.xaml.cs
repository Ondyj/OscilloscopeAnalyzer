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
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows.Media;
using System.IO;

namespace OscilloscopeGUI {
    public partial class MainWindow : Window {
        private SignalLoader loader = new SignalLoader(); // Ulozeni dat do tridy
        private SignalPlotter plotter;
        private SignalFileService fileService = new SignalFileService();
        private PlotNavigationService navService;
        private bool isDragging = false;
        private Point lastMousePosition;
        private IProtocolAnalyzer? activeAnalyzer;
        private int currentMatchIndex = 0;
        private byte? searchedValue = null;
        private ScottPlot.Plottables.VerticalLine? matchLine = null;

        private SpiChannelMapping? lastUsedSpiMapping = null;


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
                    navService.ResetView(plotter.EarliestTime);  // zavolani resetu kamery
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

            // Vyber CSV souboru
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            ResetState();

            // Zobrazeni progress dialogu
            var progressDialog = new ProgressDialog();
            progressDialog.Show();
            var progress = new Progress<int>(value => progressDialog.ReportProgress(value));
            progressDialog.OnCanceled = () => cts.Cancel();

            try {
                // Nacitani CSV
                bool success = await Task.Run(() => {
                    loader.LoadCsvFile(openFileDialog.FileName, progress, cts.Token);
                    return loader.SignalData.Count > 0;
                }, cts.Token);

                if (cts.IsCancellationRequested) {
                    progressDialog.Finish("Nacitani bylo zruseno uzivatelem.", autoClose: false);
                    progressDialog.OnOkClicked = () => progressDialog.Close();
                    return;
                }

                if (!success) {
                    progressDialog.SetErrorState();
                    progressDialog.Finish("Nacitani selhalo nebo byl nacten poskozeny soubor.", autoClose: false);
                    progressDialog.OnOkClicked = () => progressDialog.Close();
                    return;
                }

                // Vykresleni
                progressDialog.SetTitle("Vykreslování...");
                progressDialog.SetPhase("Vykreslování");
                progressDialog.ReportMessage("Vykreslování signálu...");

                await plotter.PlotSignalsAsync(loader.SignalData, progress);
                navService.ResetView(plotter.EarliestTime);

                progressDialog.Finish("Vykreslovani dokonceno.", autoClose: true);
                progressDialog.OnOkClicked = () => progressDialog.Close();

                // Vyber protokolu (napr. pomocí vlastního dialogu)
                var protocolDialog = new ProtocolSelectDialog(); 
                protocolDialog.Owner = this;
                if (protocolDialog.ShowDialog() != true)
                    return;

                string selectedProtocol = protocolDialog.SelectedProtocol;

                // Mapovani SPI
                if (selectedProtocol == "SPI") {
                    var spiMapDialog = new SpiChannelMappingDialog(loader.SignalData.Keys.ToList());
                    spiMapDialog.Owner = this;
                    if (spiMapDialog.ShowDialog() != true)
                        return;

                    lastUsedSpiMapping = spiMapDialog.Mapping;

                    // Prejmenovani legendy
                    var renameMap = new Dictionary<string, string> {
                        { lastUsedSpiMapping.ChipSelect, "CS" },
                        { lastUsedSpiMapping.Clock, "SCLK" },
                        { lastUsedSpiMapping.Mosi, "MOSI" }
                    };
                    if (!string.IsNullOrEmpty(lastUsedSpiMapping.Miso))
                        renameMap[lastUsedSpiMapping.Miso] = "MISO";

                    plotter.RenameChannels(renameMap);
                }

                // Inicializace analyzeru podle zvoleneho protokolu
                switch (selectedProtocol) {
                    case "SPI":
                        var inferredSettings = SpiInferenceHelper.InferSettings(loader.SignalData);
                        activeAnalyzer = new SpiProtocolAnalyzer(loader.SignalData, inferredSettings, lastUsedSpiMapping!);
                        break;
                    // TODO: Doplnit UART, I2C, ...
                }
            }
            catch (Exception ex) {
                progressDialog.SetErrorState();
                progressDialog.Finish($"Chyba pri nacitani: {ex.Message}", autoClose: false);
                progressDialog.OnOkClicked = () => progressDialog.Close();
            }
        }


        /// <summary>
        /// Asynchronne vykresli vsechny signaly pomoci tridy SignalPlotter
        /// </summary>
        private async Task PlotSignalGraphAsync() {
            await plotter.PlotSignalsAsync(loader.SignalData); // SignalPlotter
            navService.ResetView(plotter.EarliestTime);
        }

        /// <summary>
        /// Obsluha klavesovych vstupu pro ovladani zoomu a prochazeni vysledku
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e) {
            if (activeAnalyzer is ISearchableAnalyzer searchable && searchable.HasMatches()) {
                if (e.Key == Key.Left) {
                    PrevResult_Click(sender, e);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Right) {
                    NextResult_Click(sender, e);
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Up || e.Key == Key.Down) {
                navService.HandleKey(e.Key);
                e.Handled = true;
                return;
            }

            navService.HandleKey(e.Key); // normalni zoom a posun
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
            if (activeAnalyzer is not ISearchableAnalyzer searchable) {
                MessageBox.Show("Aktivní analyzátor nepodporuje vyhledávání.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string query = SearchBox.Text.Trim();
            if (!byte.TryParse(query, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value)) {
                MessageBox.Show("Neplatný hexadecimální vstup. Zadejte např. 'A5'", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            searchedValue = value;
            searchable.Search(value);

            if (searchable.HasMatches()) {
                currentMatchIndex = 0;
                ShowMatch();
                ResultNavigationPanel.Visibility = Visibility.Visible;
            } else {
                MessageBox.Show($"Hodnota 0x{value:X2} nebyla nalezena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                ResultNavigationPanel.Visibility = Visibility.Collapsed;
            }
        }


        private void ShowMatch() {
            if (activeAnalyzer is not ISearchableAnalyzer searchable || searchedValue == null)
                return;

            if (searchable.MatchCount == 0)
                return;

            ResultInfo.Text = searchable.GetMatchDisplay(currentMatchIndex);
            double timestamp = searchable.GetMatchTimestamp(currentMatchIndex);

            if (currentMatchIndex == 0)
                navService.CenterOn(timestamp);
            else
                navService.MoveTo(timestamp);

            // Pokud znacka jiz existuje, pouze zmenime X
            if (matchLine != null) {
                matchLine.X = timestamp;
            } else {
                matchLine = plot.Plot.Add.VerticalLine(timestamp);
                matchLine.Color = new ScottPlot.Color(255, 0, 0, 128); // pruhledna cervena
                matchLine.LineWidth = 2;
            }

            plot.Refresh();
        }

        private void NextResult_Click(object sender, RoutedEventArgs e) {
            if (activeAnalyzer is not ISearchableAnalyzer searchable || searchable.MatchCount == 0)
                return;

            currentMatchIndex = (currentMatchIndex + 1) % searchable.MatchCount;
            ShowMatch();
        }

        private void PrevResult_Click(object sender, RoutedEventArgs e) {
            if (activeAnalyzer is not ISearchableAnalyzer searchable || searchable.MatchCount == 0)
                return;

            currentMatchIndex = (currentMatchIndex - 1 + searchable.MatchCount) % searchable.MatchCount;
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
            int channelCount = loader.SignalData.Count;

            if (!CheckChannelCount(selectedProtocol, channelCount)) {
                return;
            }

            try {
                switch (selectedProtocol) {
                    case "UART":
                        UartSettings uartSettings;
                        if (isManual) { // Pokud je manualni rezim, zobraz dialog a nacti nastaveni
                            var dialog = new UartSettingsDialog();
                            if (dialog.ShowDialog() != true) return;
                            uartSettings = dialog.Settings;
                        } else { // Automaticka detekce nastaveni ze singnalu
                            try {
                                var rawSamples = loader.SignalData.Values.FirstOrDefault();
                                if (rawSamples == null || rawSamples.Count == 0) {
                                    MessageBox.Show("Nebyly nalezeny žádné signály pro automatickou detekci.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }

                                var signalSamples = rawSamples.Select(t => new SignalSample(t.Item1, t.Item2 > 0.5)).ToList();
                                // Automaticky odhad nastaveni UART:
                                // - Spocita cas mezi zmenami stavu (hranami) => odhadne delku jednoho bitu => vypocita BaudRate.
                                // - Urci, zda je linka v klidu ve stavu HIGH nebo LOW (IdleLevelHigh).
                                // - Tyto data jsou nastaveny pevne DataBits = 8, Parity = None, StopBits = 1
                                uartSettings = UartInferenceHelper.InferUartSettings(signalSamples);
                            } catch (Exception ex) {
                                MessageBox.Show($"Nepodařilo se odhadnout nastavení UART: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }

                        activeAnalyzer = new UartProtocolAnalyzer(loader.SignalData, uartSettings);
                        break;

                        case "SPI":
                            SpiSettings spiSettings;

                            if (isManual) {
                                var dialog = new SpiSettingsDialog();
                                if (dialog.ShowDialog() != true) return;
                                spiSettings = dialog.Settings;
                            } else {
                                try {
                                    spiSettings = SpiInferenceHelper.InferSettings(loader.SignalData);
                                } catch (Exception ex) {
                                    MessageBox.Show($"Nepodařilo se odhadnout nastavení SPI: {ex.Message}",
                                        "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                            }

                            // Pokud už máme uložené mapování z LoadCsv_Click, použijeme ho
                            if (lastUsedSpiMapping == null) {
                                var mapDialog = new SpiChannelMappingDialog(loader.SignalData.Keys.ToList());
                                if (mapDialog.ShowDialog() != true) return;
                                lastUsedSpiMapping = mapDialog.Mapping;
                            }

                            activeAnalyzer = new SpiProtocolAnalyzer(loader.SignalData, spiSettings, lastUsedSpiMapping);
                            break;

                    case "I2C":
                        //TODO
                        //activeAnalyzer = new I2cProtocolAnalyzer(loader.SignalData);
                        break;

                    default:
                        MessageBox.Show("Neni vybran platny protokol.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                }

                if (activeAnalyzer != null) {
                    activeAnalyzer.Analyze();
                    MessageBox.Show($"{activeAnalyzer.ProtocolName} analyza dokoncena.", "Vysledek", MessageBoxButton.OK, MessageBoxImage.Information);
                } else {
                    MessageBox.Show("Analyzer nebyl vytvoren.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Chyba pri analyze: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CheckChannelCount(string protocol, int channelCount) {
            int requiredChannels = protocol switch {
                "UART" => 1,
                "SPI" => 3,
                "I2C" => 2,
                _ => 0
            };

            if (requiredChannels == 0) {
                return true;
            }

            if (channelCount < requiredChannels) {
                MessageBox.Show($"Pro analyzu {protocol} je potreba alespon {requiredChannels} kanaly.", 
                    "Nedostatecny pocet kanalu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

            private void ExportResultsButton_Click(object sender, RoutedEventArgs e) {
                if (activeAnalyzer is not IExportableAnalyzer exportable) {
                    MessageBox.Show("Aktivní analyzátor nepodporuje export.", "Chyba exportu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string outputDir = "Vysledky";
                Directory.CreateDirectory(outputDir);
                string outputPath = GetUniqueFilePath(outputDir, $"{exportable.ProtocolName.ToLower()}.csv");

                exportable.ExportResults(outputPath);

                MessageBox.Show($"Výsledky byly exportovány do:\n{outputPath}", "Export dokončen", MessageBoxButton.OK, MessageBoxImage.Information);
            }


        private string GetUniqueFilePath(string directory, string baseFileName) {
            string baseNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(baseFileName);
            string extension = System.IO.Path.GetExtension(baseFileName);

            string path = System.IO.Path.Combine(directory, baseFileName);
            int counter = 2;

            while (System.IO.File.Exists(path)) {
                path = System.IO.Path.Combine(directory, $"{baseNameWithoutExtension}_{counter}{extension}");
                counter++;
            }

            return path;
        }

        private void ResetState() {
            activeAnalyzer = null;
            currentMatchIndex = 0;
            searchedValue = null;

            ResultNavigationPanel.Visibility = Visibility.Collapsed;
            ResultInfo.Text = "";

            plot.Plot.Clear();
            plot.Refresh();
        }


        private void SearchBox_GotFocus(object sender, RoutedEventArgs e) {
            if (SearchBox.Text == "např. FF") {
                SearchBox.Text = "";
                SearchBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) {
            if (string.IsNullOrWhiteSpace(SearchBox.Text)) {
                SearchBox.Text = "např. FF";
                SearchBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
        }
    }
}