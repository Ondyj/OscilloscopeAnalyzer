using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OscilloscopeCLI.Signal;

namespace OscilloscopeCLI.Protocols {
    public class UartProtocolAnalyzer : IProtocolAnalyzer {
        public string ProtocolName => "UART";

        private List<(double Timestamp, byte ByteValue)> _decodedBytes = new();
        private List<(double Timestamp, string Error)> _errors = new();

        public void Analyze(List<DigitalSignalAnalyzer.SignalSample> samples, double baudRate) {
            _decodedBytes.Clear();
            _errors.Clear();

            if (samples == null || samples.Count == 0 || baudRate <= 0) {
                _errors.Add((0, "Neplatne vstupni parametry nebo nulovy baud rate."));
                return;
            }

            double bitDuration = 1.0 / baudRate;

            for (int i = 0; i < samples.Count - 10; i++) {
                if (samples[i].State == false && samples[i + 1].State == false) {
                    // Pravdepodobny start bit (prechod na LOW)
                    double startTime = samples[i].Timestamp;

                    // Nacitani bitu (vcetne start bitu)
                    byte value = 0;
                    bool errorDetected = false;

                    for (int bit = 0; bit < 8; bit++) {
                        double sampleTime = startTime + (bit + 1) * bitDuration;
                        bool bitValue = GetBitAtTime(samples, sampleTime);

                        if (bitValue)
                            value |= (byte)(1 << bit); // LSB first
                    }

                    // Kontrola stop bitu
                    double stopBitTime = startTime + 9 * bitDuration;
                    bool stopBit = GetBitAtTime(samples, stopBitTime);

                    if (!stopBit) {
                        _errors.Add((startTime, "Chybny stop bit"));
                        continue;
                    }

                    _decodedBytes.Add((startTime, value));

                    // Posun na konec ramce
                    i = FindIndexAfterTime(samples, stopBitTime);
                }
            }
        }

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
