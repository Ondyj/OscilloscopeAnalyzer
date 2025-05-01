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
            double panFactor = 0.1;
            double rangeX = xAxis.Max - xAxis.Min;
            double shiftX = rangeX * zoomFactor;
            double panX = rangeX * panFactor;

            double minY = yAxis.Min;
            double maxY = yAxis.Max;

            if (key == Key.W || key == Key.Up) {
                // Klavesa W nebo sipka nahoru = zoom in (priblizeni)
                xAxis.Min += shiftX;
                xAxis.Max -= shiftX;
            } else if (key == Key.S || key == Key.Down) {
                // Klavesa S nebo sipka dolu = zoom out (oddaleni)
                double newRange = rangeX + 2 * shiftX;

                // Vychozi rozsah (pred zoomem)
                double baseRange = (baseLimits?.XRange.Max - baseLimits?.XRange.Min) ?? rangeX;

                // FIKSNI FAKTOR 
                double maxAllowedFactor = 10;
                double maxAllowedRange = baseRange * maxAllowedFactor;

                if (newRange <= maxAllowedRange) {
                    xAxis.Min -= shiftX;
                    xAxis.Max += shiftX;
                }
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
        /// Resetuje pohled na graf a zarovna zacatek signalu do stredu
        /// </summary>
        public void ResetView(double signalStartTime) {
            plot.Plot.Axes.AutoScale();
            var limits = plot.Plot.Axes.GetLimits();

            double originalRange = limits.XRange.Max - limits.XRange.Min;
            double zoomFactor = 30;
            double zoomedRange = originalRange / zoomFactor;

            double newXMin = signalStartTime - zoomedRange / 2;
            double newXMax = signalStartTime + zoomedRange / 2;

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
        /// </summary>
        public void CenterOn(double xValue) {
            var plt = plot.Plot;

            // Pokud nejsou ulozene vychozi limity, vezmeme aktualni
            var limits = baseLimits ?? plt.Axes.GetLimits();

            double originalXMin = limits.XRange.Min;
            double originalXMax = limits.XRange.Max;
            double originalRange = originalXMax - originalXMin;

            if (isZoomedIn)
                return;

            // Dynamicky urceni rozsahu priblizeni podle velikosti puvodniho rozsahu
            double zoomFactor;
            if (originalRange < 10)
                zoomFactor = 1000; // mene priblizit kdyz je signal maly
            else if(originalRange < 21)
                zoomFactor = 10000;
            else
                zoomFactor = 1000000; // velky signal = hodne priblizit

            double range = originalRange / zoomFactor;
            double newXMin = xValue - range / 2;
            double newXMax = xValue + range / 2;

            plt.Axes.SetLimitsX(newXMin, newXMax);
            plot.Refresh();

            isZoomedIn = true;
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
