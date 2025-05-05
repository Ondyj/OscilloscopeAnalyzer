using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using OscilloscopeCLI.Signal;

namespace OscilloscopeGUI.Services {
    /// <summary>
    /// Objekt reprezentujici vysledek nacitani CSV souboru – obsahuje priznak uspechu a cestu k souboru.
    /// </summary>
    public class CsvLoadResult {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
    }

    public class FileLoadingService {

        /// <summary>
        /// Zobrazi dialog pro vyber CSV souboru, nacte jeho obsah, zobrazi progress dialog a vrati vysledek nacitani.
        /// </summary>
        public async Task<CsvLoadResult> LoadCsvAsync(SignalLoader loader, Window owner) {
            var openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog(owner) != true)
                return new CsvLoadResult { Success = false };

            string filePath = openFileDialog.FileName;
            var cts = new CancellationTokenSource();
            var progressDialog = new ProgressDialog();
            progressDialog.Owner = owner;
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
        public (bool Success, string? FilePath) PromptForFileOnly(Window owner) {
            var openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog(owner) != true)
                return (false, null);

            return (true, openFileDialog.FileName);
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