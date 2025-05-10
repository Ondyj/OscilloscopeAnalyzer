using OscilloscopeCLI.Protocols;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

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

        public void UpdateUartStats(UartProtocolAnalyzer uart, List<UartDecodedByte>? filtered = null) {
            var bytes = filtered ?? uart.DecodedBytes;
            int total = bytes.Count;
            int errors = bytes.Count(b => !string.IsNullOrEmpty(b.Error));
            double avgDurationUs = total > 0 ? bytes.Average(b => (b.EndTime - b.StartTime) * 1e6) : 0;
            double minUs = total > 0 ? bytes.Min(b => (b.EndTime - b.StartTime) * 1e6) : 0;
            double maxUs = total > 0 ? bytes.Max(b => (b.EndTime - b.StartTime) * 1e6) : 0;

            double bitsPerByte = uart.Settings.DataBits + 1 + (uart.Settings.Parity == Parity.None ? 0 : 1) + uart.Settings.StopBits;
            double avgBaud = avgDurationUs > 0 ? (bitsPerByte * 1_000_000.0 / avgDurationUs) : 0;

            StatsBaudRate.Text = $"Odhadovaná přenosová rychlost: {avgBaud:F0} baud";
            StatsMinMaxDuration.Text = $"Délka bajtu (min/max): {minUs:F1} / {maxUs:F1} µs";
            StatsSpiTransfers.Text = "Počet přenosů (SPI): –";
            StatsMosiMiso.Text = "Počet bajtů MOSI/MISO: –";
            StatsTotalBytes.Text = $"Celkový počet bajtů: {total}";
            StatsErrors.Text = $"Počet bajtů s chybou: {errors}";
            StatsAvgDuration.Text = $"Průměrná délka bajtu: {avgDurationUs:F1} µs";
        }

        public void UpdateSpiStats(SpiProtocolAnalyzer spi, List<SpiDecodedByte>? filtered = null) {
            var bytes = filtered ?? spi.DecodedBytes;
            int total = bytes.Count;
            int errors = bytes.Count(b => !string.IsNullOrEmpty(b.Error));
            double avgDurationUs = total > 0 ? bytes.Average(b => (b.EndTime - b.StartTime) * 1e6) : 0;
            double minUs = total > 0 ? bytes.Min(b => (b.EndTime - b.StartTime) * 1e6) : 0;
            double maxUs = total > 0 ? bytes.Max(b => (b.EndTime - b.StartTime) * 1e6) : 0;

            int transferCount = spi.TransferCount;
            double avgTransferLength = spi.AvgTransferDurationUs;
            int misoBytes = bytes.Count(b => b.HasMISO);

            StatsBaudRate.Text = $"Prům. délka SPI přenosu: {avgTransferLength:F1} µs";
            StatsMinMaxDuration.Text = $"Délka bajtu (min/max): {minUs:F1} / {maxUs:F1} µs";
            StatsSpiTransfers.Text = $"Počet přenosů (CS aktivní): {transferCount}";
            StatsMosiMiso.Text = $"Bajty MOSI / MISO: {total - misoBytes} / {misoBytes}";
            StatsTotalBytes.Text = $"Celkový počet bajtů: {total}";
            StatsErrors.Text = $"Počet bajtů s chybou: {errors}";
            StatsAvgDuration.Text = $"Průměrná délka bajtu: {avgDurationUs:F1} µs";
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