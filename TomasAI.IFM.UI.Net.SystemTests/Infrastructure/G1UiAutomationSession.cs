using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

/// <summary>
/// Semantic UI Automation page object used by the G1 navigation and read-only audit.
/// </summary>
public sealed class G1UiAutomationSession : IDisposable
{
    static readonly IReadOnlyDictionary<string, string> ToolbarActions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Trade"] = "Trade Orders",
            ["MarketData"] = "Market Data",
            ["Funds"] = "Funds",
            ["Reference"] = "Reference",
            ["System"] = "System"
        };

    readonly FlaUI.Core.Application _application;
    readonly UIA3Automation _automation;
    readonly int _processId;
    Window? _mainWindow;
    bool _modalPending;

    public G1UiAutomationSession(int processId)
    {
        _processId = processId;
        _application = FlaUI.Core.Application.Attach(processId);
        _automation = new UIA3Automation();
    }

    public Window MainWindow
        => _mainWindow ?? throw new InvalidOperationException("The main window has not been discovered.");

    public async Task<Window> WaitForMainWindowAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var window = await WaitUntilAsync(
            () =>
            {
                var candidate = _application.GetMainWindow(_automation, TimeSpan.FromMilliseconds(500));
                return candidate is { IsEnabled: true } && !string.IsNullOrWhiteSpace(candidate.Title)
                    ? candidate
                    : null;
            },
            timeout,
            "The responsive IFM main window did not appear.",
            cancellationToken);
        _mainWindow = window;
        window.Focus();
        return window;
    }

    public async Task<G1ShellState> WaitForInitializedShellAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () =>
            {
                var toolbar = ReadToolbarEnabledState();
                if (toolbar.Values.Any(enabled => !enabled))
                    return null;
                var status = ReadStatusText();
                return !string.Equals(status, "Ready", StringComparison.OrdinalIgnoreCase)
                       && !string.IsNullOrWhiteSpace(status)
                    ? ReadShellState()
                    : null;
            },
            timeout,
            "The shell did not expose a status-console update and enabled actions.",
            cancellationToken);

    public IReadOnlyDictionary<string, bool> ReadToolbarEnabledState()
        => ToolbarActions.ToDictionary(
            pair => pair.Key,
            pair => FindDescendant(MainWindow, null, pair.Value)?.IsEnabled ?? false,
            StringComparer.Ordinal);

    public G2MarketDataFeedUiState ReadMarketDataFeedState()
    {
        var button = FindDescendant(MainWindow, "marketDataFeedButton", null)
            ?? FindDescendant(MainWindow, null, "Start Market Feed")
            ?? FindDescendant(MainWindow, null, "Stop Market Feed")
            ?? throw new InvalidOperationException("The shell market-data feed control was not found.");
        var action = button.Name;
        var isActive = string.Equals(action, "Stop Market Feed", StringComparison.Ordinal);
        if (!isActive && !string.Equals(action, "Start Market Feed", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected market-data feed action '{action}'.");
        return new G2MarketDataFeedUiState(isActive, action, button.IsEnabled);
    }

    public async Task<G2MarketDataFeedUiState> WaitForMarketDataFeedStateAsync(
        bool isActive,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () =>
            {
                var state = ReadMarketDataFeedState();
                return state.IsActive == isActive && state.IsEnabled ? state : null;
            },
            timeout,
            $"The shell did not show the market-data feed as {(isActive ? "active" : "inactive")}.",
            cancellationToken);

    public void InvokeMarketDataFeedAction()
    {
        var button = FindDescendant(MainWindow, "marketDataFeedButton", null)
            ?? FindDescendant(MainWindow, null, "Start Market Feed")
            ?? FindDescendant(MainWindow, null, "Stop Market Feed")
            ?? throw new InvalidOperationException("The shell market-data feed control was not found.");
        if (!button.IsEnabled)
            throw new InvalidOperationException($"The market-data feed action '{button.Name}' is disabled.");
        PostControlClick(button);
    }

    public string ReadStatusText()
    {
        var statusBar = FindDescendant(MainWindow, "statusBar", "statusStrip1")
            ?? throw new InvalidOperationException("The application status bar was not found.");
        var statusText = statusBar.FindAllDescendants()
            .FirstOrDefault(element => element.ControlType == ControlType.Text);
        return statusText?.Name ?? statusBar.Name ?? string.Empty;
    }

    public G1ShellState ReadShellState()
    {
        Dictionary<string, string> outlook = new(StringComparer.Ordinal)
        {
            ["MarketDirection"] = ReadText(MainWindow, "txtMarketTrendRT"),
            ["MarketVolatility"] = ReadText(MainWindow, "txtMarketVolatilityRT"),
            ["PriceDirection"] = ReadText(MainWindow, "txtMarketDirectionRT"),
            ["PriceVolatility"] = ReadText(MainWindow, "txtVixVolRT"),
            ["Open"] = ReadText(MainWindow, "txtOpenRT"),
            ["High"] = ReadText(MainWindow, "txtHighRT"),
            ["Low"] = ReadText(MainWindow, "txtLowRT"),
            ["Close"] = ReadText(MainWindow, "txtCloseRT"),
            ["Volume"] = ReadText(MainWindow, "txtVolumeRT"),
            ["PercentChange"] = ReadText(MainWindow, "txtPercentChangeRT")
        };
        return new G1ShellState(ReadStatusText(), ReadToolbarEnabledState(), outlook);
    }

    public G1StatusConsoleState ReadStatusConsoleState()
    {
        SelectTab(MainWindow, "Status Console");
        var list = RequireDescendant(MainWindow, "lstStatusConsole");
        var rows = ReadDataItemRows(list);
        return new G1StatusConsoleState(rows.Count, rows.FirstOrDefault() ?? string.Empty, rows);
    }

    public async Task<G1ChartState> ReadChartAsync(
        string tabName,
        string chartAutomationId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        BringToForeground(MainWindow);
        SelectTab(MainWindow, tabName);
        return await WaitUntilAsync(
            () =>
            {
                var graph = FindDescendant(MainWindow, chartAutomationId, null);
                if (graph is null)
                    return null;
                var points = graph.FindAllDescendants()
                    .Count(element => element.Name.StartsWith("Data Point", StringComparison.OrdinalIgnoreCase));
                var linePixelSpan = MeasureSeriesLinePixelSpan(graph, tabName);
                return points > 0 && linePixelSpan >= 20
                    ? new G1ChartState(tabName, graph.Name, points, linePixelSpan)
                    : null;
            },
            timeout,
            $"The {tabName} chart did not expose a visible rendered line.",
            cancellationToken);
    }

    static int MeasureSeriesLinePixelSpan(AutomationElement graph, string tabName)
    {
        BringToForeground(graph);
        var rectangle = graph.BoundingRectangle;
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
            return 0;
        using Bitmap bitmap = new(
            (int)Math.Ceiling((double)rectangle.Width),
            (int)Math.Ceiling((double)rectangle.Height));
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                (int)Math.Floor((double)rectangle.Left),
                (int)Math.Floor((double)rectangle.Top),
                0,
                0,
                bitmap.Size,
                CopyPixelOperation.SourceCopy);
        }

        var minimumX = bitmap.Width;
        var maximumX = -1;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y);
            var isSeriesPixel = string.Equals(tabName, "ES", StringComparison.Ordinal)
                ? color.R >= 180 && color.G >= 180 && color.B <= 140
                : color.R >= 160 && color.G <= 170 && color.B >= 160;
            if (!isSeriesPixel)
                continue;
            minimumX = Math.Min(minimumX, x);
            maximumX = Math.Max(maximumX, x);
        }
        return maximumX < minimumX ? 0 : maximumX - minimumX + 1;
    }

    public async Task<G1EconomicCalendarState> ReadEconomicCalendarViewsAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Dictionary<string, int> rows = new(StringComparer.Ordinal);
        var today = DateTime.Today;
        foreach (var view in new[] { "Today", "Yesterday", "Tomorrow", "This Week", "Next Week" })
        {
            SelectTab(MainWindow, view);
            var expectedDate = FormatCalendarDate(today, view);
            var rowCount = await WaitUntilAsync(
                () =>
                {
                    var list = FindDescendant(MainWindow, "lstEconomicCalendar", "Economic calendar list");
                    var renderedDate = ReadText(MainWindow, "txtCalendarDate");
                    return list is null || !string.Equals(renderedDate, expectedDate, StringComparison.Ordinal)
                        ? null
                        : CountDataItems(list);
                },
                timeout,
                $"The economic-calendar '{view}' view did not render.",
                cancellationToken);
            rows[view] = rowCount;
        }

        return new G1EconomicCalendarState(
            ReadText(MainWindow, "txtCalendarDate"),
            ReadComboItems(MainWindow, "ddlCountryCodes"),
            rows);
    }

    public void InvokeToolbarAction(string semanticName)
    {
        if (!ToolbarActions.TryGetValue(semanticName, out var accessibleName))
            throw new ArgumentOutOfRangeException(nameof(semanticName), semanticName, "Unknown shell action.");
        if (_modalPending)
            throw new InvalidOperationException("The prior modal navigation invocation is still active.");

        var button = FindDescendant(MainWindow, null, accessibleName)
            ?? throw new InvalidOperationException($"The '{accessibleName}' shell action was not found.");

        // ToolStrip InvokePattern remains blocked for the lifetime of ShowDialog, while a
        // global mouse click is unreliable in an unattended desktop. Post the equivalent
        // click to the owning WinForms control so the handler runs asynchronously.
        PostControlClick(button);
        _modalPending = true;
    }

    public async Task<Window> WaitForWindowAsync(
        string title,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () => FindNativeWindow(title)
                  ?? FindFocusedWindow(title)
                  ?? TopLevelWindows().SingleOrDefault(window =>
                      !ReferenceEquals(window, MainWindow)
                      && string.Equals(window.Title, title, StringComparison.OrdinalIgnoreCase)),
            timeout,
            $"The '{title}' window did not appear.",
            cancellationToken);

    public async Task<G1SelectorCatalog> ReadSelectorCatalogAsync(
        Window window,
        string selectorAutomationId,
        IReadOnlyDictionary<string, string> descriptionToViewAutomationId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var selector = RequireDescendant(window, selectorAutomationId).AsComboBox();
        var items = await WaitUntilAsync(
            () =>
            {
                var catalog = ReadComboItems(selector);
                return catalog.Count == descriptionToViewAutomationId.Count
                       && catalog.All(descriptionToViewAutomationId.ContainsKey)
                    ? catalog
                    : null;
            },
            timeout,
            $"Selector '{selectorAutomationId}' did not expose the queried catalog "
            + $"[{string.Join(", ", descriptionToViewAutomationId.Keys)}].",
            cancellationToken);

        List<G1SelectorViewState> views = [];
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            SelectComboIndex(selector, index);
            var viewId = descriptionToViewAutomationId[item];
            var view = await WaitUntilAsync(
                () => FindDescendant(window, viewId, null),
                timeout,
                $"Selector item '{item}' did not render '{viewId}'.",
                cancellationToken);
            views.Add(new G1SelectorViewState(
                item,
                viewId,
                ReadNamedDataCounts(view),
                ReadCommandStates(window)));
        }
        return new G1SelectorCatalog(items, views);
    }

    public async Task<G1FundWindowState> ReadFundWindowAsync(
        Window window,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var funds = await WaitUntilAsync(
            () =>
            {
                var items = ReadComboItems(window, "ddlFund");
                return items.Count > 0 ? items : null;
            },
            timeout,
            "The fund selector did not render any queried funds.",
            cancellationToken);
        var balance = await WaitUntilAsync(
            () =>
            {
                var value = ReadText(window, "txtFundBalance");
                return string.IsNullOrWhiteSpace(value) ? null : value;
            },
            timeout,
            "The selected fund balance did not render.",
            cancellationToken);
        var grid = RequireDescendant(window, "gridTransactions");
        return new G1FundWindowState(funds, balance, CountDataItems(grid), ReadText(window, "txtProfitLoss"));
    }

    public async Task<G1TradeWindowState> ReadTradeWindowAsync(
        Window window,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var funds = await WaitUntilAsync(
            () =>
            {
                var items = ReadComboItems(window, "ddlFund");
                return items.Count > 0 ? items : null;
            },
            timeout,
            "The trade-order fund selector did not render any queried funds.",
            cancellationToken);
        return new G1TradeWindowState(
            funds,
            CountDataItems(RequireDescendant(window, "lstTradeOrders")),
            CountDataItems(RequireDescendant(window, "lstTrades")),
            ReadCommandStates(window));
    }

    public IReadOnlyList<string> ReadComboItems(AutomationElement root, string automationId)
    {
        var combo = RequireDescendant(root, automationId).AsComboBox();
        return ReadComboItems(combo);
    }

    public IReadOnlyList<string> FindUnexpectedWindowTitles(params string[] expectedTitles)
    {
        var expected = expectedTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        expected.Add(MainWindow.Title);
        return TopLevelWindows()
            .Select(window => window.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title) && !expected.Contains(title))
            .ToArray();
    }

    public async Task CloseWindowAsync(Window window, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var title = window.Title;
        window.Close();
        _modalPending = false;
        await WaitUntilAsync(
            () => TopLevelWindows().All(candidate =>
                !string.Equals(candidate.Title, title, StringComparison.OrdinalIgnoreCase))
                ? "closed"
                : null,
            timeout,
            $"The '{title}' window did not close.",
            cancellationToken);
    }

    public void CloseAllSecondaryWindows()
    {
        foreach (var window in TopLevelWindows()
                     .Where(candidate => !string.Equals(
                         candidate.Title,
                         MainWindow.Title,
                         StringComparison.OrdinalIgnoreCase))
                     .Reverse())
        {
            try { window.Close(); }
            catch { /* Best-effort recovery after a captured navigation failure. */ }
        }
        _modalPending = false;
    }

    public void RequestMainWindowClose() => MainWindow.Close();

    public void CaptureScreenshot(AutomationElement element, string path)
    {
        var rectangle = element.BoundingRectangle;
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
            throw new InvalidOperationException("The selected window has no capturable bounds.");
        BringToForeground(element);
        rectangle = element.BoundingRectangle;
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
        bitmap.Save(path, ImageFormat.Png);
    }

    static void BringToForeground(AutomationElement element)
    {
        AutomationElement? host = element;
        IntPtr windowHandle = IntPtr.Zero;
        while (host is not null)
        {
            try
            {
                var candidate = new IntPtr(host.Properties.NativeWindowHandle.Value);
                if (candidate != IntPtr.Zero)
                    windowHandle = candidate;
            }
            catch { /* Virtual children do not necessarily expose a native handle. */ }
            host = host.Parent;
        }
        if (windowHandle == IntPtr.Zero)
            return;

        ShowWindow(windowHandle, SwRestore);
        SetForegroundWindow(windowHandle);
        Thread.Sleep(100);
    }

    public void DumpAutomationTree(AutomationElement element, string path)
    {
        StringBuilder builder = new();
        AppendElement(builder, element, 0);
        File.WriteAllText(path, builder.ToString());
    }

    static void PostControlClick(AutomationElement element)
    {
        var rectangle = element.BoundingRectangle;
        NativePoint point = new()
        {
            X = (int)Math.Round((double)(rectangle.Left + rectangle.Width / 2)),
            Y = (int)Math.Round((double)(rectangle.Top + rectangle.Height / 2))
        };
        AutomationElement? host = element;
        IntPtr windowHandle = IntPtr.Zero;
        while (host is not null && windowHandle == IntPtr.Zero)
        {
            try { windowHandle = new IntPtr(host.Properties.NativeWindowHandle.Value); }
            catch { /* ToolStrip items are virtual children; continue to the owning control. */ }
            host = host.Parent;
        }
        if (windowHandle == IntPtr.Zero || !ScreenToClient(windowHandle, ref point))
            throw new InvalidOperationException("The navigation control did not expose a clickable WinForms window handle.");

        var parameter = new IntPtr((point.Y << 16) | (point.X & 0xFFFF));
        if (!PostMessage(windowHandle, WmLeftButtonDown, new IntPtr(MkLeftButton), parameter)
            || !PostMessage(windowHandle, WmLeftButtonUp, IntPtr.Zero, parameter))
            throw new InvalidOperationException(
                $"The navigation click could not be posted (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    public IReadOnlyList<string> CaptureTopLevelEvidence(
        string screenshotDirectory,
        string automationTreeDirectory,
        string prefix)
    {
        List<string> captured = [];
        var index = 0;
        foreach (var window in TopLevelWindows())
        {
            var safeTitle = string.Concat(window.Title.Select(character =>
                Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
            if (string.IsNullOrWhiteSpace(safeTitle))
                safeTitle = "window";
            var name = $"{prefix}-{index++:00}-{safeTitle}";
            var screenshot = Path.Combine(screenshotDirectory, name + ".png");
            var tree = Path.Combine(automationTreeDirectory, name + ".txt");
            try
            {
                CaptureScreenshot(window, screenshot);
                captured.Add(screenshot);
            }
            catch { /* An unavailable screenshot must not suppress the automation tree. */ }
            try
            {
                DumpAutomationTree(window, tree);
                captured.Add(tree);
            }
            catch { /* A provider failure must not suppress other window evidence. */ }
        }
        return captured;
    }

    public void Dispose()
    {
        _mainWindow = null;
        _automation.Dispose();
        _application.Dispose();
    }

    IReadOnlyList<Window> TopLevelWindows()
    {
        List<Window> windows = [];
        foreach (var handle in EnumerateTopLevelWindowHandles(_processId))
        {
            if (_modalPending
                && string.Equals(ReadWindowTitle(handle), MainWindow.Title, StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var element = _automation.FromHandle(handle);
                if (element.ControlType == ControlType.Window)
                    windows.Add(element.AsWindow());
            }
            catch { /* A window can close between native enumeration and UIA materialization. */ }
        }
        if (_mainWindow is not null)
        {
            try { windows.AddRange(_mainWindow.ModalWindows); }
            catch { /* The main window may be closing while the provider enumerates owned dialogs. */ }
        }
        try
        {
            windows.AddRange(_automation.GetDesktop()
                .FindAllChildren(condition => condition
                    .ByProcessId(_processId)
                    .And(condition.ByControlType(ControlType.Window)))
                .Select(element => element.AsWindow()));
        }
        catch { /* Preserve any application/owned-window results during provider transitions. */ }
        if (!_modalPending)
        {
            try { windows.AddRange(_application.GetAllTopLevelWindows(_automation)); }
            catch { /* Process.MainWindowHandle can be transiently unavailable during lifecycle transitions. */ }
        }
        return DistinctWindows(windows);
    }

    Window? FindNativeWindow(string title)
    {
        foreach (var handle in EnumerateTopLevelWindowHandles(_processId))
        {
            if (!string.Equals(ReadWindowTitle(handle), title, StringComparison.OrdinalIgnoreCase))
                continue;
            try { return _automation.FromHandle(handle).AsWindow(); }
            catch { /* The matching window may still be initializing its UIA provider. */ }
        }
        return null;
    }

    Window? FindFocusedWindow(string title)
    {
        try
        {
            var element = _automation.FocusedElement();
            while (element is not null)
            {
                if (element.ControlType == ControlType.Window
                    && string.Equals(element.Name, title, StringComparison.OrdinalIgnoreCase))
                    return element.AsWindow();
                element = element.Parent;
            }
        }
        catch { /* Focus may transition while the modal initializes. */ }
        return null;
    }

    static IReadOnlyList<Window> DistinctWindows(IEnumerable<Window> windows)
        => windows
            .DistinctBy(window => SafeRead(() => window.Properties.NativeWindowHandle.Value.ToString()))
            .ToArray();

    static IReadOnlyList<IntPtr> EnumerateTopLevelWindowHandles(int processId)
    {
        List<IntPtr> handles = [];
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var ownerProcessId);
            if (ownerProcessId == (uint)processId)
                handles.Add(handle);
            return true;
        }, IntPtr.Zero);
        return handles;
    }

    static string ReadWindowTitle(IntPtr windowHandle)
    {
        var length = GetWindowTextLength(windowHandle);
        if (length <= 0)
            return string.Empty;
        StringBuilder title = new(length + 1);
        return GetWindowText(windowHandle, title, title.Capacity) > 0
            ? title.ToString()
            : string.Empty;
    }

    static IReadOnlyDictionary<string, bool> ReadCommandStates(AutomationElement root)
    {
        Dictionary<string, bool> states = new(StringComparer.Ordinal);
        foreach (var id in new[]
                 {
                     "btnAdd", "btnChange", "btnRemove", "btnImport", "btnClose", "btnAdjust",
                     "btnCreateFund", "btnCreateOrder", "btnDeleteOrder", "btnLoadOrder",
                     "btnCompleteOrder", "btnAddTrade", "btnRemoveTrade", "btnEndOfDay", "btnSubmitOrder",
                     "btnRun"
                 })
        {
            var control = FindDescendant(root, id, null);
            if (control is not null)
                states[id] = control.IsEnabled;
        }
        return states;
    }

    static IReadOnlyDictionary<string, int> ReadNamedDataCounts(AutomationElement root)
    {
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (var id in new[]
                 {
                     "lstFuturesContractIds", "lstFuturesOptionContractIds", "gridYieldCurveRates",
                     "lstCalendarEvents", "lstLookupTypeNames", "lstLookupTypeShortCodes",
                     "clbDatabases", "lbStatusMessages"
                 })
        {
            var control = FindDescendant(root, id, null);
            if (control is not null)
                counts[id] = CountDataItems(control);
        }
        return counts;
    }

    static IReadOnlyList<string> ReadDataItemRows(AutomationElement root)
        => root.FindAllDescendants()
            .Where(IsDataItem)
            .Select(element => string.Join(" | ", element.FindAllDescendants()
                .Where(IsText)
                .Select(descendant => descendant.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))))
            .Where(row => !string.IsNullOrWhiteSpace(row))
            .ToArray();

    static IReadOnlyList<string> ReadComboItems(FlaUI.Core.AutomationElements.ComboBox combo)
    {
        var name = combo.Name ?? string.Empty;
        const string catalogMarker = "catalog:";
        var catalogIndex = name.LastIndexOf(catalogMarker, StringComparison.OrdinalIgnoreCase);
        if (catalogIndex >= 0)
        {
            var namedCatalog = name[(catalogIndex + catalogMarker.Length)..];
            var namedItems = namedCatalog.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (namedItems.Length > 0)
                return namedItems;
        }

        try
        {
            var catalog = combo.Properties.HelpText.Value;
            if (!string.IsNullOrWhiteSpace(catalog))
                return catalog.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        }
        catch { /* Fall through to provider item discovery. */ }

        IReadOnlyList<string> items;
        try
        {
            combo.Expand();
            items = combo.Items
                .Select(item => item.Text)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }
        finally
        {
            try { combo.Collapse(); }
            catch { /* The provider may already have collapsed after item discovery. */ }
        }
        if (items.Count > 0)
            return items;

        var selected = combo.SelectedItem?.Text;
        if (!string.IsNullOrWhiteSpace(selected))
            return [selected];
        var separator = name.LastIndexOf(':');
        var namedSelection = separator >= 0 ? name[(separator + 1)..].Trim() : string.Empty;
        return string.IsNullOrWhiteSpace(namedSelection) ? [] : [namedSelection];
    }

    static void SelectComboIndex(FlaUI.Core.AutomationElements.ComboBox combo, int index)
    {
        var comboHandle = new IntPtr(combo.Properties.NativeWindowHandle.Value);
        if (comboHandle == IntPtr.Zero
            || SendMessage(comboHandle, CbSetCurrentSelection, new IntPtr(index), IntPtr.Zero).ToInt64() < 0)
            throw new InvalidOperationException($"Combo-box item {index} could not be selected.");

        var parentHandle = GetParent(comboHandle);
        var controlId = GetDlgCtrlID(comboHandle);
        var notification = new IntPtr((CbnSelectionChanged << 16) | (controlId & 0xFFFF));
        if (parentHandle == IntPtr.Zero
            || !PostMessage(parentHandle, WmCommand, notification, comboHandle))
            throw new InvalidOperationException($"Combo-box item {index} selection could not be notified.");
    }

    static int CountDataItems(AutomationElement root)
        => root.FindAllDescendants().Count(IsDataItem);

    static bool IsDataItem(AutomationElement element)
    {
        try { return element.ControlType is ControlType.DataItem or ControlType.ListItem; }
        catch { return false; }
    }

    static bool IsText(AutomationElement element)
    {
        try { return element.ControlType == ControlType.Text; }
        catch { return false; }
    }

    static void SelectTab(AutomationElement root, string tabName)
    {
        var tab = root.FindFirstDescendant(condition =>
            condition.ByControlType(ControlType.TabItem).And(condition.ByName(tabName)))
            ?? throw new InvalidOperationException($"The '{tabName}' tab was not found.");
        tab.AsTabItem().Select();
    }

    static string ReadText(AutomationElement root, string automationId)
    {
        var element = RequireDescendant(root, automationId);
        try
        {
            var value = element.AsTextBox().Text ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        catch { }
        try
        {
            var description = element.Properties.HelpText.Value;
            if (!string.IsNullOrWhiteSpace(description))
                return description;
        }
        catch { return element.Name ?? string.Empty; }
        return string.Empty;
    }

    static string FormatCalendarDate(DateTime today, string view)
    {
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var date = view switch
        {
            "Yesterday" => today.AddDays(-1),
            "Tomorrow" => today.AddDays(1),
            "This Week" => today.Date.AddDays(-daysSinceMonday),
            "Next Week" => today.Date.AddDays(-daysSinceMonday + 7),
            _ => today
        };
        return $"{date.DayOfWeek}, {date:MMMM} {date:dd}, {date:yyyy}";
    }

    static AutomationElement RequireDescendant(AutomationElement root, string automationId)
        => FindDescendant(root, automationId, null)
           ?? throw new InvalidOperationException($"The '{automationId}' control was not found.");

    static AutomationElement? FindDescendant(
        AutomationElement root,
        string? automationId,
        string? accessibleName)
    {
        if (!string.IsNullOrWhiteSpace(automationId))
        {
            try
            {
                var byId = root.FindFirstDescendant(condition => condition.ByAutomationId(automationId));
                if (byId is not null)
                    return byId;
            }
            catch
            {
                // Some hosted WinForms controls do not expose AutomationId.
            }
        }
        return string.IsNullOrWhiteSpace(accessibleName)
            ? null
            : root.FindFirstDescendant(condition => condition.ByName(accessibleName));
    }

    static async Task<T> WaitUntilAsync<T>(
        Func<T?> read,
        TimeSpan timeout,
        string timeoutMessage,
        CancellationToken cancellationToken)
        where T : class
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        Exception? lastFailure = null;
        while (!timeoutSource.IsCancellationRequested)
        {
            try
            {
                var result = read();
                if (result is not null)
                    return result;
            }
            catch (Exception exception)
            {
                lastFailure = exception;
            }
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        throw new TimeoutException(timeoutMessage, lastFailure);
    }

    static async Task<int> WaitUntilAsync(
        Func<int?> read,
        TimeSpan timeout,
        string timeoutMessage,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var result = read();
            if (result.HasValue)
                return result.Value;
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        throw new TimeoutException(timeoutMessage);
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool PrintWindow(IntPtr windowHandle, IntPtr destinationDeviceContext, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ShowWindow(IntPtr windowHandle, int command);

    delegate bool EnumWindowCallback(IntPtr windowHandle, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maximumLength);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowTextLength(IntPtr windowHandle);

    const uint WmLeftButtonDown = 0x0201;
    const uint WmLeftButtonUp = 0x0202;
    const uint WmCommand = 0x0111;
    const uint CbSetCurrentSelection = 0x014E;
    const int CbnSelectionChanged = 1;
    const int MkLeftButton = 0x0001;
    const int SwRestore = 9;

    [StructLayout(LayoutKind.Sequential)]
    struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ScreenToClient(IntPtr windowHandle, ref NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wordParameter, IntPtr longParameter);

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr windowHandle, uint message, IntPtr wordParameter, IntPtr longParameter);

    [DllImport("user32.dll")]
    static extern IntPtr GetParent(IntPtr windowHandle);

    [DllImport("user32.dll")]
    static extern int GetDlgCtrlID(IntPtr windowHandle);
}

public sealed record G1ShellState(
    string Status,
    IReadOnlyDictionary<string, bool> Toolbar,
    IReadOnlyDictionary<string, string> MarketOutlook);

public sealed record G2MarketDataFeedUiState(
    bool IsActive,
    string Action,
    bool IsEnabled);

public sealed record G1StatusConsoleState(
    int RowCount,
    string FirstRow,
    IReadOnlyList<string> Rows);

public sealed record G1ChartState(
    string Tab,
    string AccessibleName,
    int DataPointCount,
    int LinePixelSpan);

public sealed record G1EconomicCalendarState(
    string CalendarDate,
    IReadOnlyList<string> Countries,
    IReadOnlyDictionary<string, int> RowsByView);

public sealed record G1SelectorCatalog(
    IReadOnlyList<string> Items,
    IReadOnlyList<G1SelectorViewState> Views);

public sealed record G1SelectorViewState(
    string Selection,
    string ViewAutomationId,
    IReadOnlyDictionary<string, int> DataCounts,
    IReadOnlyDictionary<string, bool> CommandStates);

public sealed record G1FundWindowState(
    IReadOnlyList<string> Funds,
    string Balance,
    int TransactionRows,
    string ProfitLoss);

public sealed record G1TradeWindowState(
    IReadOnlyList<string> Funds,
    int OrderRows,
    int TradeRows,
    IReadOnlyDictionary<string, bool> CommandStates);
