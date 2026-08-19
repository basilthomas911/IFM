using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

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

    public async Task SelectMarketDataEditorAsync(
        Window window,
        string selectorItem,
        string editorAutomationId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var selector = RequireDescendant(window, "ddlMarketDataSelector").AsComboBox();
        var items = await WaitUntilAsync(
            () =>
            {
                var catalog = ReadComboItems(selector);
                return catalog.Contains(selectorItem, StringComparer.Ordinal) ? catalog : null;
            },
            timeout,
            $"The Market Data selector did not expose '{selectorItem}'.",
            cancellationToken);
        SelectComboIndex(selector, items
            .Select((item, index) => (item, index))
            .Single(pair => string.Equals(pair.item, selectorItem, StringComparison.Ordinal))
            .index);
        await WaitUntilAsync(
            () =>
            {
                var editor = FindDescendant(window, editorAutomationId, null);
                var add = FindDescendant(window, "btnAdd", null);
                return editor is not null && add is { IsEnabled: true } ? editor : null;
            },
            timeout,
            $"The '{editorAutomationId}' editor did not finish loading.",
            cancellationToken);
    }

    public async Task SelectReferenceEditorAsync(
        Window window,
        string selectorItem,
        string editorAutomationId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var selector = RequireDescendant(window, "ddlReferenceDataSelector").AsComboBox();
        var items = await WaitUntilAsync(
            () =>
            {
                var catalog = ReadComboItems(selector);
                return catalog.Contains(selectorItem, StringComparer.Ordinal) ? catalog : null;
            },
            timeout,
            $"The Reference selector did not expose '{selectorItem}'.",
            cancellationToken);
        SelectComboIndex(selector, items
            .Select((item, index) => (item, index))
            .Single(pair => string.Equals(pair.item, selectorItem, StringComparison.Ordinal))
            .index);
        await WaitUntilAsync(
            () =>
            {
                var editor = FindDescendant(window, editorAutomationId, null);
                var add = FindDescendant(window, "btnAdd", null);
                return editor is not null && add is { IsEnabled: true }
                    ? editor
                    : null;
            },
            timeout,
            $"The '{editorAutomationId}' editor did not finish loading.",
            cancellationToken);
    }

    public async Task<G2SecuritiesEditorUiState> AddFuturesContractAsync(
        Window window,
        G2SecuritiesFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectMarketDataEditorAsync(
            window, fixture.FuturesDefinitionDescription, "FuturesContractEditorControl", timeout, cancellationToken);
        ClickEnabled(window, "btnAdd");
        await WaitForEnabledAsync(window, "dtmLastTradeDate", timeout, cancellationToken);
        SetDate(window, "dtmLastTradeDate", fixture.MaturityDate);
        SetCombo(window, "ddlSymbol", fixture.SymbolIndex);
        SetCombo(window, "ddlSecurityType", fixture.FuturesSecurityTypeIndex);
        SetCombo(window, "ddlCurrency", fixture.CurrencyIndex);
        SetCombo(window, "ddlExchange", fixture.ExchangeIndex);
        SetCombo(window, "ddlMultiplier", fixture.MultiplierIndex);
        SetCombo(window, "ddlCurrentlyTraded", 1);
        ClickEnabled(window, "btnAdd");
        return await WaitForSecuritiesStateAsync(
            window,
            "FuturesContractEditorControl",
            "lstFuturesContractIds",
            fixture.FuturesContractId,
            null,
            present: true,
            timeout,
            cancellationToken);
    }

    public async Task<G2SecuritiesEditorUiState> ChangeFuturesContractAsync(
        Window window,
        G2SecuritiesFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectMarketDataEditorAsync(
            window, fixture.FuturesDefinitionDescription, "FuturesContractEditorControl", timeout, cancellationToken);
        SelectListItem(window, "lstFuturesContractIds", fixture.FuturesContractId);
        ClickEnabled(window, "btnChange");
        await WaitForEnabledAsync(window, "txtDescription", timeout, cancellationToken);
        SetText(window, "txtDescription", fixture.FuturesChangedDescription);
        ClickEnabled(window, "btnChange");
        return await WaitForSecuritiesStateAsync(
            window,
            "FuturesContractEditorControl",
            "lstFuturesContractIds",
            fixture.FuturesContractId,
            null,
            present: true,
            timeout,
            cancellationToken);
    }

    public async Task<G2SecuritiesEditorUiState> RemoveFuturesContractAsync(
        Window window,
        G2SecuritiesFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectMarketDataEditorAsync(
            window, fixture.FuturesDefinitionDescription, "FuturesContractEditorControl", timeout, cancellationToken);
        SelectListItem(window, "lstFuturesContractIds", fixture.FuturesContractId);
        ClickEnabled(window, "btnRemove");
        await ConfirmAsync("Remove Futures Contract", timeout, cancellationToken);
        return await WaitForSecuritiesStateAsync(
            window,
            "FuturesContractEditorControl",
            "lstFuturesContractIds",
            fixture.FuturesContractId,
            null,
            present: false,
            timeout,
            cancellationToken);
    }

    public async Task<G2SecuritiesEditorUiState> AddFuturesOptionContractAsync(
        Window window,
        G2SecuritiesFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectMarketDataEditorAsync(
            window, fixture.OptionDefinitionDescription, "FuturesOptionContractEditorControl", timeout, cancellationToken);
        ClickEnabled(window, "btnAdd");
        await WaitForEnabledAsync(window, "dtmContractMonth", timeout, cancellationToken);
        SetDate(window, "dtmContractMonth", fixture.MaturityDate);
        SetCombo(window, "ddlSymbol", fixture.SymbolIndex);
        SetCombo(window, "ddlOptionType", fixture.CallOptionTypeIndex);
        SetCombo(window, "ddlSecurityType", fixture.OptionSecurityTypeIndex);
        SetCombo(window, "ddlCurrency", fixture.CurrencyIndex);
        SetCombo(window, "ddlExchange", fixture.ExchangeIndex);
        SetCombo(window, "ddlMultiplier", fixture.MultiplierIndex);
        SetText(window, "txtStrikePrice", fixture.StrikePrice.ToString());
        SetText(window, "txtDescription", fixture.OptionAddedDescription);
        ClickEnabled(window, "btnAdd");
        return await WaitForSecuritiesStateAsync(
            window,
            "FuturesOptionContractEditorControl",
            "lstFuturesOptionContractIds",
            fixture.OptionContractId,
            null,
            present: true,
            timeout,
            cancellationToken);
    }

    public async Task<G2SecuritiesEditorUiState> ChangeFuturesOptionContractAsync(
        Window window,
        G2SecuritiesFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectMarketDataEditorAsync(
            window, fixture.OptionDefinitionDescription, "FuturesOptionContractEditorControl", timeout, cancellationToken);
        SelectListItem(window, "lstFuturesOptionContractIds", fixture.OptionContractId);
        ClickEnabled(window, "btnChange");
        await WaitForEnabledAsync(window, "txtDescription", timeout, cancellationToken);
        SetText(window, "txtDescription", fixture.OptionChangedDescription);
        ClickEnabled(window, "btnChange");
        return await WaitForSecuritiesStateAsync(
            window,
            "FuturesOptionContractEditorControl",
            "lstFuturesOptionContractIds",
            fixture.OptionContractId,
            null,
            present: true,
            timeout,
            cancellationToken);
    }

    public async Task<G2SecuritiesEditorUiState> ReloadFuturesContractAsync(
        Window window,
        G2SecuritiesFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectMarketDataEditorAsync(
            window, fixture.OptionDefinitionDescription, "FuturesOptionContractEditorControl", timeout, cancellationToken);
        await SelectMarketDataEditorAsync(
            window, fixture.FuturesDefinitionDescription, "FuturesContractEditorControl", timeout, cancellationToken);
        return await WaitForSecuritiesStateAsync(
            window,
            "FuturesContractEditorControl",
            "lstFuturesContractIds",
            fixture.FuturesContractId,
            fixture.FuturesChangedDescription,
            present: true,
            timeout,
            cancellationToken);
    }

    public async Task<G2SecuritiesEditorUiState> ReloadFuturesOptionContractAsync(
        Window window,
        G2SecuritiesFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectMarketDataEditorAsync(
            window, fixture.FuturesDefinitionDescription, "FuturesContractEditorControl", timeout, cancellationToken);
        await SelectMarketDataEditorAsync(
            window, fixture.OptionDefinitionDescription, "FuturesOptionContractEditorControl", timeout, cancellationToken);
        return await WaitForSecuritiesStateAsync(
            window,
            "FuturesOptionContractEditorControl",
            "lstFuturesOptionContractIds",
            fixture.OptionContractId,
            fixture.OptionChangedDescription,
            present: true,
            timeout,
            cancellationToken);
    }

    public async Task<G2SecuritiesEditorUiState> RemoveFuturesOptionContractAsync(
        Window window,
        G2SecuritiesFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectMarketDataEditorAsync(
            window, fixture.OptionDefinitionDescription, "FuturesOptionContractEditorControl", timeout, cancellationToken);
        SelectListItem(window, "lstFuturesOptionContractIds", fixture.OptionContractId);
        ClickEnabled(window, "btnRemove");
        await ConfirmAsync("Remove Futures Option Contract", timeout, cancellationToken);
        return await WaitForSecuritiesStateAsync(
            window,
            "FuturesOptionContractEditorControl",
            "lstFuturesOptionContractIds",
            fixture.OptionContractId,
            null,
            present: false,
            timeout,
            cancellationToken);
    }

    public async Task<G2YieldCurveEditorUiState> AddYieldCurveRateAsync(
        Window window,
        G2YieldCurveFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectMarketDataEditorAsync(
            window, fixture.DefinitionDescription, "YieldCurveRateEditorControl", timeout, cancellationToken);
        PostButtonClick(window, "btnAdd");
        var dialog = await WaitForWindowAsync("Add Yield Curve Rate", timeout, cancellationToken);
        await WaitForEnabledAsync(dialog, "dtmValueDate", timeout, cancellationToken);
        await SetDateAsync(dialog, "dtmValueDate", fixture.ManualDate, timeout, cancellationToken);
        SetYieldCurveRateFields(dialog, fixture.AddedRate);
        await WaitForEnabledAsync(dialog, "btnSave", timeout, cancellationToken);
        InvokeButtonEnabled(dialog, "btnSave");
        return await WaitForYieldCurveStateAsync(
            window, fixture.ManualDate, fixture.AddedRate, present: true, timeout, cancellationToken);
    }

    public async Task<G2YieldCurveEditorUiState> ChangeYieldCurveRateAsync(
        Window window,
        G2YieldCurveFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        SelectYieldCurveRow(window, fixture.ManualDate);
        PostButtonClick(window, "btnChange");
        var dialog = await WaitForWindowAsync("Change Yield Curve Rate", timeout, cancellationToken);
        await WaitForEnabledAsync(dialog, "txtOneMonth", timeout, cancellationToken);
        SetYieldCurveRateFields(dialog, fixture.ChangedRate);
        InvokeButtonEnabled(dialog, "btnSave");
        return await WaitForYieldCurveStateAsync(
            window, fixture.ManualDate, fixture.ChangedRate, present: true, timeout, cancellationToken);
    }

    public async Task<G2YieldCurveEditorUiState> RemoveYieldCurveRateAsync(
        Window window,
        G2YieldCurveFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        SelectYieldCurveRow(window, fixture.ManualDate);
        PostButtonClick(window, "btnRemove");
        await ConfirmAsync("Remove Yield Curve Rate", timeout, cancellationToken);
        return await WaitForYieldCurveStateAsync(
            window, fixture.ManualDate, expectedRate: null, present: false, timeout, cancellationToken);
    }

    public async Task<G2YieldCurveEditorUiState> ImportYieldCurveRatesAsync(
        Window window,
        G2YieldCurveFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectMarketDataEditorAsync(
            window, fixture.DefinitionDescription, "YieldCurveRateEditorControl", timeout, cancellationToken);
        await SetDateAsync(window, "dtmImportDate", fixture.ImportDate, timeout, cancellationToken);
        PostButtonClick(window, "btnImport");
        await WaitUntilAsync(
            () => FindDescendant(window, "dtmImportDate", null) is { IsEnabled: false } ? "busy" : null,
            timeout,
            "The yield-curve import operation did not enter its busy state.",
            cancellationToken);
        await WaitForEnabledAsync(window, "dtmImportDate", timeout, cancellationToken);
        var period = fixture.ImportDate.Year.ToString(CultureInfo.InvariantCulture);
        if (ReadComboItems(window, "ddlTimePeriod").Contains(period, StringComparer.Ordinal))
            await SelectYieldCurvePeriodAsync(window, period, timeout, cancellationToken);
        return ReadYieldCurveState(window, fixture.ImportDate);
    }

    public async Task<G2YieldCurveEditorUiState> ReloadYieldCurveRateAsync(
        Window window,
        G2YieldCurveFixture fixture,
        DateOnly valueDate,
        YieldCurveRateReadModel? expectedRate,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return await WaitForYieldCurveStateAsync(
            window, valueDate, expectedRate, present, timeout, cancellationToken);
    }

    public async Task<G2EconomicCalendarEditorUiState> AddEconomicCalendarAsync(
        Window window,
        G2EconomicCalendarFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectReferenceEditorAsync(
            window, fixture.DefinitionDescription, "EconomicCalendarEditorView", timeout, cancellationToken);
        await SetDateAsync(window, "dtmEventDate", fixture.ManualDate, timeout, cancellationToken);
        PostButtonClick(window, "btnAdd");
        await WaitForEnabledAsync(window, "txtEventName", timeout, cancellationToken);
        await SelectComboValueAsync(window, "ddlCountryCodes", fixture.CountryCode, timeout, cancellationToken);
        SetEconomicCalendarFields(window, fixture.AddedCalendar, includeEventName: true);
        PostButtonClick(window, "btnAdd");
        return await WaitForEconomicCalendarStateAsync(
            window, fixture.ManualDate, fixture.AddedCalendar, present: true, timeout, cancellationToken);
    }

    public async Task<G2EconomicCalendarEditorUiState> ChangeEconomicCalendarAsync(
        Window window,
        G2EconomicCalendarFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        SelectEconomicCalendarEvent(window, fixture.AddedCalendar.EventName);
        PostButtonClick(window, "btnChange");
        await WaitForEnabledAsync(window, "txtActual", timeout, cancellationToken);
        SetEconomicCalendarFields(window, fixture.ChangedCalendar, includeEventName: false);
        PostButtonClick(window, "btnChange");
        return await WaitForEconomicCalendarStateAsync(
            window, fixture.ManualDate, fixture.ChangedCalendar, present: true, timeout, cancellationToken);
    }

    public async Task<G2EconomicCalendarEditorUiState> RemoveEconomicCalendarAsync(
        Window window,
        G2EconomicCalendarFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        SelectEconomicCalendarEvent(window, fixture.ChangedCalendar.EventName);
        PostButtonClick(window, "btnRemove");
        await ConfirmAsync("Remove Economic Calendar", timeout, cancellationToken);
        return await WaitForEconomicCalendarStateAsync(
            window, fixture.ManualDate, fixture.ChangedCalendar, present: false, timeout, cancellationToken);
    }

    public async Task<G2EconomicCalendarEditorUiState> ImportEconomicCalendarsAsync(
        Window window,
        G2EconomicCalendarFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SetDateAsync(window, "dtmEventDate", fixture.ImportDate, timeout, cancellationToken);
        await SelectComboValueAsync(window, "ddlCountryCodes", fixture.CountryCode, timeout, cancellationToken);
        PostButtonClick(window, "btnImport");
        await WaitUntilAsync(
            () => FindDescendant(window, "dtmEventDate", null) is { IsEnabled: false } ? "busy" : null,
            timeout,
            "The economic-calendar import operation did not enter its busy state.",
            cancellationToken);
        await DismissDialogAsync("Economic Calendar Import", "OK", timeout, cancellationToken);
        await WaitForEnabledAsync(window, "dtmEventDate", timeout, cancellationToken);
        return ReadEconomicCalendarState(window, fixture.ImportDate);
    }

    public async Task<G2EconomicCalendarEditorUiState> ReloadEconomicCalendarAsync(
        Window window,
        DateOnly date,
        string countryCode,
        EconomicCalendarReadModel? expected,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SetDateAsync(window, "dtmEventDate", date, timeout, cancellationToken);
        await SelectComboValueAsync(window, "ddlCountryCodes", countryCode, timeout, cancellationToken);
        return await WaitForEconomicCalendarStateAsync(
            window, date, expected, present, timeout, cancellationToken);
    }

    public async Task<G2LookupTypeEditorUiState> AddLookupTypeAsync(
        Window window,
        G2LookupTypeFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectReferenceEditorAsync(
            window, fixture.DefinitionDescription, "LookupTypeEditorView", timeout, cancellationToken);
        PostButtonClick(window, "btnAdd");
        await WaitUntilAsync(
            () => FindDescendant(window, "lstLookupTypeNames", null) is { IsEnabled: false } ? "editing" : null,
            timeout,
            "The lookup-type editor did not enter add mode.",
            cancellationToken);
        SetText(window, "txtLookupTypeName", fixture.AddedLookupType.LookupTypeName);
        SetText(window, "txtShortCode", fixture.AddedLookupType.ShortCode);
        SetText(window, "txtDescription", fixture.AddedLookupType.Description);
        PostButtonClick(window, "btnAdd");
        return await WaitForLookupTypeStateAsync(
            window, fixture.AddedLookupType, present: true, timeout, cancellationToken);
    }

    public async Task<G2LookupTypeEditorUiState> ChangeLookupTypeAsync(
        Window window,
        G2LookupTypeFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        SelectLookupType(
            window,
            fixture.AddedLookupType.LookupTypeName,
            fixture.AddedLookupType.ShortCode);
        PostButtonClick(window, "btnChange");
        await WaitUntilAsync(
            () => FindDescendant(window, "lstLookupTypeNames", null) is { IsEnabled: false } ? "editing" : null,
            timeout,
            "The lookup-type editor did not enter change mode.",
            cancellationToken);
        SetText(window, "txtShortCode", fixture.ChangedLookupType.ShortCode);
        SetText(window, "txtDescription", fixture.ChangedLookupType.Description);
        PostButtonClick(window, "btnChange");
        return await WaitForLookupTypeStateAsync(
            window, fixture.ChangedLookupType, present: true, timeout, cancellationToken);
    }

    public async Task<G2LookupTypeEditorUiState> RemoveLookupTypeAsync(
        Window window,
        G2LookupTypeFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        SelectLookupType(
            window,
            fixture.ChangedLookupType.LookupTypeName,
            fixture.ChangedLookupType.ShortCode);
        PostButtonClick(window, "btnRemove");
        await ConfirmAsync("Remove Lookup Type", timeout, cancellationToken);
        return await WaitForLookupTypeStateAsync(
            window, fixture.ChangedLookupType, present: false, timeout, cancellationToken);
    }

    public async Task<G2CreatedFundUiState> CreateFundAsync(
        Window tradeWindow,
        G2FundFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        PostButtonClick(tradeWindow, "btnCreateFund");
        var dialog = await WaitForWindowAsync("Create Fund", timeout, cancellationToken);
        var fundIdText = await WaitUntilAsync(
            () => int.TryParse(ReadText(dialog, "txtFundId"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var value) && value > 0
                ? value.ToString(CultureInfo.InvariantCulture)
                : null,
            timeout,
            "The Create Fund dialog did not resolve a positive fund identifier.",
            cancellationToken);
        SetText(dialog, "txtFundName", fixture.FundName);
        SetText(dialog, "txtDescription", fixture.FundDescription);
        SetText(dialog, "txtInitialBalance", fixture.InitialBalance.ToString(CultureInfo.CurrentCulture));
        PostButtonClick(dialog, "btnSave");
        await WaitUntilAsync(
            () => TopLevelWindows().All(window =>
                    !string.Equals(window.Title, "Create Fund", StringComparison.OrdinalIgnoreCase))
                ? "closed"
                : null,
            timeout,
            "The Create Fund dialog did not close after submission.",
            cancellationToken);
        return new G2CreatedFundUiState(
            int.Parse(fundIdText, CultureInfo.InvariantCulture),
            fixture.FundName,
            fixture.InitialBalance);
    }

    public async Task<G2FundTransactionUiState> SelectFundAsync(
        Window fundWindow,
        string fundName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectComboValueAsync(fundWindow, "ddlFund", fundName, timeout, cancellationToken);
        return await WaitUntilAsync(
            () => ReadFundTransactionState(fundWindow, fundName),
            timeout,
            $"Fund '{fundName}' did not render its balance and transaction state.",
            cancellationToken);
    }

    public async Task<G2FundTransactionUiState> CreateCashTransactionAsync(
        Window fundWindow,
        string fundName,
        FundTransactionType transactionType,
        decimal amount,
        string description,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectFundAsync(fundWindow, fundName, timeout, cancellationToken);
        var buttonId = transactionType switch
        {
            FundTransactionType.CashDeposit => "btnDeposit",
            FundTransactionType.CashWithdrawal => "btnWithdraw",
            _ => throw new ArgumentOutOfRangeException(nameof(transactionType), transactionType, null)
        };
        var title = transactionType == FundTransactionType.CashDeposit
            ? "Create Cash Deposit"
            : "Create Cash Withdrawal";
        PostButtonClick(fundWindow, buttonId);
        var dialog = await WaitForWindowAsync(title, timeout, cancellationToken);
        await WaitForEnabledAsync(dialog, "txtAmount", timeout, cancellationToken);
        SetText(dialog, "txtAmount", amount.ToString(CultureInfo.CurrentCulture));
        SetText(dialog, "txtDescription", description);
        await WaitForEnabledAsync(dialog, "btnSave", timeout, cancellationToken);
        PostButtonClick(dialog, "btnSave");
        await WaitUntilAsync(
            () => TopLevelWindows().All(window =>
                    !string.Equals(window.Title, title, StringComparison.OrdinalIgnoreCase))
                ? "closed"
                : null,
            timeout,
            $"The '{title}' dialog did not close after its terminal event.",
            cancellationToken);
        return await WaitUntilAsync(
            () =>
            {
                var state = ReadFundTransactionState(fundWindow, fundName);
                return state?.Rows.Any(row => row.Contains(description, StringComparison.Ordinal)) == true
                    ? state
                    : null;
            },
            timeout,
            $"Fund '{fundName}' did not visibly render transaction '{description}'.",
            cancellationToken);
    }

    public async Task<G2FundTransactionUiState> WaitForFundTransactionStateAsync(
        Window fundWindow,
        string fundName,
        string[] requiredDescriptions,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () =>
            {
                var state = ReadFundTransactionState(fundWindow, fundName);
                return state is not null && requiredDescriptions.All(description =>
                    state.Rows.Any(row => row.Contains(description, StringComparison.Ordinal)))
                    ? state
                    : null;
            },
            timeout,
            $"Fund '{fundName}' did not render the required transaction history.",
            cancellationToken);

    public async Task<G2TradeOrderUiState> CreateFundOrderAsync(
        Window tradeWindow,
        string fundName,
        G2OrderTradeFixture fixture,
        DateOnly tradeDate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectTradeFundAsync(tradeWindow, fundName, timeout, cancellationToken);
        PostButtonClick(tradeWindow, "btnCreateOrder");
        var dialog = await WaitForWindowAsync("Create Fund Order", timeout, cancellationToken);
        var orderId = await WaitUntilAsync(
            () => int.TryParse(ReadText(dialog, "txtOrderId"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null as int?,
            timeout,
            "The Create Fund Order dialog did not resolve a positive order identifier.",
            cancellationToken);
        var contracts = await WaitUntilAsync(
            () =>
            {
                var items = ReadComboItems(dialog, "ddlBaseContracts");
                return items.Count > 0 ? items : null;
            },
            timeout,
            "The Create Fund Order dialog did not expose a base contract.",
            cancellationToken);
        var contractId = contracts.FirstOrDefault(item =>
                item.StartsWith(fixture.PreferredBaseSymbol, StringComparison.OrdinalIgnoreCase))
            ?? contracts[0];
        await SelectComboValueAsync(
            dialog, "ddlBaseContracts", contractId, timeout, cancellationToken);
        await WaitForEnabledAsync(dialog, "dtpTradeDate", timeout, cancellationToken);
        SetDate(dialog, "dtpTradeDate", tradeDate);
        SetDate(dialog, "dtpMaturityDate", tradeDate.AddDays(fixture.MaturityDays));
        SetText(dialog, "txtReference", fixture.OrderReference);
        await WaitForEnabledAsync(dialog, "btnSave", timeout, cancellationToken);
        PostButtonClick(dialog, "btnSave");
        await WaitForWindowClosedAsync("Create Fund Order", timeout, cancellationToken);
        await WaitForListItemAsync(
            tradeWindow, "lstTradeOrders", orderId, present: true, timeout, cancellationToken);
        SelectListItemById(tradeWindow, "lstTradeOrders", orderId);
        return await WaitForTradeOrderStateAsync(
            tradeWindow,
            fundName,
            orderId,
            orderPresent: true,
            fixture.OrderReference,
            tradeId: null,
            tradePresent: false,
            expectedTradeReference: null,
            expectedTradeState: null,
            timeout,
            cancellationToken);
    }

    public async Task<G2TradeOrderUiState> AddFundOrderTradeAsync(
        Window tradeWindow,
        string fundName,
        int orderId,
        G2OrderTradeFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectTradeFundAsync(tradeWindow, fundName, timeout, cancellationToken);
        SelectListItemById(tradeWindow, "lstTradeOrders", orderId);
        await WaitForEnabledAsync(tradeWindow, "btnAddTrade", timeout, cancellationToken);
        PostButtonClick(tradeWindow, "btnAddTrade");
        var dialog = await WaitForWindowAsync("Add Trade", timeout, cancellationToken);
        var tradeId = await WaitUntilAsync(
            () => int.TryParse(ReadText(dialog, "txtTradeId"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var value) && value > 0 ? value : null as int?,
            timeout,
            "The Add Trade dialog did not resolve a positive trade identifier.",
            cancellationToken);
        await SelectComboValueAsync(
            dialog, "ddlTradeType", fixture.TradeType.ToString(), timeout, cancellationToken);
        var symbols = await WaitUntilAsync(
            () =>
            {
                var items = ReadComboItems(dialog, "ddlBaseSymbol");
                return items.Count > 0 ? items : null;
            },
            timeout,
            "The Add Trade dialog did not expose a base symbol.",
            cancellationToken);
        var symbol = symbols.FirstOrDefault(item =>
                item.Contains(fixture.PreferredBaseSymbol, StringComparison.OrdinalIgnoreCase))
            ?? symbols[0];
        await SelectComboValueAsync(dialog, "ddlBaseSymbol", symbol, timeout, cancellationToken);
        SetText(dialog, "txtReference", fixture.TradeReference);
        PostButtonClick(dialog, "btnSave");
        await WaitForWindowClosedAsync("Add Trade", timeout, cancellationToken);
        await WaitForListItemAsync(
            tradeWindow, "lstTrades", tradeId, present: true, timeout, cancellationToken);
        SelectListItemById(tradeWindow, "lstTrades", tradeId);
        return await WaitForTradeOrderStateAsync(
            tradeWindow,
            fundName,
            orderId,
            orderPresent: true,
            fixture.OrderReference,
            tradeId,
            tradePresent: true,
            fixture.TradeReference,
            fixture.InitialTradeState,
            timeout,
            cancellationToken);
    }

    public async Task<G2TradeOrderUiState> ChangeFundOrderTradeStateAsync(
        Window tradeWindow,
        string fundName,
        int orderId,
        int tradeId,
        G2OrderTradeFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectTradeFundAsync(tradeWindow, fundName, timeout, cancellationToken);
        SelectListItemById(tradeWindow, "lstTradeOrders", orderId);
        SelectListItemById(tradeWindow, "lstTrades", tradeId);
        await SelectComboValueAsync(
            tradeWindow,
            "ddlTradeState",
            fixture.ChangedTradeState.ToStringFast(),
            timeout,
            cancellationToken);
        PostButtonClick(tradeWindow, "btnChangeTradeState");
        return await WaitForTradeOrderStateAsync(
            tradeWindow,
            fundName,
            orderId,
            orderPresent: true,
            fixture.OrderReference,
            tradeId,
            tradePresent: true,
            fixture.TradeReference,
            fixture.ChangedTradeState,
            timeout,
            cancellationToken);
    }

    public async Task<G2TradeOrderUiState> RemoveFundOrderTradeAsync(
        Window tradeWindow,
        string fundName,
        int orderId,
        int tradeId,
        G2OrderTradeFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectTradeFundAsync(tradeWindow, fundName, timeout, cancellationToken);
        SelectListItemById(tradeWindow, "lstTradeOrders", orderId);
        SelectListItemById(tradeWindow, "lstTrades", tradeId);
        PostButtonClick(tradeWindow, "btnRemoveTrade");
        return await WaitForTradeOrderStateAsync(
            tradeWindow,
            fundName,
            orderId,
            orderPresent: true,
            fixture.OrderReference,
            tradeId,
            tradePresent: false,
            expectedTradeReference: null,
            expectedTradeState: null,
            timeout,
            cancellationToken);
    }

    public async Task<G2TradeOrderUiState> RemoveFundOrderAsync(
        Window tradeWindow,
        string fundName,
        int orderId,
        G2OrderTradeFixture fixture,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectTradeFundAsync(tradeWindow, fundName, timeout, cancellationToken);
        SelectListItemById(tradeWindow, "lstTradeOrders", orderId);
        PostButtonClick(tradeWindow, "btnDeleteOrder");
        var dialog = await WaitForWindowAsync("Delete Fund Order", timeout, cancellationToken);
        PostButtonClick(dialog, "btnYes");
        await WaitForWindowClosedAsync("Delete Fund Order", timeout, cancellationToken);
        return await WaitForTradeOrderStateAsync(
            tradeWindow,
            fundName,
            orderId,
            orderPresent: false,
            expectedOrderReference: null,
            tradeId: null,
            tradePresent: false,
            expectedTradeReference: null,
            expectedTradeState: null,
            timeout,
            cancellationToken);
    }

    async Task SelectTradeFundAsync(
        Window tradeWindow,
        string fundName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await SelectComboValueAsync(tradeWindow, "ddlFund", fundName, timeout, cancellationToken);
        await WaitForEnabledAsync(tradeWindow, "btnCreateOrder", timeout, cancellationToken);
    }

    async Task<G2TradeOrderUiState> WaitForTradeOrderStateAsync(
        Window tradeWindow,
        string fundName,
        int orderId,
        bool orderPresent,
        string? expectedOrderReference,
        int? tradeId,
        bool tradePresent,
        string? expectedTradeReference,
        TradeState? expectedTradeState,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () =>
            {
                var state = ReadTradeOrderState(tradeWindow, fundName);
                if (state is null)
                    return null;
                var orderRow = state.OrderRows.SingleOrDefault(row => HasLeadingId(row, orderId));
                if ((orderRow is not null) != orderPresent)
                    return null;
                if (expectedOrderReference is not null
                    && (orderRow is null || !orderRow.Contains(expectedOrderReference, StringComparison.Ordinal)))
                    return null;
                if (tradeId is not null)
                {
                    var tradeRow = state.TradeRows.SingleOrDefault(row => HasLeadingId(row, tradeId.Value));
                    if ((tradeRow is not null) != tradePresent)
                        return null;
                    if (expectedTradeReference is not null
                        && (tradeRow is null || !tradeRow.Contains(expectedTradeReference, StringComparison.Ordinal)))
                        return null;
                    if (expectedTradeState is not null
                        && (tradeRow is null || !tradeRow.Contains(
                            $"| {expectedTradeState.Value} |", StringComparison.Ordinal)))
                        return null;
                }
                return state;
            },
            timeout,
            $"The Trade Orders editor did not render order {orderId} as {(orderPresent ? "present" : "absent")}"
            + (tradeId is null ? string.Empty : $" and trade {tradeId} as {(tradePresent ? "present" : "absent")}"),
            cancellationToken);

    G2TradeOrderUiState? ReadTradeOrderState(Window tradeWindow, string fundName)
    {
        var selector = RequireDescendant(tradeWindow, "ddlFund").AsComboBox();
        if (!string.Equals(ReadSelectedComboValue(selector), fundName, StringComparison.Ordinal))
            return null;
        return new G2TradeOrderUiState(
            fundName,
            ReadSelectedListId(tradeWindow, "lstTradeOrders"),
            ReadSelectedListId(tradeWindow, "lstTrades"),
            ReadSemanticListRows(tradeWindow, "lstTradeOrders"),
            ReadSemanticListRows(tradeWindow, "lstTrades"),
            ReadCommandStates(tradeWindow));
    }

    async Task WaitForListItemAsync(
        AutomationElement root,
        string automationId,
        int id,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () => ReadListItemIds(root, automationId).Contains(id) == present ? id : null as int?,
            timeout,
            $"The '{automationId}' list did not render {id} as {(present ? "present" : "absent")}.",
            cancellationToken);

    async Task WaitForWindowClosedAsync(
        string title,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () => TopLevelWindows().All(window =>
                    !string.Equals(window.Title, title, StringComparison.OrdinalIgnoreCase))
                ? title
                : null,
            timeout,
            $"The '{title}' window did not close.",
            cancellationToken);

    static void SelectListItemById(AutomationElement root, string automationId, int id)
    {
        var list = RequireDescendant(root, automationId).AsListBox();
        var item = list.Items.SingleOrDefault(candidate =>
                int.TryParse(candidate.Text.Split(' ', '|')[0], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var value) && value == id)
            ?? throw new InvalidOperationException($"The '{automationId}' list does not contain ID {id}.");
        item.Select();
    }

    static int? ReadSelectedListId(AutomationElement root, string automationId)
    {
        var selected = RequireDescendant(root, automationId).AsListBox().SelectedItem?.Text;
        return int.TryParse(selected?.Split(' ', '|')[0], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    static IReadOnlyList<int> ReadListItemIds(AutomationElement root, string automationId)
        => RequireDescendant(root, automationId).AsListBox().Items
            .Select(item => int.TryParse(item.Text.Split(' ', '|')[0], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToArray();

    static IReadOnlyList<string> ReadSemanticListRows(AutomationElement root, string automationId)
    {
        var list = RequireDescendant(root, automationId);
        string description = string.Empty;
        try { description = list.Properties.HelpText.Value ?? string.Empty; }
        catch { /* Fall back to the accessible name and visible items. */ }
        if (string.IsNullOrWhiteSpace(description))
        {
            var name = list.Name ?? string.Empty;
            var marker = name.IndexOf("rows:", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0)
                description = name[(marker + "rows:".Length)..].Trim();
        }
        var rows = description.Split(" || ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (rows.Length > 0)
            return rows;
        return list.AsListBox().Items.Select(item => item.Text).ToArray();
    }

    static bool HasLeadingId(string row, int id)
        => row.StartsWith(id.ToString(CultureInfo.InvariantCulture) + " |", StringComparison.Ordinal)
           || string.Equals(row, id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    G2FundTransactionUiState? ReadFundTransactionState(Window fundWindow, string fundName)
    {
        var selector = RequireDescendant(fundWindow, "ddlFund").AsComboBox();
        if (!string.Equals(ReadSelectedComboValue(selector), fundName, StringComparison.Ordinal))
            return null;
        var balance = ReadText(fundWindow, "txtFundBalance");
        if (string.IsNullOrWhiteSpace(balance))
            return null;
        return new G2FundTransactionUiState(
            fundName,
            balance,
            ReadDataGridRows(RequireDescendant(fundWindow, "gridTransactions")));
    }

    public string ReadStatusText()
    {
        var statusBar = FindDescendant(MainWindow, "statusBar", "statusStrip1")
            ?? throw new InvalidOperationException("The application status bar was not found.");
        var statusText = statusBar.FindAllDescendants()
            .FirstOrDefault(element => element.ControlType == ControlType.Text);
        return statusText?.Name ?? statusBar.Name ?? string.Empty;
    }

    async Task<G2SecuritiesEditorUiState> WaitForSecuritiesStateAsync(
        Window window,
        string editorAutomationId,
        string listAutomationId,
        string contractId,
        string? expectedDescription,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () =>
            {
                var editor = FindDescendant(window, editorAutomationId, null);
                var listElement = FindDescendant(window, listAutomationId, null);
                if (editor is null || listElement is null)
                    return null;
                var list = listElement.AsListBox();
                var ids = list.Items.Select(item => item.Text).ToArray();
                var contains = ids.Contains(contractId, StringComparer.Ordinal);
                if (contains != present)
                    return null;
                if (present)
                {
                    list.Select(contractId);
                    var renderedDescription = ReadText(window, "txtDescription");
                    if (expectedDescription is not null
                        && !string.Equals(renderedDescription, expectedDescription, StringComparison.Ordinal))
                        return null;
                }
                return new G2SecuritiesEditorUiState(
                    editorAutomationId,
                    ids,
                    list.SelectedItem?.Text ?? string.Empty,
                    FindDescendant(window, "txtDescription", null) is null
                        ? string.Empty
                        : ReadText(window, "txtDescription"));
            },
            timeout,
            $"The {editorAutomationId} did not render contract '{contractId}' as {(present ? "present" : "absent")}",
            cancellationToken);

    async Task WaitForEnabledAsync(
        AutomationElement root,
        string automationId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () => FindDescendant(root, automationId, null) is { IsEnabled: true } element ? element : null,
            timeout,
            $"The '{automationId}' control did not become enabled.",
            cancellationToken);

    async Task ConfirmAsync(string title, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var dialog = await WaitForWindowAsync(title, timeout, cancellationToken);
        var yes = FindDescendant(dialog, null, "Yes")
            ?? throw new InvalidOperationException($"The '{title}' confirmation did not expose a Yes action.");
        PostControlClick(yes);
        await WaitUntilAsync(
            () => TopLevelWindows().All(window =>
                    !string.Equals(window.Title, title, StringComparison.OrdinalIgnoreCase))
                ? "closed"
                : null,
            timeout,
            $"The '{title}' confirmation did not close.",
            cancellationToken);
    }

    async Task DismissDialogAsync(
        string title,
        string actionName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var dialog = await WaitForWindowAsync(title, timeout, cancellationToken);
        var action = FindDescendant(dialog, null, actionName)
            ?? throw new InvalidOperationException($"The '{title}' dialog did not expose a '{actionName}' action.");
        PostControlClick(action);
        await WaitUntilAsync(
            () => TopLevelWindows().All(window =>
                    !string.Equals(window.Title, title, StringComparison.OrdinalIgnoreCase))
                ? "closed"
                : null,
            timeout,
            $"The '{title}' dialog did not close.",
            cancellationToken);
    }

    static void ClickEnabled(AutomationElement root, string automationId)
    {
        var control = RequireDescendant(root, automationId);
        if (!control.IsEnabled)
            throw new InvalidOperationException($"The '{automationId}' command is disabled.");
        PostControlClick(control);
    }

    static void InvokeButtonEnabled(AutomationElement root, string automationId)
    {
        var button = RequireDescendant(root, automationId).AsButton();
        if (!button.IsEnabled)
            throw new InvalidOperationException($"The '{automationId}' command is disabled.");
        button.Invoke();
    }

    static void PostButtonClick(AutomationElement root, string automationId)
    {
        var button = RequireDescendant(root, automationId).AsButton();
        if (!button.IsEnabled)
            throw new InvalidOperationException($"The '{automationId}' command is disabled.");
        var handle = new IntPtr(button.Properties.NativeWindowHandle.Value);
        if (handle == IntPtr.Zero || !PostMessage(handle, BmClick, IntPtr.Zero, IntPtr.Zero))
            throw new InvalidOperationException(
                $"The '{automationId}' WinForms button could not receive BM_CLICK "
                + $"(Win32 error {Marshal.GetLastWin32Error()}).");
    }

    static void SetText(AutomationElement root, string automationId, string value)
    {
        var textBox = RequireDescendant(root, automationId).AsTextBox();
        if (!textBox.IsEnabled)
            throw new InvalidOperationException($"The '{automationId}' text input is disabled.");
        if (textBox.Patterns.Value.IsSupported)
            textBox.Patterns.Value.Pattern.SetValue(value);
        else
            textBox.Enter(value);
        if (!string.Equals(textBox.Text, value, StringComparison.Ordinal))
            throw new InvalidOperationException($"The '{automationId}' text input did not retain the entered value.");
    }

    static void SetDate(AutomationElement root, string automationId, DateOnly value)
    {
        var picker = RequireDescendant(root, automationId).AsDateTimePicker();
        if (!picker.IsEnabled)
            throw new InvalidOperationException($"The '{automationId}' date input is disabled.");
        var pickerHandle = FindVisibleDateTimePickerHandle(root, picker);
        if (pickerHandle == IntPtr.Zero)
            throw new InvalidOperationException($"The '{automationId}' date input does not expose a native handle.");
        var selectedDate = ReadAccessibleDate(picker, automationId);
        if (selectedDate == value)
            return;

        if (picker.Patterns.LegacyIAccessible.IsSupported)
        {
            picker.Patterns.LegacyIAccessible.Pattern.SetValue(
                value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Thread.Sleep(50);
            if (ReadAccessibleDate(picker, automationId) == value)
                return;
        }

        if (picker.Patterns.Value.IsSupported)
        {
            picker.Patterns.Value.Pattern.SetValue(
                value.ToString("yyyy-MMM-dd", CultureInfo.CurrentCulture));
            Thread.Sleep(50);
            if (ReadAccessibleDate(picker, automationId) == value)
                return;
        }

        var nativeDate = new NativeSystemTime
        {
            Year = checked((ushort)value.Year),
            Month = checked((ushort)value.Month),
            Day = checked((ushort)value.Day)
        };
        SendMessage(pickerHandle, DtmSetSystemTime, new IntPtr(GdtValid), ref nativeDate);
        Thread.Sleep(50);
        picker = FindDescendant(root, automationId, null)?.AsDateTimePicker() ?? picker;
        if (ReadAccessibleDate(picker, automationId) == value)
            return;

        if (picker.Patterns.ExpandCollapse.IsSupported)
            picker.Patterns.ExpandCollapse.Pattern.Expand();
        else
            OpenDateTimePickerDropDown(pickerHandle);
        var calendarHandle = WaitForVisibleMonthCalendar(root, pickerHandle, TimeSpan.FromSeconds(3));
        if (calendarHandle == IntPtr.Zero)
            throw new InvalidOperationException($"The '{automationId}' calendar popup did not open.");

        var monthDelta = (value.Year - selectedDate.Year) * 12 + value.Month - selectedDate.Month;
        var monthKey = monthDelta >= 0 ? VkPageDown : VkPageUp;
        for (var index = 0; index < Math.Abs(monthDelta); index++)
            SendKey(calendarHandle, monthKey);

        var intermediateDay = Math.Min(selectedDate.Day, DateTime.DaysInMonth(value.Year, value.Month));
        var dayDelta = value.Day - intermediateDay;
        var dayKey = dayDelta >= 0 ? VkRight : VkLeft;
        for (var index = 0; index < Math.Abs(dayDelta); index++)
            SendKey(calendarHandle, dayKey);
        SendKey(calendarHandle, VkEnter);
    }

    static DateOnly ReadAccessibleDate(
        FlaUI.Core.AutomationElements.DateTimePicker picker,
        string automationId)
    {
        var name = picker.Name;
        var separator = name.IndexOf(',', StringComparison.Ordinal);
        var dateText = separator >= 0 ? name[(separator + 1)..].Trim() : name;
        if (DateTime.TryParse(dateText, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var date)
            || DateTime.TryParse(dateText, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
            return DateOnly.FromDateTime(date);
        throw new InvalidOperationException(
            $"The '{automationId}' date input did not expose a parseable accessible value; name='{name}'.");
    }

    static void OpenDateTimePickerDropDown(IntPtr pickerHandle)
    {
        if (!GetClientRect(pickerHandle, out var rectangle))
            throw new InvalidOperationException(
                $"The native DateTimePicker client area could not be read (Win32 error {Marshal.GetLastWin32Error()}).");
        var x = Math.Max(0, rectangle.Right - 8);
        var y = Math.Max(0, (rectangle.Bottom - rectangle.Top) / 2);
        var parameter = new IntPtr((y << 16) | (x & 0xFFFF));
        SendMessage(pickerHandle, WmLeftButtonDown, new IntPtr(MkLeftButton), parameter);
        SendMessage(pickerHandle, WmLeftButtonUp, IntPtr.Zero, parameter);
    }

    static IntPtr FindVisibleDateTimePickerHandle(
        AutomationElement root,
        FlaUI.Core.AutomationElements.DateTimePicker picker)
    {
        var directHandle = new IntPtr(picker.Properties.NativeWindowHandle.Value);
        if (IsDateTimePickerWindow(directHandle))
            return directHandle;

        var hostHandle = new IntPtr(root.Properties.NativeWindowHandle.Value);
        if (hostHandle == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr result = IntPtr.Zero;
        EnumChildWindows(hostHandle, (handle, _) =>
        {
            if (IsWindowVisible(handle) && IsDateTimePickerWindow(handle))
            {
                result = handle;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    static IntPtr WaitForVisibleMonthCalendar(
        AutomationElement root,
        IntPtr pickerHandle,
        TimeSpan timeout)
    {
        GetWindowThreadProcessId(pickerHandle, out var pickerProcessId);
        var stopwatch = Stopwatch.StartNew();
        do
        {
            var hostHandle = new IntPtr(root.Properties.NativeWindowHandle.Value);
            var result = FindVisibleWindowByClass(hostHandle, pickerProcessId, "SysMonthCal32");
            if (result != IntPtr.Zero)
                return result;
            Thread.Sleep(25);
        } while (stopwatch.Elapsed < timeout);
        return IntPtr.Zero;
    }

    static IntPtr FindVisibleWindowByClass(IntPtr hostHandle, uint processId, string classNameSegment)
    {
        IntPtr result = IntPtr.Zero;
        if (hostHandle != IntPtr.Zero)
        {
            EnumChildWindows(hostHandle, (handle, _) =>
            {
                if (IsWindowVisible(handle) && WindowClassContains(handle, classNameSegment))
                {
                    result = handle;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
        }
        if (result != IntPtr.Zero)
            return result;

        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var candidateProcessId);
            if (candidateProcessId == processId
                && IsWindowVisible(handle)
                && WindowClassContains(handle, classNameSegment))
            {
                result = handle;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    static void SendKey(IntPtr handle, int virtualKey)
    {
        SendMessage(handle, WmKeyDown, new IntPtr(virtualKey), IntPtr.Zero);
        SendMessage(handle, WmKeyUp, new IntPtr(virtualKey), IntPtr.Zero);
    }

    static bool IsDateTimePickerWindow(IntPtr handle)
        => WindowClassContains(handle, "SysDateTimePick32");

    static bool WindowClassContains(IntPtr handle, string classNameSegment)
    {
        if (handle == IntPtr.Zero)
            return false;
        StringBuilder className = new(256);
        return GetClassName(handle, className, className.Capacity) > 0
               && className.ToString().Contains(classNameSegment, StringComparison.Ordinal);
    }

    static Task SetDateAsync(
        AutomationElement root,
        string automationId,
        DateOnly value,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetDate(root, automationId, value);
        return Task.CompletedTask;
    }

    static void SetCombo(AutomationElement root, string automationId, int index)
    {
        var combo = RequireDescendant(root, automationId).AsComboBox();
        if (!combo.IsEnabled)
            throw new InvalidOperationException($"The '{automationId}' selector is disabled.");
        SelectComboIndex(combo, index);
    }

    async Task SelectComboValueAsync(
        AutomationElement root,
        string automationId,
        string value,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await WaitForEnabledAsync(root, automationId, timeout, cancellationToken);
        var combo = RequireDescendant(root, automationId).AsComboBox();
        var items = await WaitUntilAsync(
            () =>
            {
                var catalog = ReadComboItems(combo);
                return catalog.Contains(value, StringComparer.Ordinal) ? catalog : null;
            },
            timeout,
            $"The '{automationId}' selector did not expose '{value}'.",
            cancellationToken);
        var targetIndex = items
            .Select((item, index) => (item, index))
            .Single(pair => string.Equals(pair.item, value, StringComparison.Ordinal))
            .index;

        var selectedThroughAutomation = false;
        try
        {
            combo.Expand();
            var target = combo.Items.SingleOrDefault(item =>
                string.Equals(item.Text, value, StringComparison.Ordinal));
            if (target is not null)
            {
                target.Select();
                selectedThroughAutomation = true;
            }
        }
        finally
        {
            try { combo.Collapse(); }
            catch { /* The provider may already have collapsed after selection. */ }
        }

        if (!selectedThroughAutomation)
            SelectComboIndex(combo, targetIndex);

        await WaitUntilAsync(
            () => string.Equals(ReadSelectedComboValue(combo), value, StringComparison.Ordinal)
                ? value
                : null,
            timeout,
            $"The '{automationId}' selector did not select '{value}'.",
            cancellationToken);
    }

    static void SelectListItem(AutomationElement root, string automationId, string value)
    {
        var list = RequireDescendant(root, automationId).AsListBox();
        if (!list.IsEnabled)
            throw new InvalidOperationException($"The '{automationId}' list is disabled.");
        list.Select(value);
    }

    async Task SelectYieldCurvePeriodAsync(
        AutomationElement root,
        string period,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var selector = RequireDescendant(root, "ddlTimePeriod").AsComboBox();
        var periods = await WaitUntilAsync(
            () =>
            {
                var items = ReadComboItems(selector);
                return items.Contains(period, StringComparer.Ordinal) ? items : null;
            },
            timeout,
            $"The yield-curve editor did not expose time period '{period}'.",
            cancellationToken);
        var index = periods
            .Select((item, index) => (item, index))
            .Single(pair => string.Equals(pair.item, period, StringComparison.Ordinal))
            .index;
        selector.Expand();
        var item = selector.Items[index];
        item.Select();
        selector.Collapse();
        await WaitUntilAsync(
            () => string.Equals(selector.SelectedItem?.Text, period, StringComparison.Ordinal)
                ? period
                : null,
            timeout,
            $"The yield-curve editor did not select time period '{period}'.",
            cancellationToken);
        await WaitForEnabledAsync(root, "gridYieldCurveRates", timeout, cancellationToken);
    }

    static void SetYieldCurveRateFields(AutomationElement dialog, YieldCurveRateReadModel rate)
    {
        SetText(dialog, "txtOneMonth", FormatRate(rate.OneMonth));
        SetText(dialog, "txtTwoMonth", FormatRate(rate.TwoMonth));
        SetText(dialog, "txtThreeMonth", FormatRate(rate.ThreeMonth));
        SetText(dialog, "txtSixMonth", FormatRate(rate.SixMonth));
        SetText(dialog, "txtOneYear", FormatRate(rate.OneYear));
        SetText(dialog, "txtTwoYear", FormatRate(rate.TwoYear));
        SetText(dialog, "txtThreeYear", FormatRate(rate.ThreeYear));
        SetText(dialog, "txtFiveYear", FormatRate(rate.FiveYear));
        SetText(dialog, "txtSevenYear", FormatRate(rate.SevenYear));
        SetText(dialog, "txtTenYear", FormatRate(rate.TenYear));
        SetText(dialog, "txtTwentyYear", FormatRate(rate.TwentyYear));
        SetText(dialog, "txtThirtyYear", FormatRate(rate.ThirtyYear));
    }

    static string FormatRate(double value)
        => value.ToString("0.00", CultureInfo.InvariantCulture);

    static void SetEconomicCalendarFields(
        AutomationElement root,
        EconomicCalendarReadModel calendar,
        bool includeEventName)
    {
        if (includeEventName)
            SetText(root, "txtEventName", calendar.EventName);
        SetText(root, "txtActual", calendar.Actual ?? string.Empty);
        SetText(root, "txtForecast", calendar.Forecast ?? string.Empty);
        SetText(root, "txtPrior", calendar.Prior ?? string.Empty);
    }

    static void SelectEconomicCalendarEvent(AutomationElement root, string eventName)
    {
        var list = RequireDescendant(root, "lstCalendarEvents").AsListBox();
        if (!list.IsEnabled)
            throw new InvalidOperationException("The economic-calendar event list is disabled.");
        var item = list.Items.SingleOrDefault(candidate =>
                candidate.Text.Contains(eventName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"The economic-calendar list does not contain event '{eventName}'.");
        list.Select(item.Text);
    }

    async Task<G2EconomicCalendarEditorUiState> WaitForEconomicCalendarStateAsync(
        AutomationElement root,
        DateOnly date,
        EconomicCalendarReadModel? expected,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () =>
            {
                var state = ReadEconomicCalendarState(root, date);
                var eventName = expected?.EventName ?? string.Empty;
                var contains = !string.IsNullOrEmpty(eventName)
                               && state.Items.Any(item => item.Contains(eventName, StringComparison.Ordinal));
                if (contains != present)
                    return null;
                if (present)
                {
                    SelectEconomicCalendarEvent(root, eventName);
                    state = ReadEconomicCalendarState(root, date);
                    if (!string.Equals(state.EventName, eventName, StringComparison.Ordinal)
                        || !string.Equals(state.Actual, expected?.Actual, StringComparison.Ordinal)
                        || !string.Equals(state.Forecast, expected?.Forecast, StringComparison.Ordinal)
                        || !string.Equals(state.Prior, expected?.Prior, StringComparison.Ordinal))
                        return null;
                }
                return state;
            },
            timeout,
            $"The economic-calendar editor did not render '{expected?.EventName}' as {(present ? "present" : "absent")}.",
            cancellationToken);

    static G2EconomicCalendarEditorUiState ReadEconomicCalendarState(
        AutomationElement root,
        DateOnly targetDate)
    {
        var picker = RequireDescendant(root, "dtmEventDate").AsDateTimePicker();
        var country = ReadSelectedComboValue(RequireDescendant(root, "ddlCountryCodes").AsComboBox());
        var list = RequireDescendant(root, "lstCalendarEvents").AsListBox();
        return new G2EconomicCalendarEditorUiState(
            ReadAccessibleDate(picker, "dtmEventDate"),
            country,
            targetDate,
            list.Items.Select(item => item.Text).ToArray(),
            ReadText(root, "txtEventName"),
            ReadText(root, "txtActual"),
            ReadText(root, "txtForecast"),
            ReadText(root, "txtPrior"));
    }

    static void SelectLookupType(
        AutomationElement root,
        string lookupTypeName,
        string shortCode)
    {
        SelectListItem(root, "lstLookupTypeNames", lookupTypeName);
        SelectListItem(root, "lstLookupTypeShortCodes", shortCode);
    }

    async Task<G2LookupTypeEditorUiState> WaitForLookupTypeStateAsync(
        AutomationElement root,
        LookupTypeReadModel expected,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () =>
            {
                var names = RequireDescendant(root, "lstLookupTypeNames").AsListBox();
                if (!names.IsEnabled)
                    return null;
                var nameItems = names.Items.Select(item => item.Text).ToArray();
                var containsName = nameItems.Contains(expected.LookupTypeName, StringComparer.Ordinal);
                if (containsName != present)
                    return null;
                if (!present)
                    return ReadLookupTypeState(root);

                names.Select(expected.LookupTypeName);
                var shortCodes = RequireDescendant(root, "lstLookupTypeShortCodes").AsListBox();
                if (!shortCodes.IsEnabled)
                    return null;
                var shortCodeItems = shortCodes.Items.Select(item => item.Text).ToArray();
                if (!shortCodeItems.Contains(expected.ShortCode, StringComparer.Ordinal))
                    return null;
                shortCodes.Select(expected.ShortCode);
                var state = ReadLookupTypeState(root);
                return string.Equals(state.LookupTypeName, expected.LookupTypeName, StringComparison.Ordinal)
                       && string.Equals(state.ShortCode, expected.ShortCode, StringComparison.Ordinal)
                       && string.Equals(
                           state.OrderId,
                           expected.OrderId.ToString(CultureInfo.InvariantCulture),
                           StringComparison.Ordinal)
                       && string.Equals(state.Description, expected.Description, StringComparison.Ordinal)
                    ? state
                    : null;
            },
            timeout,
            $"The lookup-type editor did not render '{expected.LookupTypeName}/{expected.ShortCode}' as "
            + $"{(present ? "present" : "absent")}.",
            cancellationToken);

    static G2LookupTypeEditorUiState ReadLookupTypeState(AutomationElement root)
    {
        var names = RequireDescendant(root, "lstLookupTypeNames").AsListBox();
        var shortCodes = RequireDescendant(root, "lstLookupTypeShortCodes").AsListBox();
        return new G2LookupTypeEditorUiState(
            names.Items.Select(item => item.Text).ToArray(),
            shortCodes.Items.Select(item => item.Text).ToArray(),
            ReadText(root, "txtLookupTypeName"),
            ReadText(root, "txtShortCode"),
            ReadText(root, "txtOrderId"),
            ReadText(root, "txtDescription"));
    }

    static string ReadSelectedComboValue(FlaUI.Core.AutomationElements.ComboBox combo)
    {
        var name = combo.Name ?? string.Empty;
        var namedSelection = ParseNamedComboSelection(name);
        if (!string.IsNullOrWhiteSpace(namedSelection))
            return namedSelection;

        var selected = combo.SelectedItem?.Text;
        if (!string.IsNullOrWhiteSpace(selected))
            return selected;

        var label = name.Split(';', 2)[0];
        var separator = label.LastIndexOf(':');
        return separator >= 0 ? label[(separator + 1)..].Trim() : string.Empty;
    }

    internal static string ParseNamedComboSelection(string name)
    {
        const string selectedMarker = "; selected=";
        var selectedIndex = name.IndexOf(selectedMarker, StringComparison.OrdinalIgnoreCase);
        if (selectedIndex >= 0)
        {
            var valueStart = selectedIndex + selectedMarker.Length;
            var catalogIndex = name.IndexOf("; catalog:", valueStart, StringComparison.OrdinalIgnoreCase);
            var namedSelection = (catalogIndex < 0 ? name[valueStart..] : name[valueStart..catalogIndex]).Trim();
            if (!string.IsNullOrWhiteSpace(namedSelection))
                return namedSelection;
        }
        return string.Empty;
    }

    static void SelectYieldCurveRow(AutomationElement root, DateOnly valueDate)
    {
        var grid = RequireDescendant(root, "gridYieldCurveRates").AsDataGridView();
        var rows = grid.Rows;
        var row = rows.SingleOrDefault(item => YieldCurveRowHasDate(item, valueDate))
            ?? (rows.Length == 1 ? rows[0] : null)
            ?? throw new InvalidOperationException($"The yield-curve grid does not contain '{valueDate:yyyy-MM-dd}'.");
        var cell = row.Cells[0];
        if (cell.Patterns.SelectionItem.IsSupported)
            cell.Patterns.SelectionItem.Pattern.Select();
        else
            cell.Click();
    }

    async Task<G2YieldCurveEditorUiState> WaitForYieldCurveStateAsync(
        AutomationElement root,
        DateOnly valueDate,
        YieldCurveRateReadModel? expectedRate,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => await WaitUntilAsync(
            () =>
            {
                var state = ReadYieldCurveState(root, valueDate);
                var gridElement = RequireDescendant(root, "gridYieldCurveRates");
                if (!gridElement.IsEnabled)
                    return null;
                var grid = gridElement.AsDataGridView();
                var matchingRow = grid.Rows.SingleOrDefault(row => YieldCurveRowHasDate(row, valueDate));
                if (present && matchingRow is null && grid.Rows.Length == 0)
                    return null;
                return state;
            },
            timeout,
            $"The yield-curve editor did not render '{valueDate:yyyy-MM-dd}' as {(present ? "present with expected rates" : "absent")}.",
            cancellationToken);

    static G2YieldCurveEditorUiState ReadYieldCurveState(AutomationElement root, DateOnly valueDate)
    {
        var period = RequireDescendant(root, "ddlTimePeriod").AsComboBox().SelectedItem?.Text ?? string.Empty;
        var importDate = ReadAccessibleDate(
            RequireDescendant(root, "dtmImportDate").AsDateTimePicker(),
            "dtmImportDate");
        var rows = ReadDataGridRows(RequireDescendant(root, "gridYieldCurveRates"));
        return new G2YieldCurveEditorUiState(
            period,
            importDate,
            valueDate,
            rows);
    }

    static bool YieldCurveRowHasDate(
        FlaUI.Core.AutomationElements.DataGridViewRow row,
        DateOnly valueDate)
    {
        var value = row.Cells.FirstOrDefault()?.Value;
        return DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var actual)
                   && actual == valueDate
               || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out actual)
                   && actual == valueDate;
    }

    static IReadOnlyList<string> ReadDataGridRows(AutomationElement element)
        => element.AsDataGridView().Rows.Select(ReadDataGridRow).ToArray();

    static string ReadDataGridRow(FlaUI.Core.AutomationElements.DataGridViewRow row)
        => string.Join(" | ", row.Cells.Select(cell => cell.Value ?? string.Empty));

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
                     "btnCompleteOrder", "btnAddTrade", "btnRemoveTrade", "btnChangeTradeState", "btnEndOfDay", "btnSubmitOrder",
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
        if (parentHandle == IntPtr.Zero)
            throw new InvalidOperationException($"Combo-box item {index} selection has no owning window.");
        SendMessage(parentHandle, WmCommand, notification, comboHandle);
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumChildWindows(
        IntPtr parentWindowHandle,
        EnumWindowsCallback callback,
        IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr windowHandle, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindowVisible(IntPtr windowHandle);

    const uint WmLeftButtonDown = 0x0201;
    const uint WmLeftButtonUp = 0x0202;
    const uint WmCommand = 0x0111;
    const uint CbSetCurrentSelection = 0x014E;
    const int CbnSelectionChanged = 1;
    const int MkLeftButton = 0x0001;
    const int SwRestore = 9;
    const uint BmClick = 0x00F5;
    const uint WmKeyDown = 0x0100;
    const uint WmKeyUp = 0x0101;
    const uint DtmSetSystemTime = 0x1002;
    const int GdtValid = 0;
    const int VkEnter = 0x0D;
    const int VkPageUp = 0x21;
    const int VkPageDown = 0x22;
    const int VkLeft = 0x25;
    const int VkRight = 0x27;

    [StructLayout(LayoutKind.Sequential)]
    struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NativeSystemTime
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;
    }

    delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ScreenToClient(IntPtr windowHandle, ref NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetClientRect(IntPtr windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wordParameter, IntPtr longParameter);

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr windowHandle, uint message, IntPtr wordParameter, IntPtr longParameter);

    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wordParameter,
        ref NativeSystemTime longParameter);

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

public sealed record G2SecuritiesEditorUiState(
    string EditorAutomationId,
    IReadOnlyList<string> ContractIds,
    string SelectedContractId,
    string Description);

public sealed record G2YieldCurveEditorUiState(
    string SelectedPeriod,
    DateOnly? ImportDate,
    DateOnly TargetDate,
    IReadOnlyList<string> Rows);

public sealed record G2EconomicCalendarEditorUiState(
    DateOnly? SelectedDate,
    string SelectedCountryCode,
    DateOnly TargetDate,
    IReadOnlyList<string> Items,
    string EventName,
    string Actual,
    string Forecast,
    string Prior);

public sealed record G2LookupTypeEditorUiState(
    IReadOnlyList<string> LookupTypeNames,
    IReadOnlyList<string> ShortCodes,
    string LookupTypeName,
    string ShortCode,
    string OrderId,
    string Description);

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

public sealed record G2CreatedFundUiState(
    int FundId,
    string FundName,
    decimal InitialBalance);

public sealed record G2FundTransactionUiState(
    string FundName,
    string Balance,
    IReadOnlyList<string> Rows);

public sealed record G2TradeOrderUiState(
    string FundName,
    int? SelectedOrderId,
    int? SelectedTradeId,
    IReadOnlyList<string> OrderRows,
    IReadOnlyList<string> TradeRows,
    IReadOnlyDictionary<string, bool> CommandStates);

public sealed record G1TradeWindowState(
    IReadOnlyList<string> Funds,
    int OrderRows,
    int TradeRows,
    IReadOnlyDictionary<string, bool> CommandStates);
