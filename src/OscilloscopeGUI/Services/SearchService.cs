using System.Windows;
using ScottPlot.WPF;
using System.Windows.Controls;
using OscilloscopeCLI.Protocols;
using System.Globalization;

namespace OscilloscopeGUI.Services {
    public class SearchService {
        private readonly WpfPlot plot;
        private readonly PlotNavigationService navService;
        private ISearchableAnalyzer? analyzer;
        private int currentMatchIndex = 0;
        private ScottPlot.Plottables.VerticalLine? matchLine = null;
        private TextBlock? resultInfoTextBlock;
        private UIElement? navigationPanel;
        private Action? updateAnnotationsCallback;
        private Func<ByteFilterMode>? getFilterModeCallback;
        private byte[]? searchedSequence = null;

        /// <summary>
        /// Inicializuje novou instanci tridy SearchService s odkazem na graf a navigaci.
        /// </summary>
        public SearchService(WpfPlot plot, PlotNavigationService navService) {
            this.plot = plot;
            this.navService = navService;
        }

        /// <summary>
        /// Nastavi analyzer, ve kterem se bude vyhledavat, a resetuje stav.
        /// </summary>
        public void SetAnalyzer(ISearchableAnalyzer analyzer) {
            this.analyzer = analyzer;
            Reset();
        }

        /// <summary>
        /// Pripoji UI prvky pro zobrazeni vysledku a navigacni panel.
        /// </summary>
        public void AttachUi(TextBlock resultInfo, UIElement navPanel) {
            this.resultInfoTextBlock = resultInfo;
            this.navigationPanel = navPanel;
        }

        /// <summary>
        /// Spusti vyhledavani hodnoty v dekodovanych datech.
        /// </summary>
        public void Search(string query) {
            if (analyzer == null)
                return;

            string trimmedQuery = query.Trim();

            // === hledani vseho ===
            if (string.IsNullOrEmpty(trimmedQuery) || trimmedQuery == "(0xFF/65/A)") {
                searchedSequence = null;
                var mode = getFilterModeCallback?.Invoke() ?? ByteFilterMode.All;
                analyzer.Search(Array.Empty<byte>(), mode); // prazdna sekvence = hledat vse

                if (analyzer.HasMatches()) {
                    currentMatchIndex = 0;
                    ShowMatch();
                    if (navigationPanel != null)
                        navigationPanel.Visibility = Visibility.Visible;
                } else {
                    MessageBox.Show("Pro zvolený filtr nebyly nalezeny žádné hodnoty.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return;
            }

            // === Původní implementace ===
            string[] parts = trimmedQuery.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            List<byte> byteList = new();

            foreach (string part in parts) {
                string token = part.Trim();

                if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                    if (byte.TryParse(token.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                        byteList.Add(b);
                    else {
                        ShowInvalidInput(token);
                        return;
                    }
                }
                else if (token.Length == 1 && !char.IsDigit(token[0])) {
                    byteList.Add((byte)token[0]);
                }
                else if (byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b2)) {
                    byteList.Add(b2);
                }
                else {
                    ShowInvalidInput(token);
                    return;
                }
            }

            if (byteList.Count == 0) {
                ShowInvalidInput(query);
                return;
            }

            searchedSequence = byteList.ToArray();
            var filterMode = getFilterModeCallback?.Invoke() ?? ByteFilterMode.All;
            analyzer.Search(searchedSequence, filterMode);

            if (analyzer.HasMatches()) {
                currentMatchIndex = 0;
                ShowMatch();
                if (navigationPanel != null)
                    navigationPanel.Visibility = Visibility.Visible;
            } else {
                MessageBox.Show($"Sekvence nebyla nalezena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
        }

        private void ShowInvalidInput(string input) {
            MessageBox.Show($"Zadaný vstup „{input}“ není platný.\n\nPoužijte jeden z následujících formátů:\n- 0xFF (HEX)\n- 65 (DEC)\n- A (ASCII znak)", "Neplatný vstup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private string FormatByte(byte b) {
            string ascii = (b >= 32 && b <= 126) ? ((char)b).ToString() : $"\\x{b:X2}";
            return $"0x{b:X2} | {b} | {ascii}";
        }


        /// <summary>
        /// Zobrazi aktualni nalezenou hodnotu v grafu a posune se na ni.
        /// </summary>
        public void ShowMatch() {
            if (analyzer == null || analyzer.MatchCount == 0)
                return;

            var mode = getFilterModeCallback?.Invoke() ?? ByteFilterMode.All;

            if (searchedSequence == null)
                analyzer.Search(Array.Empty<byte>(), mode);
            else
                analyzer.Search(searchedSequence, mode);

            if (!analyzer.HasMatches()) {
                MessageBox.Show(
                    $"Sekvence nebyla nalezena pro aktuální filtr.",
                    "Žádné výsledky",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            // Ochrana: pokud currentMatchIndex je mimo rozsah
            if (currentMatchIndex >= analyzer.MatchCount)
                currentMatchIndex = 0;

            double timestamp = analyzer.GetMatchTimestamp(currentMatchIndex);
            string label = analyzer.GetMatchDisplay(currentMatchIndex);

            resultInfoTextBlock?.Dispatcher.Invoke(() => resultInfoTextBlock.Text = label);
            navService.MoveTo(timestamp);

            if (matchLine == null) {
                matchLine = plot.Plot.Add.VerticalLine(timestamp);
                matchLine.Color = new ScottPlot.Color(255, 0, 0, 128);
                matchLine.LineWidth = 2;
            } else {
                matchLine.X = timestamp;
            }

            updateAnnotationsCallback?.Invoke();
            plot.Refresh();
        }
        /// <summary>
        /// Presune se na dalsi vysledek vyhledavani.
        /// </summary>
        public void NextMatch() {
            if (analyzer == null)
                return;

            var mode = getFilterModeCallback?.Invoke() ?? ByteFilterMode.All;
            analyzer.Search(searchedSequence ?? Array.Empty<byte>(), mode);

            if (!analyzer.HasMatches()) {
                MessageBox.Show("Nenalezena žádná odpovídající hodnota.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            currentMatchIndex = (currentMatchIndex + 1) % analyzer.MatchCount;
            ShowMatch();
        }
        /// <summary>
        /// Presune se na predchozi vysledek vyhledavani.
        /// </summary>
        public void PreviousMatch() {
            if (analyzer == null)
                return;

            var mode = getFilterModeCallback?.Invoke() ?? ByteFilterMode.All;
            analyzer.Search(searchedSequence ?? Array.Empty<byte>(), mode);

            if (!analyzer.HasMatches()) {
                MessageBox.Show("Nenalezena žádná odpovídající hodnota.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            currentMatchIndex = (currentMatchIndex - 1 + analyzer.MatchCount) % analyzer.MatchCount;
            ShowMatch();
        }

        /// <summary>
        /// Resetuje stav vyhledavani a odstrani znacky z grafu.
        /// </summary>
        public void Reset() {
            currentMatchIndex = 0;
            resultInfoTextBlock?.Dispatcher.Invoke(() => resultInfoTextBlock.Text = "");


            if (matchLine != null) {
                plot.Plot.Remove(matchLine);
                matchLine = null;
                plot.Refresh();
            }
        }
        public void SetFilterCallback(Func<ByteFilterMode> getFilterMode) {
            getFilterModeCallback = getFilterMode;
        }
        public void SetUpdateCallback(Action updateAnnotations) {
            updateAnnotationsCallback = updateAnnotations;
        }

        public void RefreshSearch() {
            if (searchedSequence != null)
                ShowMatch();
        }
    }
}