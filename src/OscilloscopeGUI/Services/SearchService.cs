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
        public void Search(string queryHex) {
            if (analyzer == null) return;

            if (!byte.TryParse(queryHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value)) {
                MessageBox.Show("Neplatný hexadecimální vstup. Zadejte např. 'A5'", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            searchedValue = value;
            analyzer.Search(value);

            if (analyzer.HasMatches()) {
                currentMatchIndex = 0;
                ShowMatch();
                if (navigationPanel != null) navigationPanel.Visibility = Visibility.Visible;
            } else {
                MessageBox.Show($"Hodnota 0x{value:X2} nebyla nalezena.", "Výsledek", MessageBoxButton.OK, MessageBoxImage.Information);
                if (navigationPanel != null) navigationPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Zobrazi aktualni nalezenou hodnotu v grafu a posune se na ni.
        /// </summary>
        public void ShowMatch() {
            if (analyzer == null || searchedValue == null || !analyzer.HasMatches())
                return;

            double timestamp = analyzer.GetMatchTimestamp(currentMatchIndex);
            string label = analyzer.GetMatchDisplay(currentMatchIndex);

            resultInfoTextBlock?.Dispatcher.Invoke(() => resultInfoTextBlock.Text = label);
            navService.MoveTo(timestamp);

            if (matchLine == null) {
                matchLine = plot.Plot.Add.VerticalLine(timestamp);
                matchLine.Color = new ScottPlot.Color(255, 0, 0, 128); // transparent red
                matchLine.LineWidth = 2;
            } else {
                matchLine.X = timestamp;
            }

            plot.Refresh();
        }

        /// <summary>
        /// Presune se na dalsi vysledek vyhledavani.
        /// </summary>
        public void NextMatch() {
            if (analyzer == null || analyzer.MatchCount == 0)
                return;

            currentMatchIndex = (currentMatchIndex + 1) % analyzer.MatchCount;
            ShowMatch();
        }
        /// <summary>
        /// Presune se na predchozi vysledek vyhledavani.
        /// </summary>
        public void PreviousMatch() {
            if (analyzer == null || analyzer.MatchCount == 0)
                return;

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
            if (navigationPanel != null) navigationPanel.Visibility = Visibility.Collapsed;

            if (matchLine != null) {
                plot.Plot.Remove(matchLine);
                matchLine = null;
                plot.Refresh();
            }
        }
    }
}