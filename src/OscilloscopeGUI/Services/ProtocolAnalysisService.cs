using System.Windows;
using OscilloscopeCLI.Protocols;
using OscilloscopeCLI.Signal;

namespace OscilloscopeGUI.Services {
    /// <summary>
    /// Sluzba pro analyzu protokolu na zaklade typu a nastaveni.
    /// </summary>
    public class ProtocolAnalysisService {

        /// <summary>
        /// Provede analyzu signalu podle vybraneho protokolu (napr. UART, SPI), s moznosti manualniho nebo automatickeho nastaveni.
        /// </summary>
        /// <param name="protocol">Nazev protokolu (napr. UART, SPI)</param>
        /// <param name="isManual">Zda uzivatel zvolil manualni zadani parametru</param>
        /// <param name="loader">Loader se signalovymi daty</param>
        /// <param name="uartChannelRenameMap">Mapovani nazvu kanalu pro UART</param>
        /// <param name="lastUsedSpiMapping">Odkaz na posledni mapovani SPI kanalu</param>
        /// <param name="owner">Okno, ktere je vlastnikem zobrazenych dialogu</param>
        /// <returns>Instanci analyzeru nebo null v pripade chyby</returns>
        public IProtocolAnalyzer? Analyze(
            string protocol,
            bool isManual,
            SignalLoader loader,
            UartChannelMapping? uartMapping,
            ref SpiChannelMapping? lastUsedSpiMapping,
            Window owner
        ) {
            if (loader.SignalData.Count == 0) {
                MessageBox.Show("Není načten žádný signál.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            try {
                switch (protocol) {
                    case "UART":
                        UartSettings uartSettings;

                        if (isManual) {
                            var dialog = new UartSettingsDialog();
                            if (dialog.ShowDialog() != true) return null;
                            uartSettings = dialog.Settings;
                        } else {
                            var rawSamples = loader.SignalData.Values.FirstOrDefault();
                            if (rawSamples == null || rawSamples.Count == 0) {
                                MessageBox.Show("Nebyly nalezeny žádné signály pro automatickou detekci.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                                return null;
                            }

                            var signalSamples = rawSamples.Select(t => new SignalSample(t.Item1, t.Item2 > 0.5)).ToList();
                            uartSettings = UartInferenceHelper.InferUartSettings(signalSamples);
                        }

                        return new UartProtocolAnalyzer(loader.SignalData, uartSettings, uartMapping);

                    case "SPI":
                        SpiSettings spiSettings;

                    if (isManual) {
                            var dialog = new SpiSettingsDialog();
                            if (dialog.ShowDialog() != true) return null;
                            spiSettings = dialog.Settings;
                        } else {
                            spiSettings = SpiInferenceHelper.InferSettings(loader.SignalData, lastUsedSpiMapping!);
                        }

                        if (lastUsedSpiMapping == null) {
                            var mapDialog = new SpiChannelMappingDialog(loader.SignalData.Keys.ToList());
                            mapDialog.Owner = owner;
                            if (mapDialog.ShowDialog() != true) return null;
                            lastUsedSpiMapping = mapDialog.Mapping;
                        }

                        return new SpiProtocolAnalyzer(loader.SignalData, spiSettings, lastUsedSpiMapping);

                    default:
                        MessageBox.Show("Není vybrán platný protokol.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return null;
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Chyba při analýze: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}