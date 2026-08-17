using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed class G0UiAutomationSession : IDisposable
{
    readonly FlaUI.Core.Application _application;
    readonly UIA3Automation _automation;
    Window? _window;

    public G0UiAutomationSession(int processId)
    {
        _application = FlaUI.Core.Application.Attach(processId);
        _automation = new UIA3Automation();
    }

    public Window Window
        => _window ?? throw new InvalidOperationException("The main window has not been discovered.");

    public async Task<Window> WaitForMainWindowAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        Exception? lastFailure = null;
        while (!timeoutSource.IsCancellationRequested)
        {
            try
            {
                var window = _application.GetMainWindow(_automation, TimeSpan.FromMilliseconds(500));
                if (window is not null && window.IsEnabled && !string.IsNullOrWhiteSpace(window.Title))
                {
                    _window = window;
                    window.Focus();
                    return window;
                }
            }
            catch (Exception exception)
            {
                lastFailure = exception;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), timeoutSource.Token).ConfigureAwait(false);
        }

        throw new TimeoutException("The responsive IFM main window did not appear.", lastFailure);
    }

    public string ReadStatusText()
    {
        var element = Window.FindFirstDescendant(cf => cf.ByAutomationId("lblStatus"));
        if (element is null)
            throw new InvalidOperationException("The shell status control 'lblStatus' was not found.");
        return element.Name ?? string.Empty;
    }

    public IReadOnlyDictionary<string, bool> ReadToolbarEnabledState()
    {
        Dictionary<string, string> controls = new(StringComparer.Ordinal)
        {
            ["tradeButton"] = "Trade Orders",
            ["marketDataButton"] = "Market Data",
            ["fundButton"] = "Funds",
            ["referenceButton"] = "Reference",
            ["systemAdminButton"] = "System"
        };
        return controls.ToDictionary(
            pair => pair.Key,
            pair => FindDescendant(pair.Key, pair.Value)?.IsEnabled ?? false,
            StringComparer.Ordinal);
    }

    public G0EconomicCalendarUiState ReadEconomicCalendarState()
    {
        var date = FindDescendant("txtCalendarDate", "Economic calendar date")
            ?? throw new InvalidOperationException("Economic-calendar date control was not found.");
        var country = FindDescendant("ddlCountryCodes", "Economic calendar country")
            ?? throw new InvalidOperationException("Economic-calendar country control was not found.");
        var list = FindDescendant("lstEconomicCalendar", "Economic calendar list")
            ?? throw new InvalidOperationException("Economic-calendar list control was not found.");
        return new G0EconomicCalendarUiState(
            date.AsTextBox().Text ?? string.Empty,
            country.AsComboBox().SelectedItem?.Text ?? string.Empty,
            list.FindAllDescendants().Length);
    }

    public void RequestClose() => Window.Close();

    public void CaptureScreenshot(string path)
    {
        var rectangle = Window.BoundingRectangle;
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
            throw new InvalidOperationException("The main window has no capturable bounds.");
        using Bitmap bitmap = new((int)Math.Ceiling((double)rectangle.Width), (int)Math.Ceiling((double)rectangle.Height));
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(
            (int)Math.Floor((double)rectangle.Left),
            (int)Math.Floor((double)rectangle.Top),
            0,
            0,
            bitmap.Size,
            CopyPixelOperation.SourceCopy);
        bitmap.Save(path, ImageFormat.Png);
    }

    public void DumpAutomationTree(string path)
    {
        StringBuilder builder = new();
        AppendElement(builder, Window, 0);
        File.WriteAllText(path, builder.ToString());
    }

    public void Dispose()
    {
        _window = null;
        _automation.Dispose();
        _application.Dispose();
    }

    AutomationElement? FindDescendant(string automationId, string accessibleName)
    {
        try
        {
            var byId = Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (byId is not null)
                return byId;
        }
        catch
        {
            // Some WinForms providers do not expose AutomationId for hosted controls.
        }

        return Window.FindFirstDescendant(cf => cf.ByName(accessibleName));
    }

    static void AppendElement(StringBuilder builder, AutomationElement element, int depth)
    {
        builder.Append(' ', depth * 2)
            .Append(SafeRead(() => element.ControlType.ToString()))
            .Append(" Name=")
            .Append(SafeRead(() => element.Name))
            .Append(" AutomationId=")
            .Append(SafeRead(() => element.AutomationId))
            .Append(" Enabled=")
            .AppendLine(SafeRead(() => element.IsEnabled.ToString()));

        if (depth >= 20)
            return;
        try
        {
            foreach (var child in element.FindAllChildren())
                AppendElement(builder, child, depth + 1);
        }
        catch (Exception exception)
        {
            builder.Append(' ', (depth + 1) * 2)
                .Append("<children unavailable: ")
                .Append(exception.Message)
                .AppendLine(">");
        }
    }

    static string SafeRead(Func<string?> read)
    {
        try { return read() ?? string.Empty; }
        catch (Exception exception) { return $"<unavailable: {exception.Message}>"; }
    }
}

public sealed record G0EconomicCalendarUiState(
    string CalendarDate,
    string Country,
    int AutomationDescendantCount);
