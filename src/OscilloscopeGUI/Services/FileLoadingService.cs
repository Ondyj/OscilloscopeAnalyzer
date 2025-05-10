using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using OscilloscopeCLI.Protocols;
using OscilloscopeCLI.Signal;

namespace OscilloscopeGUI.Services {
    /// <summary>
    /// Vzsledek zakladniho nacteni CSV.
    /// </summary>
    public class CsvLoadResult {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
    }

    /// <summary>
    /// Vzsledek nacteni CSV souboru a mapovani protokolu.
    /// </summary>
    public class CsvLoadAndMapResult {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public Dictionary<string, string> RenameMap { get; set; } = new();
        public string? SelectedProtocol { get; set; }
        public UartChannelMapping? UartMapping { get; set; }
        public SpiChannelMapping? SpiMapping { get; set; }
    }

    public class FileLoadingService {

        public (bool Success, string? FilePath) PromptForFileOnly(Window owner) {
            var openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog(owner) != true)
                return (false, null);

            return (true, openFileDialog.FileName);
        }

        public async Task<CsvLoadAndMapResult> LoadAndMapAsync(SignalLoader loader, Window owner) {
            var filePick = PromptForFileOnly(owner);
            if (!filePick.Success)
                return new CsvLoadAndMapResult { Success = false };

            string filePath = filePick.FilePath!;

            var loadResult = await LoadFromFilePathAsync(loader, filePath, owner);
            if (!loadResult.Success)
                return new CsvLoadAndMapResult { Success = false };

            var activeChannels = loader.GetRemainingChannelNames();
            var protocolDialog = new ProtocolSelectDialog(activeChannels.Count) {
                Owner = owner
            };
            if (protocolDialog.ShowDialog() != true)
                return new CsvLoadAndMapResult { Success = false };

            string selectedProtocol = protocolDialog.SelectedProtocol;
            Dictionary<string, string> renameMap = new();
            UartChannelMapping? uartMap = null;
            SpiChannelMapping? spiMap = null;

            if (selectedProtocol == "SPI") {
                var spiMapDialog = new SpiChannelMappingDialog(activeChannels) {
                    Owner = owner
                };
                if (spiMapDialog.ShowDialog() != true)
                    return new CsvLoadAndMapResult { Success = false };

                spiMap = spiMapDialog.Mapping;
                renameMap = new Dictionary<string, string> {
                    { spiMap.ChipSelect, "CS" },
                    { spiMap.Clock, "SCLK" },
                    { spiMap.Mosi, "MOSI" }
                };
                if (!string.IsNullOrEmpty(spiMap.Miso))
                    renameMap[spiMap.Miso] = "MISO";
            }
            else if (selectedProtocol == "UART") {
                var uartMapDialog = new UartChannelMappingDialog(activeChannels) {
                    Owner = owner
                };
                if (uartMapDialog.ShowDialog() != true)
                    return new CsvLoadAndMapResult { Success = false };

                var mapping = uartMapDialog.ChannelRenames;
                uartMap = new UartChannelMapping {
                    Tx = mapping.FirstOrDefault(kv => kv.Value == "TX").Key ?? "",
                    Rx = mapping.FirstOrDefault(kv => kv.Value == "RX").Key ?? ""
                };

                if (mapping.Count > 1 && !uartMap.IsValid()) {
                    MessageBox.Show(
                        "Mapování UART signálů není validní (TX a RX musí být různé a neprázdné).",
                        "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                    return new CsvLoadAndMapResult { Success = false };
                }
                else if (mapping.Count == 0) {
                    MessageBox.Show(
                        "Musíte vybrat alespoň jednu roli (RX nebo TX).",
                        "Neúplné mapování", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return new CsvLoadAndMapResult { Success = false };
                }

                foreach (var kv in mapping)
                    renameMap[kv.Key] = kv.Value;
            }

            return new CsvLoadAndMapResult {
                Success = true,
                FilePath = filePath,
                SelectedProtocol = selectedProtocol,
                RenameMap = renameMap,
                UartMapping = uartMap,
                SpiMapping = spiMap
            };
        }

        public async Task<CsvLoadResult> LoadFromFilePathAsync(SignalLoader loader, string filePath, Window owner) {
            var cts = new CancellationTokenSource();
            var progressDialog = new ProgressDialog {
                Owner = owner
            };
            progressDialog.Show();
            var progress = new Progress<int>(value => progressDialog.ReportProgress(value));
            progressDialog.OnCanceled = () => cts.Cancel();

            try {
                bool loaded = await Task.Run(() => {
                    loader.LoadCsvFile(filePath, progress, cts.Token);
                    return loader.SignalData.Count > 0;
                }, cts.Token);

                if (cts.IsCancellationRequested) {
                    progressDialog.Finish("Načítání bylo zrušeno.", autoClose: false);
                    progressDialog.OnOkClicked = () => progressDialog.Close();
                    return new CsvLoadResult { Success = false };
                }

                if (!loaded) {
                    progressDialog.SetErrorState();
                    progressDialog.Finish("Soubor je prázdný nebo poškozený.", autoClose: false);
                    progressDialog.OnOkClicked = () => progressDialog.Close();
                    return new CsvLoadResult { Success = false };
                }

                progressDialog.Close();
                return new CsvLoadResult {
                    Success = true,
                    FilePath = filePath
                };
            } catch (Exception ex) {
                progressDialog.SetErrorState();
                progressDialog.Finish($"Chyba při načítání: {ex.Message}", autoClose: false);
                progressDialog.OnOkClicked = () => progressDialog.Close();
                return new CsvLoadResult { Success = false };
            }
        }
    }
}