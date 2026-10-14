using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
using FlaUI.Core.WindowsAPI;

public sealed class MainMenuParityTests
{
    [Fact]
    public async Task Actual_frontends_show_the_same_top_navigation_order_and_health_links()
    {
        var root = Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;
        var output = Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".artifacts/rust-ui/development-ui-processes.json")));
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        Assert.True((await http.GetAsync("http://localhost:22543/health/launch-ready")).IsSuccessStatusCode);
        using var dpi = new DpiScope();
        using var automation = new UIA3Automation();
        foreach (var field in new[] { "WinFormsProcessId", "RustProcessId" })
        {
            using var process = Process.GetProcessById(state.RootElement.GetProperty(field).GetInt32());
            process.Refresh();
            var window = automation.FromHandle(process.MainWindowHandle).AsWindow();
            window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
            window.Focus();
            Thread.Sleep(1200);
            var labels = new[] { "Trade Orders", "Market Data", "Portfolio", "Reference", "System" };
            var buttons = labels.Select(label => window.FindAllDescendants(cf => cf.ByName(label).And(cf.ByControlType(ControlType.Button)))
                .Single(e => e.BoundingRectangle.Top > window.BoundingRectangle.Top + 20)).ToArray();
            // Exclude the Windows title-bar System menu, which has the same accessible name.
            for (var i = 0; i < buttons.Length; i++)
            {
                Assert.True(buttons[i].IsEnabled);
                Assert.InRange(buttons[i].BoundingRectangle.Top - buttons[0].BoundingRectangle.Top, -2, 2);
                Assert.True(buttons[i].BoundingRectangle.Height > 10);
                if (i > 0) Assert.True(buttons[i].BoundingRectangle.Left >= buttons[i-1].BoundingRectangle.Right);
            }
            var feed = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).First(e => e.Name is "Start Market Feeds" or "Stop Market Feeds");
            var health = FindHealth(window);
            Assert.True(feed.BoundingRectangle.Left >= buttons[^1].BoundingRectangle.Right);
            Assert.True(health.BoundingRectangle.Left >= feed.BoundingRectangle.Right);
            Assert.InRange(health.BoundingRectangle.Top-buttons[0].BoundingRectangle.Top, -4, 4);
            if (field == "RustProcessId")
            {
                buttons[3].Click(); Thread.Sleep(300);
                Assert.NotNull(window.FindFirstDescendant(cf => cf.ByName("Lookup Definitions")));
                Keyboard.Press(VirtualKeyShort.ESCAPE);
                buttons[4].Click(); Thread.Sleep(300);
                Assert.NotNull(window.FindFirstDescendant(cf => cf.ByName("Scheduled Tasks")));
                Keyboard.Press(VirtualKeyShort.ESCAPE);
            }
            FlaUI.Core.Input.Mouse.MoveTo(new System.Drawing.Point(window.BoundingRectangle.Right - 30, window.BoundingRectangle.Bottom - 30));
            Thread.Sleep(150);
            var capturePath = Path.Combine(output, field + "-menu-bar.png");
            window.Capture().Save(capturePath);
            using (var image = new System.Drawing.Bitmap(capturePath))
            {
                if (feed.Name == "Stop Market Feeds") AssertColors(image, window, feed, System.Drawing.Color.Red, System.Drawing.Color.White);
                var palette = health.Name switch
                {
                    "Feed Health: Green" => (System.Drawing.Color.LimeGreen, System.Drawing.Color.Black),
                    "Feed Health: Yellow" => (System.Drawing.Color.Yellow, System.Drawing.Color.Black),
                    "Feed Health: Red" => (System.Drawing.Color.Red, System.Drawing.Color.White),
                    "Feed Health: Orange / Resetting" or "Feed Health: Off-hours Degraded" => (System.Drawing.Color.DarkOrange, System.Drawing.Color.Black),
                    "Feed Health: Off-hours Active" => (System.Drawing.Color.SteelBlue, System.Drawing.Color.White),
                    _ => (System.Drawing.Color.DimGray, System.Drawing.Color.White)
                };
                AssertColors(image, window, health, palette.Item1, palette.Item2);
            }
            Assert.True(process.Responding);
        }
        Assert.True((await http.GetAsync("http://localhost:22543/health/launch-ready")).IsSuccessStatusCode);
    }
    static void AssertColors(System.Drawing.Bitmap image, Window window, AutomationElement control, System.Drawing.Color background, System.Drawing.Color foreground)
    {
        var rectangle = control.BoundingRectangle;
        int backgrounds = 0, foregrounds = 0;
        for (int y = Math.Max(0, rectangle.Top-window.BoundingRectangle.Top); y < Math.Min(image.Height, rectangle.Bottom-window.BoundingRectangle.Top); y++)
        for (int x = Math.Max(0, rectangle.Left-window.BoundingRectangle.Left); x < Math.Min(image.Width, rectangle.Right-window.BoundingRectangle.Left); x++)
        {
            var color = image.GetPixel(x,y);
            if (color.ToArgb() == background.ToArgb()) backgrounds++;
            if (color.ToArgb() == foreground.ToArgb()) foregrounds++;
        }
        Assert.True(backgrounds > 20, control.Name + " background does not match WinForms RGB");
        Assert.True(foregrounds > 8, control.Name + " foreground does not match WinForms RGB");
    }

    sealed class DpiScope : IDisposable
    {
        readonly IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
        public void Dispose() => SetThreadDpiAwarenessContext(previous);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    static AutomationElement FindHealth(Window window)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(15))
        {
            foreach (var element in window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button).Or(cf.ByControlType(ControlType.Hyperlink))))
            {
                try { if (element.Name?.StartsWith("Feed Health:") == true) return element; }
                catch (System.Runtime.InteropServices.COMException) { }
                catch (FlaUI.Core.Exceptions.ElementNotAvailableException) { }
            }
            Thread.Sleep(100);
        }
        throw new InvalidOperationException("No accessible Feed Health action in the top bar");
    }
}
