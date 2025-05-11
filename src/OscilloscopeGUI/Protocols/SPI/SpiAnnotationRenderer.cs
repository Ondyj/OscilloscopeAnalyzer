using OscilloscopeCLI.Protocols;
using ScottPlot;
using ScottPlot.Plottables;

namespace OscilloscopeGUI;

public class SpiAnnotationRenderer : IAnnotationRenderer {
    public void Render(
        IProtocolAnalyzer analyzer,
        Plot plot,
        ByteDisplayFormat format,
        List<Text> byteLabels,
        List<IPlottable> byteStartLines) {
        if (analyzer is not SpiProtocolAnalyzer spi)
            return;

        var limits = plot.Axes.GetLimits();
        double xMin = limits.Left, xMax = limits.Right;

        var bytes = spi.DecodedBytes;
        for (int i = 0; i < bytes.Count; i++) {
            var b = bytes[i];
            double centerX = (b.StartTime + b.EndTime) / 2;
            if (centerX < xMin || centerX > xMax)
                continue;

            bool hasError = !string.IsNullOrEmpty(b.Error);
            var color = hasError ? Colors.Red : (i % 2 == 0 ? Colors.Gray : Colors.Black);

            var textMosi = plot.Add.Text(FormatByte(b.ValueMOSI, format), centerX, 1.3);
            textMosi.LabelStyle.FontSize = 16;
            textMosi.LabelStyle.Bold = true;
            textMosi.LabelFontColor = color;
            byteLabels.Add(textMosi);

            if (b.HasMISO) {
                var textMiso = plot.Add.Text(FormatByte(b.ValueMISO, format), centerX, -1.5);
                textMiso.LabelStyle.FontSize = 16;
                textMiso.LabelStyle.Bold = true;
                textMiso.LabelFontColor = color;
                byteLabels.Add(textMiso);
            }

            var lineStart = plot.Add.VerticalLine(b.StartTime);
            lineStart.Color = color;
            lineStart.LineWidth = 1;
            lineStart.LinePattern = LinePattern.Dashed;
            byteStartLines.Add(lineStart);

            var lineEnd = plot.Add.VerticalLine(b.EndTime);
            lineEnd.Color = color;
            lineEnd.LineWidth = 1;
            lineEnd.LinePattern = LinePattern.Dashed;
            byteStartLines.Add(lineEnd);
        }
    }

    private string FormatByte(byte b, ByteDisplayFormat format) => format switch {
        ByteDisplayFormat.Hex => $"0x{b:X2}",
        ByteDisplayFormat.Dec => b.ToString(),
        ByteDisplayFormat.Ascii => char.IsControl((char)b) ? "." : ((char)b).ToString(),
        _ => $"0x{b:X2}"
    };
}
