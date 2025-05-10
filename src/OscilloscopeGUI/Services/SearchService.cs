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
        private byte? searchedValue = null;
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
        /// Spusti vyhledavani hodnoty v hex formatu v dekodovanych datech.
        /// </summary>
        public void Search(string query) {
            if (analyzer == null) return;

            string[] parts = query.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            List<byte> byteList = new();

            foreach (string part in parts) {
                string trimmed = part.Trim();

                if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                    if (byte.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                        byteList.Add(b);
                    else {
                        ShowInvalidInput(trimmed);
                        return;
                    }
                }
                else if (trimmed.Length == 1 && !char.IsDigit(trimmed[0])) {
                    byteList.Add((byte)trimmed[0]);
                }
                else if (byte.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b2)) {
                    byteList.Add(b2);
                }
                else {
                    ShowInvalidInput(trimmed);
                    return;
                }
            }

            if (byteList.Count == 0) {
                ShowInvalidInput(query);
                return;
            }

            var sequence = byteList.ToArray();

            searchedSequence = sequence;
            var mode = getFilterModeCallback?.Invoke() ?? ByteFilterMode.All;
            analyzer.Search(sequence, mode);

            if (analyzer.HasMatches()) {
                currentMatchIndex = 0;
                ShowMatch();
                if (navigationPanel != null) navigationPanel.Visibility = Visibility.Visible;
            }
            else {
                MessageBox.Show($"Sekvence nebyla nalezena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (analyzer == null || searchedSequence == null || searchedSequence.Length == 0)
                return;

            var mode = getFilterModeCallback?.Invoke() ?? ByteFilterMode.All;
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
            if (analyzer == null || analyzer.MatchCount == 0) {
                MessageBox.Show("Žádná další nalezená hodnota.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            currentMatchIndex = (currentMatchIndex + 1) % analyzer.MatchCount;
            ShowMatch();
        }
        /// <summary>
        /// Presune se na predchozi vysledek vyhledavani.
        /// </summary>
        public void PreviousMatch() {
            if (analyzer == null || analyzer.MatchCount == 0) {
                MessageBox.Show("Žádná předchozí nalezená hodnota.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
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
            searchedValue = null;
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