using System;
using System.Threading.Tasks;
using Microsoft.Win32;
using OscilloscopeCLI.Signal;

namespace OscilloscopeGUI.Services {
    /// <summary>
    /// Trida pro nacitani signalovych dat ze souboru CSV
    /// </summary>
    public class SignalFileService {
        /// <summary>
        /// Zobrazi dialog pro vyber souboru a nacte CSV data
        /// </summary>
        /// <param name="loader">Instance loaderu pro nacteni dat</param>
        /// <returns>True pokud bylo uspesne nacteno, jinak false</returns>
        public async Task<bool> LoadFromCsvAsync(SignalLoader loader) {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true) {
                try {
                    await Task.Run(() => loader.LoadCsvFile(openFileDialog.FileName));
                    return loader.SignalData.Count > 0;
                } catch (Exception ex) {
                    throw new Exception($"Chyba pri nacitani souboru: {ex.Message}");
                }
            }

            return false;
        }
    }
}
