namespace OscilloscopeCLI.Protocols;

/// <summary>
/// Nastaveni pro analyzator UART protokolu.
/// </summary>
public class UartSettings : IProtocolSettings {
    public string ProtocolName => "UART"; /// Nazev protokolu (UART).

    private int baudRate; // Baud rate (rychlost prenosu)
    private int dataBits; // Pocet datovych bitu
    private int stopBits; // Pocet stop bitu
    private Parity parity; // Parita (None, Even, Odd)

    /// <summary>
    /// Baud rate (rychlost prenosu dat).
    /// </summary>
    public int BaudRate {
        get => baudRate;
        set {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(BaudRate), "BaudRate musí být kladný.");
            baudRate = value;
        }
    }

    /// <summary>
    /// Pocet datovych bitu v jednom prenosu.
    /// </summary>
    public int DataBits {
        get => dataBits;
        set {
            if (value < 5 || value > 9) throw new ArgumentOutOfRangeException(nameof(DataBits), "DataBits musí být v rozsahu 5 až 9.");
            dataBits = value;
        }
    }

    /// <summary>
    /// Pocet stop bitu.
    /// </summary>
    public int StopBits {
        get => stopBits;
        set {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(StopBits), "StopBits musí být alespoň 1.");
            stopBits = value;
        }
    }

    /// <summary>
    /// Parita (None, Even, Odd).
    /// </summary>
    public Parity Parity {
        get => parity;
        set {
            if (!Enum.IsDefined(typeof(Parity), value)) throw new ArgumentException("Neplatná hodnota parity.", nameof(Parity));
            parity = value;
        }
    }

    /// <summary>
    /// Udava, zda je idle uroven vysoká (true = vysoká, false = nízká).
    /// </summary>
    public bool IdleLevelHigh { get; set; }
}

/// <summary>
/// Typy parit (None, Even, Odd) pro UART.
/// </summary>
public enum Parity {
    None, // Bez parity
    Even, // Parita suda
    Odd   // Parita licha
}