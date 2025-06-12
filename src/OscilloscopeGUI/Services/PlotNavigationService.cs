using ScottPlot;
using ScottPlot.WPF;
using System.Windows.Input;

namespace OscilloscopeGUI.Services {
    /// <summary>
    /// Trida pro ovladani zoomovani a navigace v grafu
    /// </summary>
    public class PlotNavigationService {
        private readonly WpfPlot plot;
        private AxisLimits? baseLimits = null;
        private bool isZoomedIn = false;
        private double? baseXRange = null;

        // Fixni maximalni faktor pro oddaleni
        private double maxZoomOutFactor = 1;

        public PlotNavigationService(WpfPlot plotControl) {
            plot = plotControl;
        }

        /// <summary>
        /// Zpracuje klavesovou udalost pro zoomovani a posun.
        /// W nebo Sipka nahoru = priblizeni, S nebo Sipka dolu = oddaleni, A = posun doleva, D = posun doprava.
        /// </summary>
        public void HandleKey(Key key) {
            var xAxis = plot.Plot.Axes.Bottom;
            var yAxis = plot.Plot.Axes.Left;

            double zoomFactor = 0.1;
            double panFactor = 1;
            double rangeX = xAxis.Max - xAxis.Min;
            double shiftX = rangeX * zoomFactor;
            double panX = rangeX * panFactor;

            double minY = yAxis.Min;
            double maxY = yAxis.Max;

            if (key == Key.W || key == Key.Up) {
                // Priblizeni
                xAxis.Min += shiftX;
                xAxis.Max -= shiftX;
            } else if (key == Key.S || key == Key.Down) {
                // Oddaleni (ale ne pres limit)
                double newRange = rangeX + 2 * shiftX;
                double baseRange = baseXRange ?? rangeX;
                double maxAllowedRange = baseRange * maxZoomOutFactor;

                if (newRange <= maxAllowedRange) {
                    xAxis.Min -= shiftX;
                    xAxis.Max += shiftX;
                }
            } else if (key == Key.A) {
                // Posun doleva
                xAxis.Min -= panX;
                xAxis.Max -= panX;
            } else if (key == Key.D) {
                // Posun doprava
                xAxis.Min += panX;
                xAxis.Max += panX;
            }

            // Zamknuti Y osy (nezoomovat vertikalne)
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
        /// Resetuje pohled na graf a zarovna zacatek signalu doleva
        /// </summary>
        public void ResetView(double signalStartTime) {
            plot.Plot.Axes.AutoScale();
            var limits = plot.Plot.Axes.GetLimits();

            double baseRange = limits.XRange.Max - limits.XRange.Min;

            double zoomedRange = baseRange * maxZoomOutFactor;

            baseLimits = limits;
            baseXRange = baseRange;

            // Zarovnani zacatku signalu doleva
            double newXMin = signalStartTime;
            double newXMax = signalStartTime + zoomedRange;

            plot.Plot.Axes.SetLimitsX(newXMin, newXMax);
            plot.Refresh();

            baseLimits = plot.Plot.Axes.GetLimits();
            isZoomedIn = false;
        }

        /// <summary>
        /// Posune graf horizontalne podle rozdilu souradnic X v pixelech
        /// </summary>
        public void PanByPixelDelta(double deltaX) {
            var xAxis = plot.Plot.Axes.Bottom;

            double rangeX = xAxis.Max - xAxis.Min;
            double widthPx = plot.ActualWidth;

            if (widthPx <= 0) return;

            double deltaUnits = (deltaX / widthPx) * rangeX;
            xAxis.Min -= deltaUnits;
            xAxis.Max -= deltaUnits;

            plot.Refresh();
        }

        /// <summary>
        /// Priblizi graf na dane X souradnici a nastavi ji do stredu obrazovky
        /// </summary>
        public void CenterOn(double xValue) {
            var plt = plot.Plot;
            var limits = baseLimits ?? plt.Axes.GetLimits();

            double originalRange = limits.XRange.Max - limits.XRange.Min;
            if (isZoomedIn)
                return;

            double baseRange = baseXRange ?? originalRange;
            double range = baseRange / maxZoomOutFactor;

            double newXMin = xValue - range / 2;
            double newXMax = xValue + range / 2;

            plt.Axes.SetLimitsX(newXMin, newXMax);
            plot.Refresh();

            isZoomedIn = true;
        }

        /// <summary>
        /// Posune graf tak, aby xValue bylo ve stredu bez zmeny zoomu
        /// </summary>
        public void MoveTo(double xCenter) {
            var xAxis = plot.Plot.Axes.Bottom;

            double rangeX = xAxis.Max - xAxis.Min;
            double halfRange = rangeX / 2;

            xAxis.Min = xCenter - halfRange;
            xAxis.Max = xCenter + halfRange;

            plot.Refresh();
        }

        public void SetZoomLimitByFileSize(long fileSizeBytes) {
            if (fileSizeBytes > 20_000_000) {
                maxZoomOutFactor = 0.0001;
            } else if (fileSizeBytes > 1_000_000) {
                maxZoomOutFactor = 0.03;
            } else {
                maxZoomOutFactor = 1.0;
            }
        }
    }
}
