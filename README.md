# OscilloscopeAnalyzer

## Ovládání grafu (vizualizace signálu)

- **W** – roztahuje graf na časové ose (zoom in)
- **S** – stahuje graf na časové ose (zoom out)
- **Kolečko myši** – přibližování / oddalování
- Doporučení: pokud se chceš podívat detailněji na průběh signálu, **nejprve si graf roztáhni pomocí klávesy `W`**


## Sestavení a spuštění
```bash
dotnet clean
dotnet build

spusteni programu 
dotnet run --project src/OscilloscopeCLI
dotnet run --project src/OscilloscopeGUI
```

# Struktura projektu
```bash
OscilloscopeAnalyzer/
│
├── src/
│   ├── OscilloscopeCLI/              # Backend – zpracování a analýza signálů
│   │   ├── Signal/                   # Načítání a základní analýza signálů
│   │   ├── Protocols/                # Implementace detekce a dekódování protokolů 
│   │   ├── ProtocolSettings/         # Nastavení pro jednotlivé protokoly
│   │   └── OscilloscopeCLI.csproj
│   │
│   └── OscilloscopeGUI/              # WPF GUI – vizualizace a ovládací prvky
│       ├── Plotting/                 # Vykreslování signálů
│       ├── Services/                 # Služby (např. zpracování CSV, analýza UART)
│       ├── App.xaml + MainWindow.xaml(.cs)
│       └── OscilloscopeGUI.csproj
│
└── README.md                         # Dokumentace k projektu 
```