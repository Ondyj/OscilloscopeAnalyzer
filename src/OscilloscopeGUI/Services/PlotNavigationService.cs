using ScottPlot;
using ScottPlot.WPF;
using System.Windows.Input;

namespace OscilloscopeGUI.Services {
    /// <summary>
    /// Trida pro ovladani zoomovani a navigace v grafu
    /// </summary>
    public class PlotNavigationService {
        private readonly WpfPlot plot;

        public PlotNavigationService(WpfPlot plotControl) {
            plot = plotControl;
        }

        /// <summary>
        /// Zpracuje klavesovou udalost pro zoomovani
        /// </summary>
        public void HandleKey(Key key) {
            var xAxis = plot.Plot.Axes.Bottom;
            var yAxis = plot.Plot.Axes.Left;

            double zoomFactor = 0.1;
            double rangeX = xAxis.Max - xAxis.Min;
            double shiftX = rangeX * zoomFactor;

            double minY = yAxis.Min;
            double maxY = yAxis.Max;

            if (key == Key.W) {
                xAxis.Min += shiftX;
                xAxis.Max -= shiftX;
            } else if (key == Key.S) {
                xAxis.Min -= shiftX;
                xAxis.Max += shiftX;
            }

            yAxis.Min = minY;
            yAxis.Max = maxY;

            plot.Refresh();
        }
    }
}
