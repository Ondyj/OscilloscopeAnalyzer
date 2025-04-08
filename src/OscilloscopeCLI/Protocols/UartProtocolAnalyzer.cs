using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OscilloscopeCLI.ProtocolSettings;
using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols {
    public class UartProtocolAnalyzer : IProtocolAnalyzer {
        public string ProtocolName => "UART";

        private List<(double Timestamp, byte ByteValue)> _decodedBytes = new();
        private List<(double Timestamp, string Error)> _errors = new();

        public void Analyze(List<DigitalSignalAnalyzer.SignalSample> samples, IProtocolSettings settings) {
            _decodedBytes.Clear();
            _errors.Clear();

            // Osetreni typu nastaveni
            if (settings is not UartSettings uartSettings) {
                _errors.Add((0, "Neplatne nastaveni UART."));
                return;
            }

            if (samples == null || samples.Count == 0 || uartSettings.BaudRate <= 0) {
                _errors.Add((0, "Neplatne vstupni parametry nebo nulovy baud rate."));
                return;
            }

            double bitDuration = 1.0 / uartSettings.BaudRate;

            for (int i = 0; i < samples.Count - 10; i++) {
                bool level = uartSettings.IdleHigh;
                bool startDetected = samples[i].State != level && samples[i + 1].State != level;

                if (startDetected) {
                    double startTime = samples[i].Timestamp;
                    byte value = 0;

                    // Cteni datovych bitu
                    for (int bit = 0; bit < uartSettings.DataBits; bit++) {
                        double sampleTime = startTime + (bit + 1) * bitDuration;
                        bool bitValue = GetBitAtTime(samples, sampleTime);
                        if (uartSettings.IdleHigh) bitValue = !bitValue;

                        if (bitValue)
                            value |= (byte)(1 << bit); // LSB first
                    }

                    int parityOffset = uartSettings.DataBits + 1;

                    // Kontrola parity pokud je aktivni
                    if (uartSettings.ParityEnabled) {
                        double parityTime = startTime + parityOffset * bitDuration;
                        bool parityBit = GetBitAtTime(samples, parityTime);
                        if (uartSettings.IdleHigh) parityBit = !parityBit;

                        int oneBits = 0;
                        for (int b = 0; b < uartSettings.DataBits; b++)
                            if ((value & (1 << b)) != 0) oneBits++;

                        bool expectedParity = uartSettings.ParityEven ? (oneBits % 2 == 0) : (oneBits % 2 != 0);
                        if (parityBit != expectedParity) {
                            _errors.Add((startTime, "Chybna parita"));
                            continue;
                        }
                    }

                    // Kontrola stop bitu
                    int stopStart = uartSettings.DataBits + 1 + (uartSettings.ParityEnabled ? 1 : 0);
                    bool validStop = true;
                    for (int sb = 0; sb < uartSettings.StopBits; sb++) {
                        double stopTime = startTime + (stopStart + sb) * bitDuration;
                        bool stopBit = GetBitAtTime(samples, stopTime);
                        if (uartSettings.IdleHigh) stopBit = !stopBit;
                        if (!stopBit) validStop = false;
                    }

                    if (!validStop) {
                        _errors.Add((startTime, "Chybny stop bit"));
                        continue;
                    }

                    _decodedBytes.Add((startTime, value));
                    double frameEndTime = startTime + (stopStart + uartSettings.StopBits) * bitDuration;
                    i = FindIndexAfterTime(samples, frameEndTime);
                }
            }
        }

        /// <summary>
        /// Exportuje dekodovana data a chyby do CSV souboru.
        /// </summary>
        public void ExportResults(string outputPath) {
            using var writer = new StreamWriter(outputPath);

            writer.WriteLine("Timestamp [s];Byte (hex);ASCII;Error");

            int i = 0, j = 0;

            while (i < _decodedBytes.Count || j < _errors.Count) {
                bool writeByte = i < _decodedBytes.Count &&
                                 (j >= _errors.Count || _decodedBytes[i].Timestamp < _errors[j].Timestamp);

                if (writeByte) {
                    var (ts, value) = _decodedBytes[i++];
                    writer.WriteLine($"{ts.ToString("F6", CultureInfo.InvariantCulture)};0x{value:X2};{(char)value};");
                } else {
                    var (ts, err) = _errors[j++];
                    writer.WriteLine($"{ts.ToString("F6", CultureInfo.InvariantCulture)};;;{err}");
                }
            }
        }

        /// <summary>
        /// Vrati hodnotu bitu v danem case. Vyhleda nejblizsi vzorek.
        /// </summary>
        private bool GetBitAtTime(List<DigitalSignalAnalyzer.SignalSample> samples, double time) {
            for (int i = 1; i < samples.Count; i++) {
                if (samples[i].Timestamp >= time)
                    return samples[i - 1].State;
            }
            return samples[^1].State; // fallback
        }

        /// <summary>
        /// Najde index vzorku, ktery je prvni za danym casem.
        /// </summary>
        private int FindIndexAfterTime(List<DigitalSignalAnalyzer.SignalSample> samples, double time) {
            for (int i = 0; i < samples.Count; i++) {
                if (samples[i].Timestamp > time)
                    return i;
            }
            return samples.Count - 1;
        }
    }
}
