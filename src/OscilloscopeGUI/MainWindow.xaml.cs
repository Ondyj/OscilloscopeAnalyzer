using System.Windows;
using OscilloscopeCLI.Signal;
using System.Windows.Controls;
using OscilloscopeCLI.Protocols;
using OscilloscopeGUI.Plotting;
using OscilloscopeGUI.Services;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Media;

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
        private Dictionary<string, string>? uartChannelRenameMap = null; // mapovani puvodnich nazvu kanalu na popisne nazvy pro UART
        private SpiChannelMapping? lastUsedSpiMapping = null; // naposledy pouzite mapovani SPI kanalu
        private string? loadedFilePath = null; // cesta k nactenemu CSV souboru     

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

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
            ResetState();

            // 1. Vyber CSV souboru
            var filePick = fileLoadingService.PromptForFileOnly(this);
            if (!filePick.Success)
                return;

            string selectedFilePath = filePick.FilePath!;

            // 2. Vyber protokolu
            var protocolDialog = new ProtocolSelectDialog(4); // napevno CH0–CH3
            protocolDialog.Owner = this;
            if (protocolDialog.ShowDialog() != true)
                return;

            string selectedProtocol = protocolDialog.SelectedProtocol;

            // 3. Predbezne mapovani SPI (pokud vybran SPI)
            if (selectedProtocol == "SPI") {
                var spiMapDialog = new SpiChannelMappingDialog(new List<string> { "CH0", "CH1", "CH2", "CH3" });
                spiMapDialog.Owner = this;
                if (spiMapDialog.ShowDialog() != true)
                    return;

                lastUsedSpiMapping = spiMapDialog.Mapping;
            }

            // 4. Nacteni CSV
            var loadResult = await fileLoadingService.LoadFromFilePathAsync(loader, selectedFilePath, this);
            if (!loadResult.Success)
                return;

            loadedFilePath = selectedFilePath;

            // 5. Vykresleni
            var progressDialog = new ProgressDialog();
            progressDialog.Owner = this;
            progressDialog.Show();
            progressDialog.SetTitle("Vykreslování...");
            progressDialog.SetPhase("Vykreslování");
            progressDialog.ReportMessage("Vykreslování signálu...");
            var progress = new Progress<int>(value => progressDialog.ReportProgress(value));
            await plotter.PlotSignalsAsync(loader.SignalData, progress);
            navService.ResetView(plotter.EarliestTime);
            progressDialog.Finish("Vykreslování dokončeno.", autoClose: true);
            progressDialog.OnOkClicked = () => progressDialog.Close();

            // 6. Prejmenovani kanalu podle mapy
            if (selectedProtocol == "SPI" && lastUsedSpiMapping != null) {
                var renameMap = new Dictionary<string, string> {
                    { lastUsedSpiMapping.ChipSelect, "CS" },
                    { lastUsedSpiMapping.Clock, "SCLK" },
                    { lastUsedSpiMapping.Mosi, "MOSI" }
                };
                if (!string.IsNullOrEmpty(lastUsedSpiMapping.Miso))
                    renameMap[lastUsedSpiMapping.Miso] = "MISO";

                plotter.RenameChannels(renameMap);
            }

            // 7. Mapovani UART kanalu
            if (selectedProtocol == "UART") {
                var uartMapDialog = new UartChannelMappingDialog(loader.SignalData.Keys.ToList());
                uartMapDialog.Owner = this;
                if (uartMapDialog.ShowDialog() != true)
                    return;

                uartChannelRenameMap = uartMapDialog.ChannelRenames;
                plotter.RenameChannels(uartChannelRenameMap);
            }
        }


        /// <summary>
        /// Spusti analyzu signalu podle zvoleneho protokolu
        /// </summary>
        private void AnalyzeButton_Click(object sender, RoutedEventArgs e) {
            string selectedProtocol = (ProtocolComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            bool isManual = ManualRadio.IsChecked == true;

            if (!CheckChannelCount(selectedProtocol, loader.SignalData.Count))
                return;

            var analyzer = protocolAnalysisService.Analyze(
                selectedProtocol,
                isManual,
                loader,
                uartChannelRenameMap,
                ref lastUsedSpiMapping,
                this
            );

            if (analyzer != null) {
                analyzer.Analyze();
                SetAnalyzer(analyzer);
                MessageBox.Show($"{analyzer.ProtocolName} analýza dokončena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Overi, zda je pro dany protokol k dispozici dostatek kanalu
        /// </summary>
        private bool CheckChannelCount(string protocol, int channelCount) {
            int requiredChannels = protocol switch {
                "UART" => 1,
                "SPI" => 3,
                _ => 0
            };

            if (requiredChannels == 0) {
                return true;
            }

            if (channelCount < requiredChannels) {
                MessageBox.Show($"Pro analýzu {protocol} je potřeba alespoň {requiredChannels} kanály.", 
                    "Nedostatečný počet kanálů", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
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

            ResultNavigationPanel.Visibility = Visibility.Collapsed;
            ResultInfo.Text = "";

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
            if (SearchBox.Text == "např. FF") {
                SearchBox.Text = "";
                SearchBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Black);
            }
        }

        /// <summary>
        /// Obnovi napovedni text ve vyhledavacim poli pri ztrate fokusu
        /// </summary>
        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) {
            if (string.IsNullOrWhiteSpace(SearchBox.Text)) {
                SearchBox.Text = "např. FF";
                SearchBox.Foreground = new SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
        }

        /// <summary>
        /// Nastavi aktualni analyzator a zaregistruje ho pro vyhledavani
        /// </summary>
        private void SetAnalyzer(IProtocolAnalyzer analyzer) {
            activeAnalyzer = analyzer;
            if (analyzer is ISearchableAnalyzer searchable)
                searchService.SetAnalyzer(searchable);
            else
                searchService.Reset();
        }
    }
}