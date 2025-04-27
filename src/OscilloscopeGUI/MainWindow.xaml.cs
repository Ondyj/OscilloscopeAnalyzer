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

        private SpiProtocolAnalyzer? spiAnalyzer;
        private UartProtocolAnalyzer? uartAnalyzer;
        private I2cProtocolAnalyzer? i2cAnalyzer;
        private string? lastAnalyzedProtocol = null;
        private bool isDragging = false;
        private Point lastMousePosition;

        private List<SpiDecodedByte> spiMatches = new();
        private List<UartDecodedByte> uartMatches = new();
        private List<I2cDecodedPacket> i2cMatches = new();
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
            bool hasMatches = (lastAnalyzedProtocol == "SPI" && spiMatches.Count > 0)
                        || (lastAnalyzedProtocol == "UART" && uartMatches.Count > 0);

            if (hasMatches) {
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
                e.Handled = true;
                return;
            }

            navService.HandleKey(e.Key); // normalni zoom
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
                MessageBox.Show("Zadejte hodnotu (napr. FF)", "Vyhledavani", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!byte.TryParse(query, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte targetValue)) {
                MessageBox.Show("Neplatny hexadecimalni vstup. Zadejte napr. 'A5'", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            searchedValue = targetValue; // ulozime hledanou hodnotu
            spiMatches.Clear();
            uartMatches.Clear();

            if (lastAnalyzedProtocol == "SPI" && spiAnalyzer != null) {
                bool hasMiso = spiAnalyzer.HasMisoChannel();

                if (hasMiso) {
                    // mame MISO kanal, hledani v MOSI i MISO
                    spiMatches = spiAnalyzer.DecodedBytes
                        .Where(b => b.ValueMOSI == searchedValue || b.ValueMISO == searchedValue)
                        .ToList();
                } else {
                    // nemame MISO, heldani jen v MOSI
                    spiMatches = spiAnalyzer.DecodedBytes
                        .Where(b => b.ValueMOSI == searchedValue)
                        .ToList();
                }
            } else if (lastAnalyzedProtocol == "UART" && uartAnalyzer != null) {
                uartMatches = uartAnalyzer.DecodedBytes
                    .Where(b => b.Value == searchedValue)
                    .ToList();
            } 
            else if (lastAnalyzedProtocol == "I2C" && i2cAnalyzer != null) {
                i2cMatches = i2cAnalyzer.DecodedPackets
                    .Where(p => p.Data.Any(d => d == searchedValue))
                    .ToList();
            }
            else {
                MessageBox.Show("Vysledky nejsou dostupne. Nejprve provedte analyzu.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool hasMatches = (lastAnalyzedProtocol == "SPI" && spiMatches.Count > 0)
                            || (lastAnalyzedProtocol == "UART" && uartMatches.Count > 0)
                            || (lastAnalyzedProtocol == "I2C" && i2cMatches.Count > 0);

            if (hasMatches) {
                currentMatchIndex = 0;
                ShowMatch();
                ResultNavigationPanel.Visibility = Visibility.Visible;
            } else {
                MessageBox.Show($"Hodnota 0x{targetValue:X2} nebyla nalezena.", "Vysledek", MessageBoxButton.OK, MessageBoxImage.Information);
                ResultNavigationPanel.Visibility = Visibility.Collapsed;
            }
        }


        private void ShowMatch() {
            if (lastAnalyzedProtocol == "SPI" && spiMatches.Count > 0) {
                var match = spiMatches[currentMatchIndex];

                string asciiChar = (match.ValueMOSI >= 32 && match.ValueMOSI <= 126)
                    ? ((char)match.ValueMOSI).ToString()
                    : $"\\x{match.ValueMOSI:X2}";

                string error = match.Error ?? "zadny";
                string timestamp = match.Timestamp.ToString("F9", CultureInfo.InvariantCulture);

                // Zjistime, jestli SPI analyzer ma MISO
                bool hasMiso = spiAnalyzer?.HasMisoChannel() ?? false;

                if (hasMiso) {
                    Console.WriteLine($"[DEBUG] SPI match {currentMatchIndex + 1}/{spiMatches.Count}: ValueMOSI=0x{match.ValueMOSI:X2}, ValueMISO=0x{match.ValueMISO:X2}, Time={timestamp}s, Error={error}");
                } else {
                    Console.WriteLine($"[DEBUG] SPI match {currentMatchIndex + 1}/{spiMatches.Count}: ValueMOSI=0x{match.ValueMOSI:X2}, Time={timestamp}s, Error={error}");
                }

                ResultInfo.Text = $"Time: {timestamp}s | ASCII: {asciiChar} | Error: {error}";

                if (currentMatchIndex == 0) {
                    navService.CenterOn(match.Timestamp);
                } else {
                    navService.MoveTo(match.Timestamp);
                }
            } else if (lastAnalyzedProtocol == "UART" && uartMatches.Count > 0) {
                var match = uartMatches[currentMatchIndex];

                string asciiChar = (match.Value >= 32 && match.Value <= 126)
                    ? ((char)match.Value).ToString()
                    : $"\\x{match.Value:X2}";

                string error = match.Error ?? "zadny";
                string timestamp = match.Timestamp.ToString("F9", CultureInfo.InvariantCulture);

                Console.WriteLine($"[DEBUG] UART match {currentMatchIndex + 1}/{uartMatches.Count}: Value=0x{match.Value:X2}, Time={timestamp}s, Error={error}");

                ResultInfo.Text = $"Time: {timestamp}s | ASCII: {asciiChar} | Error: {error}";

                if (currentMatchIndex == 0) {
                    navService.CenterOn(match.Timestamp);
                } else {
                    navService.MoveTo(match.Timestamp);
                }
            } else if (lastAnalyzedProtocol == "I2C" && i2cMatches.Count > 0) {
                var match = i2cMatches[currentMatchIndex];
                //TODO

                if (currentMatchIndex == 0) {
                    navService.CenterOn(match.StartTimestamp);
                } else {
                    navService.MoveTo(match.StartTimestamp);
                }
            }
        }


        private void NextResult_Click(object sender, RoutedEventArgs e) {
            if (searchedValue == null) return;

            int count = lastAnalyzedProtocol == "SPI" ? spiMatches.Count :
             lastAnalyzedProtocol == "UART" ? uartMatches.Count :
             lastAnalyzedProtocol == "I2C" ? i2cMatches.Count :
             0;
            if (count == 0) return;

            currentMatchIndex = (currentMatchIndex + 1) % count;
            ShowMatch();  
        }

        private void PrevResult_Click(object sender, RoutedEventArgs e) {
            if (searchedValue == null) return;

            int count = lastAnalyzedProtocol == "SPI" ? spiMatches.Count :
             lastAnalyzedProtocol == "UART" ? uartMatches.Count :
             lastAnalyzedProtocol == "I2C" ? i2cMatches.Count :
             0;
            if (count == 0) return;

            currentMatchIndex = (currentMatchIndex - 1 + count) % count;
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

                // kontrola poctu kanalu
                if (!CheckChannelCount(selectedProtocol, channelCount)) {
                    return;
                }

            switch (selectedProtocol) {
                    case "UART":
                        if (isManual) {
                            // Otevreni dialogu pro rucni zadani nastaveni UART
                            var dialog = new UartSettingsDialog();
                            bool? confirmed = dialog.ShowDialog();

                            if (confirmed == true) {
                                var settings = dialog.Settings;

                                Console.WriteLine($"[DEBUG] Používá se UART nastavení:");
                                Console.WriteLine($"        BaudRate = {settings.BaudRate}");
                                Console.WriteLine($"        DataBits = {settings.DataBits}");
                                Console.WriteLine($"        Parity = {settings.Parity}");
                                Console.WriteLine($"        StopBits = {settings.StopBits}");
                                Console.WriteLine($"        IdleLevelHigh = {settings.IdleLevelHigh}");

                                uartAnalyzer = new UartProtocolAnalyzer(loader.SignalData, settings);
                                uartAnalyzer.Analyze();

                                MessageBox.Show("UART analýza dokončena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                                lastAnalyzedProtocol = "UART";
                            }
                        } else {
                            // Nastaveni vychozich parametru pro UART
                            var uartSettings = new UartSettings {
                                BaudRate = 115200,
                                DataBits = 8,
                                Parity = Parity.None,
                                StopBits = 1,
                                IdleLevelHigh = true
                            };

                            Console.WriteLine($"[DEBUG] Používá se výchozí UART nastavení:");
                            Console.WriteLine($"        BaudRate = {uartSettings.BaudRate}");
                            Console.WriteLine($"        DataBits = {uartSettings.DataBits}");
                            Console.WriteLine($"        Parity = {uartSettings.Parity}");
                            Console.WriteLine($"        StopBits = {uartSettings.StopBits}");
                            Console.WriteLine($"        IdleLevelHigh = {uartSettings.IdleLevelHigh}");

                            uartAnalyzer = new UartProtocolAnalyzer(loader.SignalData, uartSettings);
                            uartAnalyzer.Analyze();

                            MessageBox.Show("UART analýza dokončena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                            lastAnalyzedProtocol = "UART";
                        }
                    break;

                    case "SPI":
                        if (isManual) {
                            var dialog = new SpiSettingsDialog();
                            bool? confirmed = dialog.ShowDialog();

                            if (confirmed == true) {
                                var settings = dialog.Settings;
                                Console.WriteLine($"[DEBUG] Používá se SPI nastavení: CPOL={(settings.Cpol ? 1 : 0)}, CPHA={(settings.Cpha ? 1 : 0)}, BitsPerWord={settings.BitsPerWord}");
                                spiAnalyzer = new SpiProtocolAnalyzer(loader.SignalData, settings);
                                spiAnalyzer.Analyze();
                                MessageBox.Show("SPI analýza dokončena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                                lastAnalyzedProtocol = "SPI";
                            }
                        } else {
                            var spiSettings = new SpiSettings {
                                Cpol = false,
                                Cpha = false,
                                BitsPerWord = 8
                            };
                            Console.WriteLine($"[DEBUG] Používá se výchozí SPI nastavení: CPOL={(spiSettings.Cpol ? 1 : 0)}, CPHA={(spiSettings.Cpha ? 1 : 0)}, BitsPerWord={spiSettings.BitsPerWord}");

                            spiAnalyzer = new SpiProtocolAnalyzer(loader.SignalData, spiSettings);
                            spiAnalyzer.Analyze();
                            MessageBox.Show("SPI analýza dokončena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                            lastAnalyzedProtocol = "SPI";
                        }
                    break;

                    case "I2C":
                        try {
                            i2cAnalyzer = new I2cProtocolAnalyzer(loader.SignalData);
                            i2cAnalyzer.Analyze();

                            Console.WriteLine($"[DEBUG] I2C analýza dokončena.");
                            // TODO
                            lastAnalyzedProtocol = "I2C";
                        }
                        catch (Exception ex) {
                            MessageBox.Show($"Chyba při I2C analýze: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                            Console.WriteLine($"[ERROR] Chyba I2C analýzy: {ex}");
                        }
                    break;

                default:
                    MessageBox.Show("Neni vybran platny protokol.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
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
            if (string.IsNullOrEmpty(lastAnalyzedProtocol)) {
                MessageBox.Show("Nebyl proveden žádný exportovatelný protokol.", "Chyba exportu", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string outputDir = "Vysledky";
            Directory.CreateDirectory(outputDir);
            string outputPath = "";

            switch (lastAnalyzedProtocol) {
                case "UART":
                    if (uartAnalyzer != null) {
                        outputPath = GetUniqueFilePath(outputDir, "uart.csv");
                        uartAnalyzer.ExportResults(outputPath);
                        MessageBox.Show($"Výsledky UART byly exportovány do:\n{outputPath}", "Export dokončen", MessageBoxButton.OK, MessageBoxImage.Information);
                    } else {
                        MessageBox.Show("Není k dispozici žádná data UART k exportu.", "Chyba exportu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    break;

                case "SPI":
                    if (spiAnalyzer != null) {
                        outputPath = GetUniqueFilePath(outputDir, "spi.csv");
                        spiAnalyzer.ExportResults(outputPath);
                        MessageBox.Show($"Výsledky SPI byly exportovány do:\n{outputPath}", "Export dokončen", MessageBoxButton.OK, MessageBoxImage.Information);
                    } else {
                        MessageBox.Show("Není k dispozici žádná data SPI k exportu.", "Chyba exportu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    break;

                /*case "I2C":
                    MessageBox.Show("Export výsledků pro I2C zatím není implementován.", "Chyba exportu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;*/

                default:
                    MessageBox.Show("Neznámý typ protokolu pro export.", "Chyba exportu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
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
            if (spiAnalyzer != null) {
                spiAnalyzer.DecodedBytes.Clear();
                spiAnalyzer = null;
            }

            if (uartAnalyzer != null) {
                uartAnalyzer.DecodedBytes.Clear();
                uartAnalyzer = null;
            }

            lastAnalyzedProtocol = null;
            ResultNavigationPanel.Visibility = Visibility.Collapsed;
            ResultInfo.Text = "";
            spiMatches.Clear();
            uartMatches.Clear();
            searchedValue = null;
            currentMatchIndex = 0;

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