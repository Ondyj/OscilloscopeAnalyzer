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
        private double maxZoomOutFactor = 10;
    
        public PlotNavigationService(WpfPlot plotControl) {
            plot = plotControl;
        }

        // <summary>
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
                double newRange = rangeX + 2 * shiftX;

                double baseRange = baseXRange ?? rangeX;
                double maxAllowedRange = baseRange * maxZoomOutFactor;

                if (newRange <= maxAllowedRange) {
                    xAxis.Min -= shiftX;
                    xAxis.Max += shiftX;
                }
                // jinak neprovadet dalsi oddaleni
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
        /// Resetuje pohled na graf a zarovna zacatek signalu do stredu
        /// </summary>
        public void ResetView(double signalStartTime) {
            plot.Plot.Axes.AutoScale();
            var limits = plot.Plot.Axes.GetLimits();

            double originalRange = limits.XRange.Max - limits.XRange.Min;
            double zoomFactor = 300;
            double zoomedRange = originalRange / zoomFactor;

            double newXMin = signalStartTime - zoomedRange / 2;
            double newXMax = signalStartTime + zoomedRange / 2;

            baseLimits = plot.Plot.Axes.GetLimits();
            baseXRange = baseLimits?.XRange.Max - baseLimits?.XRange.Min;

            plot.Plot.Axes.SetLimitsX(newXMin, newXMax);
            plot.Refresh();

            baseLimits = plot.Plot.Axes.GetLimits();
            isZoomedIn = false;
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
        /// <summary>
        /// Priblizi graf na dane X souradnici a nastavi ji do stredu obrazovky.
        /// Respektuje maximalni priblizeni podle maxZoomOutFactor.
        /// </summary>
        public void CenterOn(double xValue) {
            var plt = plot.Plot;

            // Pouzij ulozene limity nebo aktualni
            var limits = baseLimits ?? plt.Axes.GetLimits();

            double originalXMin = limits.XRange.Min;
            double originalXMax = limits.XRange.Max;
            double originalRange = originalXMax - originalXMin;

            if (isZoomedIn)
                return;

            // Minimalni rozsah = base rozsah / maxZoomOutFactor
            double baseRange = baseXRange ?? originalRange;
            double minRange = baseRange / (maxZoomOutFactor > 0 ? maxZoomOutFactor : 1);

            // Vypocet rozsahu s dynamickym zoomem (ale zaroven omezenim)
            double zoomFactor;
            if (originalRange < 10)
                zoomFactor = 1000;
            else if (originalRange < 21)
                zoomFactor = 10000;
            else
                zoomFactor = 1000000;

            double range = originalRange / zoomFactor;

            // Ujistime se, ze rozsah nebude mensi nez minRange
            if (range < minRange)
                range = minRange;

            double newXMin = xValue - range / 2;
            double newXMax = xValue + range / 2;

            plt.Axes.SetLimitsX(newXMin, newXMax);
            plot.Refresh();

            isZoomedIn = true;
        }

        public void SetZoomOutLimitBasedOnDuration(double durationSeconds) {
            // Dynamicke urceni faktoru podle delky signalu
            if (durationSeconds < 10)
                maxZoomOutFactor = 20;
            else
                maxZoomOutFactor = 0.0001;
        }

        /// <summary>
        /// Plynule posune graf tak, aby xValue bylo ve stredu grafu (beze zmeny zoomu)
        /// </summary>
        public void MoveTo(double xCenter) {
            var xAxis = plot.Plot.Axes.Bottom;

            double rangeX = xAxis.Max - xAxis.Min;
            double halfRange = rangeX / 2;

            xAxis.Min = xCenter - halfRange;
            xAxis.Max = xCenter + halfRange;

            plot.Refresh();
        }

    }
}
