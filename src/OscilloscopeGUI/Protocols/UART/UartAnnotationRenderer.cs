using OscilloscopeCLI.Protocols;
using ScottPlot;
using ScottPlot.Plottables;

namespace OscilloscopeGUI;

public class UartAnnotationRenderer : IAnnotationRenderer {
    public void Render(IProtocolAnalyzer analyzer, Plot plot, ByteDisplayFormat format, List<Text> byteLabels, List<IPlottable> byteStartLines) {
        if (analyzer is not UartProtocolAnalyzer uart)
            return;

        var limits = plot.Axes.GetLimits();
        double xMin = limits.Left;
        double xMax = limits.Right;

        var bytes = uart.DecodedBytes;
        for (int i = 0; i < bytes.Count; i++) {
            var b = bytes[i];
            double centerX = (b.StartTime + b.EndTime) / 2;
            if (centerX < xMin || centerX > xMax)
                continue;

            var isError = !string.IsNullOrEmpty(b.Error);
            var color = isError ? Colors.Red : Colors.Black;

            var text = plot.Add.Text(FormatByte(b.Value, format), centerX, 1.3);
            text.LabelStyle.FontSize = 16;
            text.LabelStyle.Bold = true;
            text.LabelFontColor = color;
            byteLabels.Add(text);

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