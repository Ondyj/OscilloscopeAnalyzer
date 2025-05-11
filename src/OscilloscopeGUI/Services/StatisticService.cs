using OscilloscopeCLI.Protocols;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.IO;

namespace OscilloscopeGUI.Services {
    public class StatisticsService {
        private readonly TextBlock StatsTotalBytes;
        private readonly TextBlock StatsErrors;
        private readonly TextBlock StatsAvgDuration;
        private readonly TextBlock StatsBaudRate;
        private readonly TextBlock StatsMinMaxDuration;
        private readonly TextBlock StatsSpiTransfers;
        private readonly TextBlock StatsMosiMiso;
        private readonly TextBlock StatsAnalysisMode;

        public StatisticsService(
            TextBlock statsTotalBytes,
            TextBlock statsErrors,
            TextBlock statsAvgDuration,
            TextBlock statsBaudRate,
            TextBlock statsMinMaxDuration,
            TextBlock statsSpiTransfers,
            TextBlock statsMosiMiso,
            TextBlock statsAnalysisMode)
        {
            StatsTotalBytes = statsTotalBytes;
            StatsErrors = statsErrors;
            StatsAvgDuration = statsAvgDuration;
            StatsBaudRate = statsBaudRate;
            StatsMinMaxDuration = statsMinMaxDuration;
            StatsSpiTransfers = statsSpiTransfers;
            StatsMosiMiso = statsMosiMiso;
            StatsAnalysisMode = statsAnalysisMode;
        }

        public void UpdateUartStats(UartProtocolAnalyzer uart) {
            StatsTotalBytes.Text = $"Celkový počet bajtů: {uart.TotalBytes}";
            StatsErrors.Text = $"Počet bajtů s chybou: {uart.ErrorCount}";
            StatsAvgDuration.Text = $"Průměrná délka bajtu: {uart.AvgDurationUs:F1} µs";
            StatsBaudRate.Text = $"Odhad: {uart.EstimatedBaudRate:F0} baud | délka bitu: {uart.EstimatedBitTimeUs:F2} µs";
            StatsMinMaxDuration.Text = $"Délka bajtu (min/max): {uart.MinDurationUs:F1} / {uart.MaxDurationUs:F1} µs";

            StatsSpiTransfers.Visibility = System.Windows.Visibility.Collapsed;
            StatsMosiMiso.Visibility = System.Windows.Visibility.Collapsed;

            StatsTotalBytes.Visibility = System.Windows.Visibility.Visible;
            StatsErrors.Visibility = System.Windows.Visibility.Visible;
            StatsAvgDuration.Visibility = System.Windows.Visibility.Visible;
            StatsBaudRate.Visibility = System.Windows.Visibility.Visible;
            StatsMinMaxDuration.Visibility = System.Windows.Visibility.Visible;

        }

        public void UpdateSpiStats(SpiProtocolAnalyzer spi) {
            StatsTotalBytes.Text = $"Celkový počet bajtů: {spi.TotalBytes}";
            StatsErrors.Text = $"Počet bajtů s chybou: {spi.ErrorCount}";
            StatsAvgDuration.Text = $"Průměrná délka bajtu: {spi.AvgDurationUs:F1} µs";
            StatsBaudRate.Text = $"Odhad: {spi.EstimatedBitRate:F0} bps | délka bitu: {spi.EstimatedBitTimeUs:F2} µs";
            StatsMinMaxDuration.Text = $"Délka bajtu (min/max): {spi.MinDurationUs:F1} / {spi.MaxDurationUs:F1} µs";
            StatsSpiTransfers.Text = $"Počet přenosů{(spi.HasChipSelect ? " (CS aktivní)" : " (bez CS)")} : {spi.TransferCount}";
            StatsMosiMiso.Text = $"Bajty MOSI / MISO: {spi.TotalBytes - spi.MisoByteCount} / {spi.MisoByteCount}";

            StatsSpiTransfers.Visibility = System.Windows.Visibility.Visible;
            StatsMosiMiso.Visibility = System.Windows.Visibility.Visible;
            StatsTotalBytes.Visibility = System.Windows.Visibility.Visible;
            StatsErrors.Visibility = System.Windows.Visibility.Visible;
            StatsAvgDuration.Visibility = System.Windows.Visibility.Visible;
            StatsBaudRate.Visibility = System.Windows.Visibility.Visible;
            StatsMinMaxDuration.Visibility = System.Windows.Visibility.Visible;
        }

        public void Reset() {
            StatsTotalBytes.Text = "Celkový počet bajtů: –";
            StatsErrors.Text = "Počet bajtů s chybou: –";
            StatsAvgDuration.Text = "Průměrná délka bajtu: –";
            StatsBaudRate.Text = "Odhadovaná rychlost (baud): –";
            StatsMinMaxDuration.Text = "Délka bajtu (min/max): –";
            StatsSpiTransfers.Text = "Počet SPI přenosů (CS aktivní): –";
            StatsMosiMiso.Text = "Bajty MOSI / MISO: –";
            StatsAnalysisMode.Text = "Režim analýzy: –";
        }
    }
}