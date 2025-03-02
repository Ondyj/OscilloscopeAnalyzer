# OscilloscopeAnalyzer

spusteni programu dotnet run --project src/OscilloscopeCLI


# Struktura projektu
OscilloscopeCLI/                 # Hlavní složka projektu
│── src/                         # Zdrojové kódy aplikace
│   │── Program.cs               # Hlavní vstupní bod aplikace
│   │── OscilloscopeCLI.csproj   # Projektový soubor pro .NET
│   │── Data/
│   │   │── SignalLoader.cs      # Načítání dat ze souborů (zatím TXT)
│   │── Protocols/
│   │── Utils/
│── data/                        # Testovací soubory a vzorkovaná data
│   │── rs115200.csv             # Ukázkový CSV soubor
│   │── rs115200.wfm             # Binární soubor osciloskopu
│   │── rs115200_txt.txt         # Textový výpis metadat
│── README.md                    # Hlavní popis projektu