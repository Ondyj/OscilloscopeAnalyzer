using System.Windows;
using OscilloscopeCLI.Signal;
using System.Windows.Controls;
using OscilloscopeCLI.Protocols;
using OscilloscopeGUI.Plotting;
using OscilloscopeGUI.Services;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Media;
using ScottPlot;

namespace OscilloscopeGUI {
    public partial class MainWindow : Window {
        private SignalLoader loader = new SignalLoader(); // nacita data ze souboru CSV
        private SignalPlotter plotter; // zodpovida za vykreslovani signalu
        private PlotNavigationService navService; // ovladani zoomu a posunu v grafu
        private SearchService searchService; // vyhledavani v analyzovanych datech
        private FileLoadingService fileLoadingService = new FileLoadingService(); // nacitani CSV souboru s dialogem a pokrokem
        private ProtocolAnalysisService protocolAnalysisService = new ProtocolAnalysisService(); // analyza podle vybraneho protokolu
        private ExportService exportService = new ExportService(); // export vysledku analyzy do CSV
        private bool isDragging = false; // priznak, zda uzivatel prave posouva graf
        private Point lastMousePosition; // posledni pozice mysi pri posunu
        private IProtocolAnalyzer? activeAnalyzer; // aktualne pouzivany analyzer protokolu
        private SpiChannelMapping? lastUsedSpiMapping = null; // naposledy pouzite mapovani SPI kanalu
        private UartChannelMapping? lastUsedUartMapping = null;
        private string? loadedFilePath = null; // cesta k nactenemu CSV souboru     

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        private List<ScottPlot.Plottables.Text> byteLabels = new();
        private List<IPlottable> byteStartLines = new();

        private enum ByteDisplayFormat { Hex, Dec, Ascii }
        private ByteDisplayFormat currentFormat = ByteDisplayFormat.Hex;
        private bool? wasManualAnalysis = null;
        private List<UartDecodedByte>? filteredUartBytes = null;
        private List<SpiDecodedByte>? filteredSpiBytes = null;
        private double? timeMark1 = null;
        private double? timeMark2 = null;
        private ScottPlot.Plottables.VerticalLine? line1 = null;
        private ScottPlot.Plottables.VerticalLine? line2 = null;

        /// <summary>
        /// Konstruktor hlavniho okna aplikace
        /// </summary>
        public MainWindow() {
            AllocConsole();
            InitializeComponent();

            plot.UserInputProcessor.Disable();
            this.KeyDown += MainWindow_KeyDown;
            this.MouseWheel += MainWindow_MouseWheel;

            plotter = new SignalPlotter(plot);
            navService = new PlotNavigationService(plot);
            searchService = new SearchService(plot, navService);
            searchService.AttachUi(ResultInfo, ResultNavigationPanel);

            plot.MouseDown += (s, e) => {
                if (e.ChangedButton == MouseButton.Middle) {
                    navService.ResetView(plotter.EarliestTime);
                    e.Handled = true;
                }
            };

            plot.MouseLeftButtonDown += (s, e) => {
                isDragging = true;
                lastMousePosition = e.GetPosition(plot);
                plot.CaptureMouse();
                UpdateAnnotations();
            };

            plot.MouseLeftButtonUp += (s, e) => {
                isDragging = false;
                plot.ReleaseMouseCapture();
            };

            plot.MouseMove += (s, e) => {
                if (isDragging) {
                    Point currentPos = e.GetPosition(plot);
                    double deltaX = currentPos.X - lastMousePosition.X;
                    navService.PanByPixelDelta(deltaX);
                    lastMousePosition = currentPos;
                }
            };

            plot.MouseRightButtonDown += Plot_MouseRightButtonDown;

            plot.MouseWheel += (s, e) => UpdateAnnotations();
            plot.MouseLeftButtonUp += (s, e) => UpdateAnnotations();
            plot.MouseRightButtonUp += (s, e) => UpdateAnnotations();
            this.KeyUp += (s, e) => UpdateAnnotations();
        }

        private void UpdateAnnotations() {
            foreach (var label in byteLabels)
                plot.Plot.Remove(label);
            byteLabels.Clear();

            foreach (var line in byteStartLines)
                plot.Plot.Remove(line);
            byteStartLines.Clear();

            if (activeAnalyzer is null)
                return;

            var limits = plot.Plot.Axes.GetLimits();
            double xMin = limits.Left;
            double xMax = limits.Right;

            ScottPlot.Color startColor = ScottPlot.Colors.Gray;
            ScottPlot.Color altColor = ScottPlot.Colors.Black;

            string FormatByte(byte b) {
                return currentFormat switch {
                    ByteDisplayFormat.Hex => $"0x{b:X2}",
                    ByteDisplayFormat.Dec => b.ToString(),
                    ByteDisplayFormat.Ascii => char.IsControl((char)b) ? "." : ((char)b).ToString(),
                    _ => $"0x{b:X2}"
                };
            }

            if (activeAnalyzer is UartProtocolAnalyzer uart) {
                var bytes = filteredUartBytes ?? uart.DecodedBytes;

                for (int i = 0; i < bytes.Count; i++) {
                    var b = bytes[i];
                    double centerX = (b.StartTime + b.EndTime) / 2;
                    if (centerX < xMin || centerX > xMax)
                        continue;

                    var color = (i % 2 == 0) ? startColor : altColor;

                    var text = plot.Plot.Add.Text(FormatByte(b.Value), centerX, 1.3);
                    text.LabelStyle.FontSize = 16;
                    text.LabelStyle.Bold = true;
                    text.LabelFontColor = color;
                    byteLabels.Add(text);

                    var lineStart = plot.Plot.Add.VerticalLine(b.StartTime);
                    lineStart.Color = color;
                    lineStart.LineWidth = 1;
                    lineStart.LinePattern = ScottPlot.LinePattern.Dashed;
                    byteStartLines.Add(lineStart);

                    var lineEnd = plot.Plot.Add.VerticalLine(b.EndTime);
                    lineEnd.Color = color;
                    lineEnd.LineWidth = 1;
                    lineEnd.LinePattern = ScottPlot.LinePattern.Dashed;
                    byteStartLines.Add(lineEnd);
                }
            } else if (activeAnalyzer is SpiProtocolAnalyzer spi) {
                var bytes = filteredSpiBytes ?? spi.DecodedBytes;

                for (int i = 0; i < bytes.Count; i++) {
                    var b = bytes[i];
                    double centerX = (b.StartTime + b.EndTime) / 2;
                    if (centerX < xMin || centerX > xMax)
                        continue;

                    var color = (i % 2 == 0) ? startColor : altColor;

                    // MOSI – nahoru
                    var textMosi = plot.Plot.Add.Text(FormatByte(b.ValueMOSI), centerX, 1.3);
                    textMosi.LabelStyle.FontSize = 16;
                    textMosi.LabelStyle.Bold = true;
                    textMosi.LabelFontColor = color;
                    byteLabels.Add(textMosi);

                    // MISO – dolu pod graf
                    if (b.HasMISO) {
                        var textMiso = plot.Plot.Add.Text(FormatByte(b.ValueMISO), centerX, -1.5);
                        textMiso.LabelStyle.FontSize = 16;
                        textMiso.LabelStyle.Bold = true;
                        textMiso.LabelFontColor = color;
                        byteLabels.Add(textMiso);
                    }

                    var lineStart = plot.Plot.Add.VerticalLine(b.StartTime);
                    lineStart.Color = color;
                    lineStart.LineWidth = 1;
                    lineStart.LinePattern = ScottPlot.LinePattern.Dashed;
                    byteStartLines.Add(lineStart);

                    var lineEnd = plot.Plot.Add.VerticalLine(b.EndTime);
                    lineEnd.Color = color;
                    lineEnd.LineWidth = 1;
                    lineEnd.LinePattern = ScottPlot.LinePattern.Dashed;
                    byteStartLines.Add(lineEnd);
                }
            }

            plot.Refresh();
        }


        private void FormatChanged(object sender, RoutedEventArgs e) {
            if (sender is RadioButton rb && rb.IsChecked == true) {
                switch (rb.Content.ToString()) {
                    case "HEX":
                        currentFormat = ByteDisplayFormat.Hex;
                        break;
                    case "DEC":
                        currentFormat = ByteDisplayFormat.Dec;
                        break;
                    case "ASCII":
                        currentFormat = ByteDisplayFormat.Ascii;
                        break;
                }
                UpdateAnnotations();
            }
        }

        private void FilterRadio_Checked(object sender, RoutedEventArgs e) {
            if (FilterAllRadio == null || FilterNoErrorRadio == null || FilterErrorRadio == null)
                return;

            string filter = "all";

            if (FilterNoErrorRadio.IsChecked == true)
                filter = "noerror";
            else if (FilterErrorRadio.IsChecked == true)
                filter = "error";

            ApplyFilter(filter);
        }

        private void ApplyFilter(string filter) {
            if (activeAnalyzer is UartProtocolAnalyzer uart) {
                filteredUartBytes = filter switch
                {
                    "error" => uart.DecodedBytes.Where(b => !string.IsNullOrEmpty(b.Error)).ToList(),
                    "noerror" => uart.DecodedBytes.Where(b => string.IsNullOrEmpty(b.Error)).ToList(),
                    _ => uart.DecodedBytes.ToList()
                };
            }
            else if (activeAnalyzer is SpiProtocolAnalyzer spi) {
                filteredSpiBytes = filter switch
                {
                    "error" => spi.DecodedBytes.Where(b => !string.IsNullOrEmpty(b.Error)).ToList(),
                    "noerror" => spi.DecodedBytes.Where(b => string.IsNullOrEmpty(b.Error)).ToList(),
                    _ => spi.DecodedBytes.ToList()
                };
            }

            UpdateAnnotations();
        }

        /// <summary>
        /// Zpracuje stisk klavesnice pro navigaci a vyhledavani
        /// </summary>
        private void MainWindow_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Left) {
                searchService.PreviousMatch();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Right) {
                searchService.NextMatch();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up || e.Key == Key.Down) {
                navService.HandleKey(e.Key);
                e.Handled = true;
                return;
            }
            navService.HandleKey(e.Key);
        }

        /// <summary>
        /// Zpracuje pohyb kolecka mysi pro zoom grafu
        /// </summary>
        private void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e) {
            navService.HandleMouseWheel(e);
        }

        /// <summary>
        /// Zpracuje stisk klavesy Enter ve vyhledavacim poli
        /// </summary>
        private void SearchBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                SearchButton_Click(sender, e);
                plot.Focus();
            }
        }

        /// <summary>
        /// Nacte CSV soubor, vykresli signal a provede mapovani kanalu podle zvoleneho protokolu
        /// </summary>
        private async void LoadCsv_Click(object sender, RoutedEventArgs e) {
            

            // 1. Vyber CSV souboru
            var filePick = fileLoadingService.PromptForFileOnly(this);
            if (!filePick.Success)
                return;

            string selectedFilePath = filePick.FilePath!;

            ResetState();


            // 2. Nacteni CSV souboru
            var loadResult = await fileLoadingService.LoadFromFilePathAsync(loader, selectedFilePath, this);
            if (!loadResult.Success)
                return;

            loadedFilePath = selectedFilePath;

            // 3. Ziskani aktivnich kanalu
            var activeChannels = loader.GetRemainingChannelNames();
            Console.WriteLine("Aktivní kanály:");
            foreach (var ch in activeChannels)
                Console.WriteLine(ch);

            // 4. Vyber protokolu realny pocet kanalu
            var protocolDialog = new ProtocolSelectDialog(activeChannels.Count) {
                Owner = this
            };
            if (protocolDialog.ShowDialog() != true)
                return;

            string selectedProtocol = protocolDialog.SelectedProtocol;

            // 5. Mapovani podle protokolu
            Dictionary<string, string> renameMap = new();
            if (selectedProtocol == "SPI") {
                var defaultMapping = new SpiChannelMapping();
                Console.WriteLine("[SPI] Výchozí mapování před zadáním uživatele:");
                Console.WriteLine($"  CS   = {defaultMapping.ChipSelect}");
                Console.WriteLine($"  SCLK = {defaultMapping.Clock}");
                Console.WriteLine($"  MOSI = {defaultMapping.Mosi}");
                Console.WriteLine($"  MISO = {defaultMapping.Miso}");

                var spiMapDialog = new SpiChannelMappingDialog(activeChannels);
                spiMapDialog.Owner = this;
                if (spiMapDialog.ShowDialog() != true)
                    return;

                lastUsedSpiMapping = spiMapDialog.Mapping;

                Console.WriteLine("[SPI] Mapování po výběru uživatele:");
                Console.WriteLine($"  CS   = {lastUsedSpiMapping.ChipSelect}");
                Console.WriteLine($"  SCLK = {lastUsedSpiMapping.Clock}");
                Console.WriteLine($"  MOSI = {lastUsedSpiMapping.Mosi}");
                Console.WriteLine($"  MISO = {lastUsedSpiMapping.Miso}");

                renameMap = new Dictionary<string, string> {
                    { lastUsedSpiMapping.ChipSelect, "CS" },
                    { lastUsedSpiMapping.Clock, "SCLK" },
                    { lastUsedSpiMapping.Mosi, "MOSI" }
                };
                if (!string.IsNullOrEmpty(lastUsedSpiMapping.Miso))
                    renameMap[lastUsedSpiMapping.Miso] = "MISO";
            } else if (selectedProtocol == "UART") {
                var uartMapDialog = new UartChannelMappingDialog(activeChannels);
                uartMapDialog.Owner = this;
                if (uartMapDialog.ShowDialog() != true)
                    return;

                var mapping = uartMapDialog.ChannelRenames;
                lastUsedUartMapping = new UartChannelMapping {
                    Tx = mapping.FirstOrDefault(kv => kv.Value == "TX").Key ?? "",
                    Rx = mapping.FirstOrDefault(kv => kv.Value == "RX").Key ?? ""
                };

                // podminka pro jednosmerne mapovani
                if (mapping.Count > 1) {
                    // jen pokud mám více než 1 přiřazenou roli, vyžaduji obě signály
                    if (!lastUsedUartMapping.IsValid()) {
                        MessageBox.Show(
                            "Mapování UART signálů není validní (TX a RX musí být různé a neprázdné).",
                            "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else if (mapping.Count == 0) {
                    // pokud uživatel nevybral ani RX ani TX
                    MessageBox.Show(
                        "Musíte vybrat alespoň jednu roli (RX nebo TX).",
                        "Neúplné mapování", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // pokud mapping.Count == 1, bereme jednosměrné měření a nevoláme IsValid

                renameMap = new Dictionary<string, string>();
                foreach (var kv in mapping)
                    renameMap[kv.Key] = kv.Value;
            }

            // 6. Vykresleni signalu (s prejmenovanymi klici)
            var progressDialog = new ProgressDialog {
                Owner = this
            };
            progressDialog.Show();
            progressDialog.SetTitle("Vykreslování...");
            progressDialog.SetPhase("Vykreslování");
            progressDialog.ReportMessage("Vykreslování signálu...");
            var progress = new Progress<int>(value => progressDialog.ReportProgress(value));

            // pokud existuje renameMap, vytvorime novy slovnik s prejmenovanymi klici
            var finalData = renameMap.Count > 0
                ? loader.SignalData.ToDictionary(
                    kvp => renameMap.TryGetValue(kvp.Key, out var newName) ? newName : kvp.Key,
                    kvp => kvp.Value)
                : loader.SignalData;

            await plotter.PlotSignalsAsync(finalData, progress);

            double minTime = finalData.Values.SelectMany(list => list.Select(p => p.Time)).Min();
            double maxTime = finalData.Values.SelectMany(list => list.Select(p => p.Time)).Max();
            double duration = maxTime - minTime;
            navService.SetZoomOutLimitBasedOnDuration(duration);
            navService.ResetView(plotter.EarliestTime);
            progressDialog.Finish("Vykreslování dokončeno.", autoClose: true);
            progressDialog.OnOkClicked = () => progressDialog.Close();
        }


        /// <summary>
        /// Spusti analyzu signalu podle zvoleneho protokolu
        /// </summary>
        private void AnalyzeButton_Click(object sender, RoutedEventArgs e) {
            string selectedProtocol = (ProtocolComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            bool isManual = ManualRadio.IsChecked == true;
            wasManualAnalysis = ManualRadio.IsChecked == true;

            // 1 kanal → jen UART, 2 kanaly → UART i SPI, 3+ kanaly → jen SPI
            if (!CheckChannelCount(selectedProtocol, loader.SignalData.Count))
                return;

            var analyzer = protocolAnalysisService.Analyze(
                selectedProtocol,
                isManual,
                loader,
                lastUsedUartMapping,
                ref lastUsedSpiMapping,
                this
            );

            if (analyzer != null) {
                analyzer.Analyze();
                SetAnalyzer(analyzer);
                UpdateStatistics();
                MessageBox.Show($"{analyzer.ProtocolName} analýza dokončena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Overi, zda se zvolený protokol hodi na aktualni pocet kanalu:
        ///   - 1 kanal  → pouze UART
        ///   - 2 kanaly → UART i SPI
        ///   - 3+kanaly → pouze SPI
        /// </summary>
        private bool CheckChannelCount(string protocol, int channelCount) {
            // 1 kanal: jen UART
            if (channelCount == 1 && protocol != "UART") {
                MessageBox.Show(
                    "S jedním kanálem lze dekódovat pouze UART.",
                    "Neplatná konfigurace",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // 3 nebo více kanálů: jen SPI
            if (channelCount >= 3 && protocol != "SPI") {
                MessageBox.Show(
                    "Při třech a více kanálech lze dekódovat pouze SPI.",
                    "Neplatná konfigurace",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // 2 kanály: UART i SPI OK, ale zkontrolujeme minimální požadavek
            int requiredMin = protocol switch {
                "UART" => 1,
                "SPI"  => 2,
                _      => 0
            };
            if (channelCount < requiredMin) {
                var suffix = requiredMin > 1 ? "ů" : "";
                MessageBox.Show(
                    $"Pro analýzu {protocol} je potřeba alespoň {requiredMin} kanál{suffix}.",
                    "Nedostatečný počet kanálů",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Zpracuje kliknuti pravym tlacitkem a zmeri casovy rozdil mezi dvema body
        /// </summary>
        private void Plot_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            // Ziskani DPI obrazovky
            var source = PresentationSource.FromVisual(this);
            double dpiX = 1.0, dpiY = 1.0;
            if (source != null) {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            // Ziskani pozice mysi a uprava podle DPI
            Point mousePos = e.GetPosition(plot);
            double adjustedX = mousePos.X * dpiX;
            double adjustedY = mousePos.Y * dpiY;

            // Vytvoreni pixelu a prevod na souradnice grafu
            var pixel = new ScottPlot.Pixel(adjustedX, adjustedY);
            var coord = plot.Plot.GetCoordinates(pixel);
            double t = coord.X;

            if (!timeMark1.HasValue) {
                timeMark1 = t;
                line1 = plot.Plot.Add.VerticalLine(t);
                line1.Color = new ScottPlot.Color(0, 150, 0);
                line1.LineWidth = 2;
                MeasurementInfo.Visibility = Visibility.Collapsed;
            } else if (!timeMark2.HasValue) {
                timeMark2 = t;
                line2 = plot.Plot.Add.VerticalLine(t);
                line2.Color = new ScottPlot.Color(0, 150, 0);
                line2.LineWidth = 2;
                ShowTimeDifference();
            } else {
                if (line1 != null) plot.Plot.Remove(line1);
                if (line2 != null) plot.Plot.Remove(line2);
                line1 = line2 = null;
                timeMark1 = timeMark2 = null;
                MeasurementInfo.Visibility = Visibility.Collapsed;
            }

            plot.Refresh();
            e.Handled = true;
        }

        /// <summary>
        /// Vypocita a zobrazi casovy rozdil mezi dvema body
        /// </summary>
        private void ShowTimeDifference() {
            if (timeMark1.HasValue && timeMark2.HasValue) {
                double delta = Math.Abs(timeMark2.Value - timeMark1.Value);

                string formattedDelta;
                if (delta >= 1)
                    formattedDelta = $"{delta:F6} s";
                else if (delta >= 1e-3)
                    formattedDelta = $"{delta * 1e3:F3} ms";
                else if (delta >= 1e-6)
                    formattedDelta = $"{delta * 1e6:F3} µs";
                else
                    formattedDelta = $"{delta * 1e9:F3} ns";

                MeasurementInfo.Text = $"{formattedDelta}";
                MeasurementInfo.Visibility = Visibility.Visible;

                // Vypocet stredu v case
                double centerX = (timeMark1.Value + timeMark2.Value) / 2;

                // Ziskani DPI transformace
                var source = PresentationSource.FromVisual(this);
                double dpiX = 1.0;
                if (source != null)
                    dpiX = source.CompositionTarget.TransformToDevice.M11;

                // Prevadi stred na pixely a zarovna text doprostred
                var pixel = plot.Plot.GetPixel(new ScottPlot.Coordinates(centerX, 0));
                double pixelX = pixel.X / dpiX;
                MeasurementInfo.Margin = new Thickness(pixelX - MeasurementInfo.ActualWidth / 2, 10, 0, 0);
            }
        }

        /// <summary>
        /// Provede export vysledku analyzy do CSV souboru
        /// </summary>
        private void ExportResultsButton_Click(object sender, RoutedEventArgs e) {
            exportService.Export(activeAnalyzer, loadedFilePath);
        }

        /// <summary>
        /// Resetuje aplikaci do vychoziho stavu pred nactenim noveho souboru
        /// </summary>
        private void ResetState() {
            activeAnalyzer = null;

            ResultInfo.Text = "";

            timeMark1 = null;
            timeMark2 = null;
            line1 = null;
            line2 = null;

            filteredUartBytes = null;
            filteredSpiBytes = null;

            MeasurementInfo.Text = "";
            MeasurementInfo.Visibility = Visibility.Collapsed;

            ResetStatistics();

            plot.Plot.Clear();
            plot.Refresh();
        }

        /// <summary>
        /// Spusti vyhledavani v dekodovanych datech
        /// </summary>
        private void SearchButton_Click(object sender, RoutedEventArgs e) {
            searchService.Search(SearchBox.Text.Trim());
        }

        /// <summary>
        /// Preskoci na dalsi nalezeny vysledek
        /// </summary>
        private void NextResult_Click(object sender, RoutedEventArgs e) {
            searchService.NextMatch();
        }

        private void UpdateStatistics() {
            int total = 0;
            int errors = 0;
            double avgDurationUs = 0;
            double minUs = 0;
            double maxUs = 0;

            if (activeAnalyzer is UartProtocolAnalyzer uart) {
                var bytes = filteredUartBytes ?? uart.DecodedBytes;
                total = bytes.Count;
                errors = bytes.Count(b => !string.IsNullOrEmpty(b.Error));
                avgDurationUs = bytes.Count > 0 ? bytes.Average(b => (b.EndTime - b.StartTime) * 1e6) : 0;
                minUs = bytes.Count > 0 ? bytes.Min(b => (b.EndTime - b.StartTime) * 1e6) : 0;
                maxUs = bytes.Count > 0 ? bytes.Max(b => (b.EndTime - b.StartTime) * 1e6) : 0;

                double bitsPerByte = uart.Settings.DataBits + 1 + (uart.Settings.Parity == Parity.None ? 0 : 1) + uart.Settings.StopBits;
                double avgBaud = avgDurationUs > 0 ? (bitsPerByte * 1_000_000.0 / avgDurationUs) : 0;

                StatsBaudRate.Text = $"Odhadovaná přenosová rychlost: {avgBaud:F0} baud";
                StatsMinMaxDuration.Text = $"Délka bajtu (min/max): {minUs:F1} / {maxUs:F1} µs";
                StatsSpiTransfers.Text = "Počet přenosů (SPI): –";
                StatsMosiMiso.Text = "Počet bajtů MOSI/MISO: –";
            }
            else if (activeAnalyzer is SpiProtocolAnalyzer spi) {
                var bytes = filteredSpiBytes ?? spi.DecodedBytes;
                total = bytes.Count;
                errors = bytes.Count(b => !string.IsNullOrEmpty(b.Error));
                avgDurationUs = bytes.Count > 0 ? bytes.Average(b => (b.EndTime - b.StartTime) * 1e6) : 0;
                minUs = bytes.Count > 0 ? bytes.Min(b => (b.EndTime - b.StartTime) * 1e6) : 0;
                maxUs = bytes.Count > 0 ? bytes.Max(b => (b.EndTime - b.StartTime) * 1e6) : 0;

                int transferCount = spi.TransferCount;
                double avgTransferLength = spi.AvgTransferDurationUs;
                int misoBytes = bytes.Count(b => b.HasMISO);

                StatsBaudRate.Text = $"Prům. délka SPI přenosu: {avgTransferLength:F1} µs";
                StatsMinMaxDuration.Text = $"Délka bajtu (min/max): {minUs:F1} / {maxUs:F1} µs";
                StatsSpiTransfers.Text = $"Počet přenosů (CS aktivní): {transferCount}";
                StatsMosiMiso.Text = $"Bajty MOSI / MISO: {total - misoBytes} / {misoBytes}";
            }

            if (wasManualAnalysis.HasValue) {
                StatsAnalysisMode.Text = $"Režim analýzy: {(wasManualAnalysis.Value ? "Ručně" : "Auto")}";
            } else {
                StatsAnalysisMode.Text = "Režim analýzy: –";
            }

            StatsTotalBytes.Text = $"Celkový počet bajtů: {total}";
            StatsErrors.Text = $"Počet bajtů s chybou: {errors}";
            StatsAvgDuration.Text = $"Průměrná délka bajtu: {avgDurationUs:F1} µs";
        }

        /// <summary>
        /// Preskoci na predchozi nalezeny vysledek
        /// </summary>
        private void PrevResult_Click(object sender, RoutedEventArgs e) {
            searchService.PreviousMatch();
        }

        /// <summary>
        /// Vymaze napovedni text ve vyhledavacim poli pri ziskani fokusu
        /// </summary>
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e) {
            if (SearchBox.Text == "(0xFF/65/A)") {
                SearchBox.Text = "";
                SearchBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
            }
        }

        /// <summary>
        /// Obnovi napovedni text ve vyhledavacim poli pri ztrate fokusu
        /// </summary>
        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) {
            if (string.IsNullOrWhiteSpace(SearchBox.Text)) {
                SearchBox.Text = "(0xFF/65/A)";
                SearchBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
        }

        /// <summary>
        /// Nastavi aktualni analyzator a zaregistruje ho pro vyhledavani
        /// </summary>
        private void SetAnalyzer(IProtocolAnalyzer analyzer) {
            activeAnalyzer = analyzer;

            // Reset filtrovanych dat
            filteredUartBytes = null;
            filteredSpiBytes = null;

            if (analyzer is ISearchableAnalyzer searchable) {
                searchService.SetAnalyzer(searchable);
                searchService.SetUpdateCallback(UpdateAnnotations);

                searchService.SetFilterCallback(() => {
                    if (FilterErrorRadio?.IsChecked == true)
                        return ByteFilterMode.OnlyErrors;
                    else if (FilterNoErrorRadio?.IsChecked == true)
                        return ByteFilterMode.NoErrors;
                    else
                        return ByteFilterMode.All;
                });
            } else {
                searchService.Reset();
            }
        }

        private void ResetStatistics() {
            StatsTotalBytes.Text = "Celkový počet bajtů: –";
            StatsErrors.Text = "Počet bajtů s chybou: –";
            StatsAvgDuration.Text = "Průměrná délka bajtu: –";
            StatsBaudRate.Text = "Odhadovaná rychlost (baud): –";
            StatsMinMaxDuration.Text = "Délka bajtu (min/max): –";
            StatsSpiTransfers.Text = "Počet SPI přenosů (CS aktivní): –";
            StatsMosiMiso.Text = "Bajty MOSI / MISO: –";
            StatsAnalysisMode.Text = "Režim analýzy: –"; 
            wasManualAnalysis = null;
        }

    }
}