using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core.Definitions;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

/// <summary>Validates the actual loaded Rust trade view without submitting broker commands.</summary>
public sealed class IronCondorViewParityTests
{
    [IronCondorParityFact]
    public void Loaded_trade_matches_WinForms_dashboard_geometry_and_palette()
    {
        // Explicitly select this test against a running UI with an established trade loaded.
        var processId = Environment.GetEnvironmentVariable("IFM_CONDOR_PARITY_PID");
        Assert.False(string.IsNullOrEmpty(processId));
        var previousDpi = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            using var process = Process.GetProcessById(int.Parse(processId!, System.Globalization.CultureInfo.InvariantCulture));
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(process.MainWindowHandle).AsWindow();
            window.Focus();
            var timer=Stopwatch.StartNew();
            var nodes=window.FindAllDescendants();
            while (!nodes.Any(node=>node.Properties.Name.ValueOrDefault=="ActionDateTime") && timer.Elapsed<TimeSpan.FromSeconds(15)) {Thread.Sleep(200);nodes=window.FindAllDescendants();}
            var names = nodes.Select(node => node.Properties.Name.ValueOrDefault).ToArray();
            Assert.Contains("Iron Condor Net Spread Path", names);
            Assert.DoesNotContain("TradePlans", names);
            Assert.Contains("Spread Type", names);
            Assert.Contains("Short IVol", names);
            Assert.Contains("Long IVol", names);
            Assert.Contains("PutCreditSpread", names);
            Assert.Contains("CallCreditSpread", names);
            Assert.Contains(nodes,node=>node.ControlType==ControlType.ComboBox);
            Assert.DoesNotContain("Position History", names);
            Assert.DoesNotContain("Trade Details", names);
            Assert.DoesNotContain("Trade Limits", names);
            Assert.DoesNotContain("Opening Fills", names);
            Assert.DoesNotContain("Trade Order Editor", names);
            Assert.DoesNotContain("Close Trade", names);
            var closeButtons=nodes.Where(node=>node.ControlType==ControlType.Button && node.Properties.Name.ValueOrDefault?.StartsWith("Close trade ",StringComparison.Ordinal)==true).ToArray();
            Assert.NotEmpty(closeButtons);
            foreach(var closeButton in closeButtons) {
                var title=closeButton.Properties.Name.ValueOrDefault!["Close trade ".Length..];
                var titleButton=nodes.Single(node=>node.ControlType==ControlType.Button && node.Properties.Name.ValueOrDefault==title);
                Assert.InRange(closeButton.BoundingRectangle.Left-titleButton.BoundingRectangle.Right,0,10);
                Assert.InRange(Math.Abs(closeButton.BoundingRectangle.Top-titleButton.BoundingRectangle.Top),0,5);
                Assert.True(closeButton.BoundingRectangle.Bottom < nodes.First(node=>node.Properties.Name.ValueOrDefault=="Order Id").BoundingRectangle.Top);
            }
            var headers = nodes.Where(node => node.Properties.Name.ValueOrDefault == "Order Id").OrderBy(node => node.BoundingRectangle.Top).ToArray();
            Assert.Equal(3, headers.Length);
            var history = headers[0].BoundingRectangle;
            var details = headers[1].BoundingRectangle;
            var limits = headers[2].BoundingRectangle;
            Assert.InRange(Math.Abs(history.Left - details.Left), 0, 5);
            Assert.InRange(Math.Abs(history.Left - limits.Left), 0, 5);
            Assert.True(details.Top > history.Bottom);
            Assert.InRange(limits.Top - details.Top, 110, 130); // WinForms 120px contract pane.
            var planHeader = nodes.Single(node => node.Properties.Name.ValueOrDefault == "ActionDateTime").BoundingRectangle;
            var spread = nodes.Single(node => node.Properties.Name.ValueOrDefault == "Spread Type").BoundingRectangle;
            Assert.True(spread.Top > limits.Top);
            Assert.True(planHeader.Top > spread.Bottom);
            var evidence = Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")
                ?? Path.Combine(Path.GetTempPath(), "ifm-condor-parity");
            Directory.CreateDirectory(evidence);
            var screenshot = Path.Combine(evidence, "iron-condor-rust-flaui.png");
            window.Capture().Save(screenshot);
            using var bitmap = new Bitmap(screenshot);
            var black = 0; var white = 0; var magenta = 0; var command = 0;
            for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.ToArgb() == Color.Black.ToArgb()) black++;
                if (color.ToArgb() == Color.White.ToArgb()) white++;
                if (color.ToArgb() == Color.Magenta.ToArgb()) magenta++;
                if (color.R == 45 && color.G == 45 && color.B == 48) command++;
            }
            Assert.True(black > bitmap.Width * bitmap.Height / 2);
            Assert.True(white > 100);
            Assert.True(magenta > 5);
            Assert.True(command > 1000);
            File.WriteAllText(Path.Combine(evidence, "iron-condor-flaui.json"), JsonSerializer.Serialize(new
            {
                ProcessId = process.Id,
                HistoryHeader = history.ToString(), TradeHeader = details.ToString(), LimitsHeader = limits.ToString(),
                SpreadHeader = spread.ToString(), PlanHeader = planHeader.ToString(), PlanTabRemoved = true,
                PalettePixels = new { Black = black, White = white, Magenta = magenta, Command = command },
                Controls = nodes.Where(node => node.ControlType == ControlType.ComboBox).Select(node => node.BoundingRectangle.ToString()).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { SetThreadDpiAwarenessContext(previousDpi); }
    }

    [DllImport("user32.dll")]
    static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}

public sealed class IronCondorParityFactAttribute : FactAttribute
{
    public IronCondorParityFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IFM_CONDOR_PARITY_PID")))
            Skip="Set IFM_CONDOR_PARITY_PID to a running Rust UI with an established Iron Condor loaded.";
    }
}
