using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.UIA3;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed class G4UiAutomationSession : IDisposable
{
    const uint BmClick = 0x00F5;
    const uint WmClose = 0x0010;
    readonly FlaUI.Core.Application _application;
    readonly UIA3Automation _automation = new();

    public G4UiAutomationSession(int processId)
        => _application = FlaUI.Core.Application.Attach(processId);

    public IReadOnlyList<string> WindowTitles()
        => TopLevelWindows().Select(window => window.Title ?? string.Empty).ToArray();

    public async Task<Window> WaitForWindowAsync(
        string title,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var window = TopLevelWindows().FirstOrDefault(candidate =>
                string.Equals(candidate.Title, title, StringComparison.OrdinalIgnoreCase));
            if (window is not null)
                return window;
            await Task.Delay(100, timeoutSource.Token).ConfigureAwait(false);
        }

        throw new TimeoutException($"Window '{title}' did not appear. Observed [{string.Join(", ", WindowTitles())}].");
    }

    public Window? FindWindowStartingWith(string titlePrefix)
        => TopLevelWindows().FirstOrDefault(candidate =>
            (candidate.Title ?? string.Empty).StartsWith(titlePrefix, StringComparison.OrdinalIgnoreCase));

    public static string ReadText(Window window)
        => string.Join(
            Environment.NewLine,
            window.FindAllDescendants()
                .Select(element => SafeRead(() => element.Name))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal));

    public static void Dismiss(Window window, string? knownTitle = null)
    {
        ArgumentNullException.ThrowIfNull(window);
        var title = knownTitle ?? window.Title;
        var handle = FindWindow(null, title);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"Native window '{title}' was not found for dismissal.");
        var ok = FindWindowEx(handle, IntPtr.Zero, "Button", "OK");
        if (ok != IntPtr.Zero)
            _ = PostMessage(ok, BmClick, IntPtr.Zero, IntPtr.Zero);
        else
            _ = PostMessage(handle, WmClose, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Capture(Window window, string screenshotPath, string treePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(treePath)!);
        var rectangle = window.BoundingRectangle;
        if (rectangle.Width > 0 && rectangle.Height > 0)
        {
            try
            {
                using Bitmap bitmap = new(
                    (int)Math.Ceiling((double)rectangle.Width),
                    (int)Math.Ceiling((double)rectangle.Height));
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(
                    (int)Math.Floor((double)rectangle.Left),
                    (int)Math.Floor((double)rectangle.Top),
                    0,
                    0,
                    bitmap.Size,
                    CopyPixelOperation.SourceCopy);
                bitmap.Save(screenshotPath, ImageFormat.Png);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                FlaUI.Core.Capturing.Capture.Element(window).ToFile(screenshotPath);
            }
        }

        StringBuilder tree = new();
        Append(tree, window, 0);
        File.WriteAllText(treePath, tree.ToString());
    }

    public void Dispose()
    {
        _automation.Dispose();
        _application.Dispose();
    }

    Window[] TopLevelWindows()
    {
        try
        {
            return _application.GetAllTopLevelWindows(_automation);
        }
        catch
        {
            return [];
        }
    }

    static void Append(StringBuilder target, AutomationElement element, int depth)
    {
        target.Append(' ', depth * 2)
            .Append(SafeRead(() => element.ControlType.ToString()))
            .Append(" Name=")
            .Append(SafeRead(() => element.Name))
            .Append(" AutomationId=")
            .Append(SafeRead(() => element.AutomationId))
            .AppendLine();
        if (depth >= 20)
            return;
        try
        {
            foreach (var child in element.FindAllChildren())
                Append(target, child, depth + 1);
        }
        catch
        {
        }
    }

    static string SafeRead(Func<string?> read)
    {
        try { return read() ?? string.Empty; }
        catch { return string.Empty; }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
