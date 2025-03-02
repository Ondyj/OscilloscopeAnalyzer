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

            loader.PrintSignalData(); // Vypis prvnich 10 vzorku signalu
        } catch (Exception ex) {
            Console.WriteLine($"Chyba: {ex.Message}");
        }
    }
}