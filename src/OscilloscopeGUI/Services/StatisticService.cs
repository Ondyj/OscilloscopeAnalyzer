using OscilloscopeCLI.Protocols;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.IO;
using System.Windows;

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
        private readonly TextBlock StatsUartTransfers;
        private readonly TextBlock StatsUartAvgGap;
        private readonly TextBlock StatsUartMinMaxGap;
        private readonly StackPanel StatsPanelLeft;
        private readonly StackPanel StatsPanelRight;
        private readonly TextBlock StatsSpiCsGap;
        private readonly TextBlock StatsSpiEdgeDelay;

        public StatisticsService(
            TextBlock statsTotalBytes,
            TextBlock statsErrors,
            TextBlock statsAvgDuration,
            TextBlock statsBaudRate,
            TextBlock statsMinMaxDuration,
            TextBlock statsSpiTransfers,
            TextBlock statsMosiMiso,
            TextBlock statsAnalysisMode,
            TextBlock statsUartTransfers,
            TextBlock statsUartAvgGap,
            TextBlock statsUartMinMaxGap,
            StackPanel statsPanelLeft,
            StackPanel statsPanelRight,
            TextBlock statsSpiCsGap,
            TextBlock statsSpiEdgeDelay
            )
        {
            StatsTotalBytes = statsTotalBytes;
            StatsErrors = statsErrors;
            StatsAvgDuration = statsAvgDuration;
            StatsBaudRate = statsBaudRate;
            StatsMinMaxDuration = statsMinMaxDuration;
            StatsSpiTransfers = statsSpiTransfers;
            StatsMosiMiso = statsMosiMiso;
            StatsAnalysisMode = statsAnalysisMode;
            StatsUartTransfers = statsUartTransfers;
            StatsUartAvgGap = statsUartAvgGap;
            StatsUartMinMaxGap = statsUartMinMaxGap;
            StatsPanelLeft = statsPanelLeft;
            StatsPanelRight = statsPanelRight;
            StatsSpiCsGap = statsSpiCsGap;
            StatsSpiEdgeDelay = statsSpiEdgeDelay;
        }

        public void UpdateUartStats(UartProtocolAnalyzer uart) {
            StatsTotalBytes.Text = $"Celkový počet bajtů: {uart.TotalBytes}";
            StatsErrors.Text = $"Počet bajtů s chybou: {uart.ErrorCount}";
            StatsAvgDuration.Text = $"Průměrná délka bajtu: {uart.AvgDurationUs:F1} µs";
            StatsBaudRate.Text = $"Odhad: {uart.EstimatedBaudRate:F0} baud | délka bitu: {uart.EstimatedBitTimeUs:F2} µs";
            StatsMinMaxDuration.Text = $"Délka bajtu (min/max): {uart.MinDurationUs:F1} / {uart.MaxDurationUs:F1} µs";
            StatsUartTransfers.Text = $"Počet přenosů: {uart.TransferCount}";
            StatsUartAvgGap.Text = $"Průměrná mezera: {uart.AvgGapUs:F1} µs";
            StatsUartMinMaxGap.Text = $"Mezera (min/max): {uart.MinGapUs:F1} / {uart.MaxGapUs:F1} µs";

            StatsPanelLeft.Visibility = Visibility.Visible;
            StatsPanelRight.Visibility = Visibility.Visible;

            StatsUartTransfers.Visibility = Visibility.Visible;
            StatsUartAvgGap.Visibility = Visibility.Visible;
            StatsUartMinMaxGap.Visibility = Visibility.Visible;

            StatsSpiTransfers.Visibility = Visibility.Collapsed;
            StatsMosiMiso.Visibility = Visibility.Collapsed;
            StatsSpiCsGap.Visibility = Visibility.Collapsed;
            StatsSpiEdgeDelay.Visibility = Visibility.Collapsed;
        }

        public void UpdateSpiStats(SpiProtocolAnalyzer spi) {
            StatsTotalBytes.Text = $"Celkový počet bajtů: {spi.TotalBytes}";
            StatsErrors.Text = $"Počet bajtů s chybou: {spi.ErrorCount}";
            StatsAvgDuration.Text = $"Průměrná délka bajtu: {spi.AvgDurationUs:F1} µs";
            StatsBaudRate.Text = $"Odhad: {spi.EstimatedBitRate:F0} bps | délka bitu: {spi.EstimatedBitTimeUs:F2} µs";
            StatsMinMaxDuration.Text = $"Délka bajtu (min/max): {spi.MinDurationUs:F1} / {spi.MaxDurationUs:F1} µs";
            StatsSpiTransfers.Text = $"Počet přenosů{(spi.HasChipSelect ? " (CS aktivní)" : " (bez CS)")} : {spi.TransferCount}";
            StatsMosiMiso.Text = $"Bajty MOSI / MISO: {spi.MosiByteCount} / {spi.MisoByteCount}";

            if (spi.HasChipSelect) {
                StatsSpiCsGap.Text = $"Průměrná mezera mezi CS: {spi.AvgCsGapUs:F1} µs";
                StatsSpiEdgeDelay.Text = $"Zpoždění první hrany hodin: {spi.AvgDelayToFirstEdgeUs:F1} µs";
            } else {
                StatsSpiCsGap.Text = "Průměrná mezera mezi CS: (bez CS)";
                StatsSpiEdgeDelay.Text = "Zpoždění první hrany hodin: (bez CS)";
            }

            StatsPanelLeft.Visibility = Visibility.Visible;
            StatsPanelRight.Visibility = Visibility.Visible;

            StatsSpiTransfers.Visibility = Visibility.Visible;
            StatsMosiMiso.Visibility = Visibility.Visible;
            StatsSpiCsGap.Visibility = Visibility.Visible;
            StatsSpiEdgeDelay.Visibility = Visibility.Visible;

            StatsUartTransfers.Visibility = Visibility.Collapsed;
            StatsUartAvgGap.Visibility = Visibility.Collapsed;
            StatsUartMinMaxGap.Visibility = Visibility.Collapsed;
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
            StatsSpiCsGap.Text = "Průměrná mezera mezi CS: –";
            StatsSpiEdgeDelay.Text = "Zpoždění první hrany hodin: –";
            StatsPanelLeft.Visibility = Visibility.Collapsed;
            StatsPanelRight.Visibility = Visibility.Collapsed;
        }
    }
}