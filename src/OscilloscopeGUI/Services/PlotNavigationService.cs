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
        /// Zpracuje klavesovou udalost pro zoomovani a posun
        /// </summary>
        public void HandleKey(Key key) {
            var xAxis = plot.Plot.Axes.Bottom;
            var yAxis = plot.Plot.Axes.Left;

            double zoomFactor = 0.1;
            double panFactor = 0.1;
            double rangeX = xAxis.Max - xAxis.Min;
            double shiftX = rangeX * zoomFactor;
            double panX = rangeX * panFactor;

            double minY = yAxis.Min;
            double maxY = yAxis.Max;

            if (key == Key.W) {
                // Klavesa W = zoom in (priblizeni)
                xAxis.Min += shiftX;
                xAxis.Max -= shiftX;
            } else if (key == Key.S) {
                // Klavesa S = zoom out (oddaleni)
                xAxis.Min -= shiftX;
                xAxis.Max += shiftX;
            } else if (key == Key.A) {
                // Klavesa A = posun doleva
                xAxis.Min -= panX;
                xAxis.Max -= panX;
            } else if (key == Key.D) {
                // Klavesa D = posun doprava
                xAxis.Min += panX;
                xAxis.Max += panX;
            }

            yAxis.Min = minY;
            yAxis.Max = maxY;

            plot.Refresh();
        }

        /// <summary>
        /// Zpracuje pohyb kolecka mysi pro simulaci klaves W/S
        /// </summary>
        public void HandleMouseWheel(MouseWheelEventArgs e) {
            if (e.Delta > 0) {
                HandleKey(Key.W); // Scroll nahoru = zoom in
            } else if (e.Delta < 0) {
                HandleKey(Key.S); // Scroll dolu = zoom out
            }
        }

        /// <summary>
        /// Resetuje pohled na graf do vychoziho stavu (jako po AutoScale)
        /// </summary>
        public void ResetView() {
            plot.Plot.Axes.AutoScale();
            plot.Refresh();
        }

        /// <summary>
        /// Posune graf horizontálně podle rozdilu souradnic X v pixelech
        /// </summary>
        public void PanByPixelDelta(double deltaX) {
            var xAxis = plot.Plot.Axes.Bottom;

            double rangeX = xAxis.Max - xAxis.Min;

            // Ziskame aktualni sirku oblasti grafu v pixelech
            double widthPx = plot.ActualWidth;

            if (widthPx <= 0) return;

            double deltaUnits = (deltaX / widthPx) * rangeX;

            xAxis.Min -= deltaUnits;
            xAxis.Max -= deltaUnits;

            plot.Refresh();
        }
    }
}
