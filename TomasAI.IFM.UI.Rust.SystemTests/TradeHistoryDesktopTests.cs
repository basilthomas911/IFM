using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

/// <summary>Exercises cached managed history bytes in the native trade monitor without live services.</summary>
public sealed class TradeHistoryDesktopTests
{
    [TradeHistoryDesktopFact]
    public void Cached_history_displays_and_updates_after_prepend_and_trim()
    {
        var root = Environment.GetEnvironmentVariable("IFM_RUST_REPO_ROOT")!;
        var fixture = Environment.GetEnvironmentVariable("IFM_TRADE_CACHE_FIXTURE")!;
        var output = Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
        Directory.CreateDirectory(output);
        // Generate the input in this test so test discovery order cannot select stale fixtures.
        new TradeRealtimeHotCacheTests().Generate_native_visual_fixture_from_managed_whole_cache_revisions();
        var executable = Path.Combine(root, "TomasAI.IFM.UI.Rust/target/debug/examples/trade_realtime_cache.exe");
        var start = new ProcessStartInfo(executable) { Arguments = "\"" + fixture + "\"",
            WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false, RedirectStandardError = true };
        start.Environment["IFM_TRADE_CACHE_INTERVAL_SECONDS"] = "10";
        using var app = Process.Start(start)!;
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        try
        {
            IntPtr handle = IntPtr.Zero;
            var watch = Stopwatch.StartNew();
            while (handle == IntPtr.Zero && watch.Elapsed < TimeSpan.FromSeconds(30))
            { if (app.HasExited) throw new InvalidOperationException("Native history fixture exited."); handle = NativeWindows.Find(app.Id); Thread.Sleep(50); }
            Assert.NotEqual(IntPtr.Zero, handle);
            SetForegroundWindow(handle);
            Window? window = null;
            while (window is null && watch.Elapsed < TimeSpan.FromSeconds(30))
            {
                try { window = automation.FromHandle(handle).AsWindow(); }
                catch (TimeoutException) { Thread.Sleep(100); }
            }
            Assert.NotNull(window);
            Wait(window, "ActionDateTime");
            Wait(window, "2026-10-21 11:01:40.000");
            window.Capture().Save(Path.Combine(output, "history-initial.png"));
            Wait(window, "2026-10-21 11:01:41.000");
            Assert.True(app.Responding);
            window.Capture().Save(Path.Combine(output, "history-updated.png"));
        }
        finally
        {
            if (!app.HasExited) { app.Kill(); app.WaitForExit(10000); }
            File.WriteAllText(Path.Combine(output, "history-native-stderr.txt"), app.StandardError.ReadToEnd());
        }
    }

    static void Wait(Window window, string name)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(20))
        {
            try { if (window.FindFirstDescendant(cf => cf.ByName(name)) is not null) return; }
            catch (System.Runtime.InteropServices.COMException) { }
            catch (TimeoutException) { }
            Thread.Sleep(50);
        }
        throw new TimeoutException("Native cached history did not display " + name);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr handle);
}

public sealed class TradeHistoryDesktopFactAttribute : FactAttribute
{
    public TradeHistoryDesktopFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IFM_TRADE_CACHE_FIXTURE")))
            Skip = "Requires managed-generated IFM_TRADE_CACHE_FIXTURE and the native trade_realtime_cache example.";
    }
}
