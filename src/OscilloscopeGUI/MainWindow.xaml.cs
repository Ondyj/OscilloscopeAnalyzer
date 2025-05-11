using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScottPlot;
using OscilloscopeCLI.Signal;
using OscilloscopeCLI.Protocols;
using OscilloscopeGUI.Plotting;
using OscilloscopeGUI.Services;

namespace OscilloscopeGUI {
    public partial class MainWindow : Window {
      
        // === Services ===
        private readonly SignalLoader loader = new();
        private readonly FileLoadingService fileLoadingService = new();
        private readonly ProtocolAnalysisService protocolAnalysisService = new();
        private readonly ExportService exportService = new();
        private SignalPlotter plotter = null!;
        private PlotNavigationService navService = null!;
        private SearchService searchService = null!;
        private StatisticsService statisticsService = null!;

        // === Stav aplikace ===
        private IProtocolAnalyzer? activeAnalyzer;
        private SpiChannelMapping? lastUsedSpiMapping = null;
        private UartChannelMapping? lastUsedUartMapping = null;
        private string? loadedFilePath = null;
        private bool isDragging = false;
        private Point lastMousePosition;

        // === Nastaveni zobrazeni ===
        private ByteDisplayFormat currentFormat = ByteDisplayFormat.Hex;
        private bool? wasManualAnalysis = null;

        // === Vyfiltrovana data ===
        private List<UartDecodedByte>? filteredUartBytes = null;
        private List<SpiDecodedByte>? filteredSpiBytes = null;

        // === Anotace v grafu ===
        private readonly List<ScottPlot.Plottables.Text> byteLabels = new();
        private readonly List<IPlottable> byteStartLines = new();
        private readonly AnnotationRendererManager annotationRendererManager = new();

        // === Casove znacky ===
        private double? timeMark1 = null;
        private double? timeMark2 = null;
        private ScottPlot.Plottables.VerticalLine? line1 = null;
        private ScottPlot.Plottables.VerticalLine? line2 = null;

        /// <summary>
        /// Konstruktor hlavniho okna aplikace
        /// </summary>
        public MainWindow() {
            InitializeComponent();
            InitializePlot();
            InitializeEvents();

            statisticsService = new StatisticsService(
                StatsTotalBytes,
                StatsErrors,
                StatsAvgDuration,
                StatsBaudRate,
                StatsMinMaxDuration,
                StatsSpiTransfers,
                StatsMosiMiso,
                StatsAnalysisMode
            );
        }

        /// <summary>
        /// Inicializace plotteru, navigace a vyhledavani
        /// </summary>
        private void InitializePlot() {
            plot.UserInputProcessor.Disable();

            plotter = new SignalPlotter(plot);
            navService = new PlotNavigationService(plot);
            searchService = new SearchService(plot, navService);
            searchService.AttachUi(ResultInfo, ResultNavigationPanel);
        }

        /// <summary>
        /// Pripoji udalosti pro klavesnici a mys
        /// </summary>
        private void InitializeEvents() {
            this.KeyDown += MainWindow_KeyDown;
            this.MouseWheel += MainWindow_MouseWheel;
            this.KeyUp += (s, e) => UpdateAnnotations();

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
                UpdateAnnotations();
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
            plot.MouseRightButtonUp += (s, e) => UpdateAnnotations();
            plot.MouseWheel += (s, e) => UpdateAnnotations();
        }

        /// <summary>
        /// Aktualizuje anotace v grafu podle aktualne vybraneho analyzatoru
        /// </summary>
        private void UpdateAnnotations() {
            // Smazat predchozi anotace
            foreach (var label in byteLabels)
                plot.Plot.Remove(label);
            byteLabels.Clear();

            foreach (var line in byteStartLines)
                plot.Plot.Remove(line);
            byteStartLines.Clear();

            if (activeAnalyzer is null)
                return;

            // Vybrat renderer podle typu analyzatoru
            var renderer = annotationRendererManager.GetRendererFor(activeAnalyzer);
            renderer?.Render(activeAnalyzer, plot.Plot, currentFormat, byteLabels, byteStartLines);

            plot.Refresh();
        }
  
        /// <summary>
        /// Reaguje na zmenu formatu bajtu (HEX/DEC/ASCII) a aktualizuje anotace v grafu
        /// </summary>
        /// <param name="sender">Odesilatel udalosti (radio button)</param>
        /// <param name="e">Argument udalosti</param>
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

        // === Vstupni udalosti (klavesnice, mys, vyhledavani) ===

        /// <summary>
        /// Reaguje na stisk klavesy – navigace vysledku a posun grafu
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
        /// Reaguje na kolecko mysi – zoomovani v grafu
        /// </summary>
        private void MainWindow_MouseWheel(object sender, MouseWheelEventArgs e) {
            navService.HandleMouseWheel(e);
        }

        /// <summary>
        /// Reaguje na stisk Enter ve vyhledavacim poli
        /// </summary>
        private void SearchBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                SearchButton_Click(sender, e);
                plot.Focus();
            }
        }

        /// <summary>
        /// Vymaze napovedu ve vyhledavacim poli pri ziskani fokusu
        /// </summary>
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e) {
            if (SearchBox.Text == "(0xFF/65/A)") {
                SearchBox.Text = "";
                SearchBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
            }
        }

        /// <summary>
        /// Obnovi napovedu ve vyhledavacim poli pri ztrate fokusu
        /// </summary>
        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) {
            if (string.IsNullOrWhiteSpace(SearchBox.Text)) {
                SearchBox.Text = "(0xFF/65/A)";
                SearchBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
        }

        /// <summary>
        /// Provede export vysledku analyzy do CSV souboru
        /// </summary>
        private void ExportResultsButton_Click(object sender, RoutedEventArgs e) {
            exportService.Export(activeAnalyzer, loadedFilePath);
        }

        /// <summary>
        /// Nacte CSV soubor, vykresli signal a provede mapovani kanalu podle zvoleneho protokolu
        /// </summary>
        private async void LoadCsv_Click(object sender, RoutedEventArgs e) {

            var result = await fileLoadingService.LoadAndMapAsync(loader, this);
            if (!result.Success)
                return;

            ResetState();

            loadedFilePath = result.FilePath;
            lastUsedUartMapping = result.UartMapping;
            lastUsedSpiMapping = result.SpiMapping;

            // připrav data pro vykreslení
            var finalData = result.RenameMap.Count > 0
                ? loader.SignalData.ToDictionary(
                    kvp => result.RenameMap.TryGetValue(kvp.Key, out var newName) ? newName : kvp.Key,
                    kvp => kvp.Value)
                : loader.SignalData;

            // vykresli
            var progressDialog = new ProgressDialog {
                Owner = this
            };
            progressDialog.Show();
            progressDialog.SetTitle("Vykreslování...");
            progressDialog.SetPhase("Vykreslování");
            progressDialog.ReportMessage("Vykreslování signálu...");

            var progress = new Progress<int>(value => progressDialog.ReportProgress(value));
            await plotter.PlotSignalsAsync(finalData, progress);

            double minTime = finalData.Values.SelectMany(list => list.Select(p => p.Time)).Min();
            double maxTime = finalData.Values.SelectMany(list => list.Select(p => p.Time)).Max();
            navService.SetZoomOutLimitBasedOnDuration(maxTime - minTime);
            navService.ResetView(plotter.EarliestTime);

            progressDialog.Finish("Vykreslování dokončeno.", autoClose: true);
            progressDialog.OnOkClicked = () => progressDialog.Close();
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

            // 3 nebo vice kanalu: jen SPI
            if (channelCount >= 3 && protocol != "SPI") {
                MessageBox.Show(
                    "Při třech a více kanálech lze dekódovat pouze SPI.",
                    "Neplatná konfigurace",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // 2 kanzly: UART i SPI OK, ale zkontrolujeme minimalni poyadavek
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

        // === Vyhledavani a filtry ===

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
        /// Aplikuje filtr na dekodovana data podle chyby (UART nebo SPI). Obnovi hledani a anotace
        /// </summary>
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

            searchService.RefreshSearch();
            UpdateAnnotations();
        }

        /// <summary>
        /// Reaguje na zaskrtnuti radio buttonu filtru. Nastavi typ filtru a aplikuje ho
        /// </summary>
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

        /// <summary>
        /// Preskoci na predchozi nalezeny vysledek
        /// </summary>
        private void PrevResult_Click(object sender, RoutedEventArgs e) {
            searchService.PreviousMatch();
        }

        // === Casove znacky ===

        /// <summary>
        /// Reaguje na prave kliknuti do grafu – nastavi casove znacky pro mereni
        /// </summary>
        private void Plot_MouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            var source = PresentationSource.FromVisual(this);
            double dpiX = 1.0, dpiY = 1.0;
            if (source != null) {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            Point mousePos = e.GetPosition(plot);
            double adjustedX = mousePos.X * dpiX;
            double adjustedY = mousePos.Y * dpiY;

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

            statisticsService.Reset();

            plot.Plot.Clear();
            plot.Refresh();
        }

        // === Statistiky ===

        /// <summary>
        /// Aktualizuje statistiky podle aktualniho analyzatoru
        /// </summary>
        private void UpdateStatistics() {
            if (activeAnalyzer is UartProtocolAnalyzer uart) {
                statisticsService.UpdateUartStats(uart, filteredUartBytes);
            }
            else if (activeAnalyzer is SpiProtocolAnalyzer spi) {
                statisticsService.UpdateSpiStats(spi, filteredSpiBytes);
            }

            if (wasManualAnalysis.HasValue) {
                StatsAnalysisMode.Text = $"Režim analýzy: {(wasManualAnalysis.Value ? "Ručně" : "Auto")}";
            } else {
                StatsAnalysisMode.Text = "Režim analýzy: –";
            }
        }
    }
}