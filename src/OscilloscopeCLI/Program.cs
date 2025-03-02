using System;
using OscilloscopeCLI.Data;

class Program
{
    static void Main() {
        string filePathCSV = "data/rs115200.csv";
        string filePathTXT = "data/rs115200_csv.txt";

        try {
            SignalLoader loader = new SignalLoader();
            loader.LoadCsvFile(filePathCSV);

            loader.PrintSignalData(); // Výpis prvních 10 vzorků signálu
        } catch (Exception ex) {
            Console.WriteLine($"Chyba: {ex.Message}");
        }
    }
}