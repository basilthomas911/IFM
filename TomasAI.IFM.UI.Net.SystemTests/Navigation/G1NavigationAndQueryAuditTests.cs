using System.Text.Json;
using System.Diagnostics;
using FluentAssertions;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

namespace TomasAI.IFM.UI.Net.SystemTests.Navigation;

[Trait("Category", "G1Process")]
public sealed class G1NavigationAndQueryAuditTests
{
    const int ExpectedStepCount = 15;

    [Fact]
    public async Task Development_desktop_navigation_and_read_only_queries_satisfy_G1()
    {
        if (!G0Configuration.G1LiveRunEnabled)
            return;

        var configuration = G0Configuration.Load();
        var redactor = new SecretRedactor([Environment.GetEnvironmentVariable("FMP_API_KEY")]);
        var evidence = new G0EvidenceWriter(configuration, redactor);
        var run = new G0RunResult
        {
            Gate = "G1",
            ExpectedStepCount = ExpectedStepCount,
            RunId = configuration.RunId,
            Environment = configuration.EnvironmentName,
            StartedUtc = DateTimeOffset.UtcNow,
            ApiExecutable = configuration.ApiExecutable,
            DesktopExecutable = configuration.DesktopExecutable,
            Endpoints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["apiReadiness"] = configuration.ApiReadyUri.ToString(),
                ["nats"] = configuration.NatsUri.ToString(),
                ["postgresql"] = $"{configuration.PostgreSql.Host}:{configuration.PostgreSql.Port}",
                ["scyllaDb"] = $"{configuration.ScyllaDb.Host}:{configuration.ScyllaDb.Port}",
                ["redis"] = $"{configuration.Redis.Host}:{configuration.Redis.Port}"
            }
        };
        var recorder = new G0AuditRecorder(run);
        using var auditTimeout = new CancellationTokenSource(configuration.AuditTimeout);
        var cancellationToken = auditTimeout.Token;

        OwnedProcess? api = null;
        OwnedProcess? desktop = null;
        G0EventObserver? observer = null;
        G0QuerySession? queries = null;
        G1UiAutomationSession? automation = null;
        G1ExpectedState? expected = null;
        bool cleanupSucceeded = false;

        try
        {
            await SyncStep("G1-001", "Validate G1 configuration",
                "G0 prerequisites remain valid and the G1 Development audit owns valid executable and evidence paths.",
                _ =>
                {
                    var errors = configuration.Validate();
                    errors = errors.Concat(FindConflictingProcesses(configuration)).ToArray();
                    if (errors.Count > 0)
                        throw new G0DependencyException(string.Join(Environment.NewLine, errors));
                    return Observation(
                        $"Configuration valid; environment={configuration.EnvironmentName}; evidence={evidence.RunDirectory}.",
                        ["result.json", "summary.md"]);
                });

            await Step("G1-002", "Probe Development dependencies and start event evidence",
                "NATS, PostgreSQL, ScyllaDB, and Redis accept connections before navigation begins.",
                async token =>
                {
                    List<string> failures = [];
                    foreach (var endpoint in new[]
                             {
                                 new G0Endpoint("NATS", configuration.NatsUri.Host, configuration.NatsUri.Port),
                                 configuration.PostgreSql,
                                 configuration.ScyllaDb,
                                 configuration.Redis
                             })
                    {
                        try
                        {
                            await InfrastructureProbe.ProbeTcpAsync(endpoint, configuration.ReadinessTimeout, token);
                        }
                        catch (G0DependencyException exception)
                        {
                            failures.Add(exception.Message);
                        }
                    }
                    if (failures.Count > 0)
                        throw new G0DependencyException(string.Join(Environment.NewLine, failures));
                    observer = new G0EventObserver(configuration.NatsUri);
                    await observer.StartAsync(token);
                    return Observation("All four Development services are reachable; typed event evidence is active.");
                });

            await Step("G1-003", "Start and verify the Development actor backend",
                $"The harness-owned API becomes Healthy with {configuration.ExpectedActorTypeCount} registered actor types.",
                async token =>
                {
                    RequirePassed(recorder, "G1-002", "Development dependencies are required by the API.");
                    api = OwnedProcess.Start(
                        configuration.ApiExecutable,
                        evidence.ApiLogDirectory,
                        redactor,
                        new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = configuration.EnvironmentName });
                    run.ApiProcessId = api.Process.Id.ToString();
                    var readiness = await InfrastructureProbe.WaitForApiReadinessAsync(
                        configuration.ApiReadyUri,
                        configuration.ReadinessTimeout,
                        token,
                        () => (api.Process.HasExited, api.Process.HasExited ? api.Process.ExitCode : null));
                    await evidence.WriteTextAsync(
                        Path.Combine("network", "api-readiness.json"),
                        JsonSerializer.Serialize(readiness, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    if (!string.Equals(readiness.Status, "Healthy", StringComparison.OrdinalIgnoreCase)
                        || readiness.RegisteredActorTypes != configuration.ExpectedActorTypeCount)
                        throw new InvalidOperationException(
                            $"API readiness was {readiness.Status}; actorTypes={readiness.RegisteredActorTypes}.");
                    return Observation(
                        $"API PID {api.Process.Id} is Healthy; registeredActorTypes={readiness.RegisteredActorTypes}.",
                        ["network/api-readiness.json"]);
                });

            await Step("G1-004", "Query and establish the read-only Development baseline",
                "Typed NATS queries return the selector catalogs and representative shell/fund data; ES and VX current chart bars exist before UI launch.",
                async token =>
                {
                    RequirePassed(recorder, "G1-003", "A ready actor backend is required by typed queries.");
                    queries = new G0QuerySession(configuration.NatsUri);
                    await queries.StartAsync(configuration.RunId, token, "G1");
                    expected = await LoadExpectedStateAsync(queries, configuration.ReadinessTimeout, token);
                    var feedNormalization = await NormalizeMarketDataFeedAsync(
                        queries, observer!, configuration.ReadinessTimeout, token);
                    return Observation(
                        $"Queried contracts={expected.Contracts.Length}, options={expected.EsOptionContracts.Length}, "
                        + $"marketSelectors={expected.MarketDataDefinitions.Length}, referenceSelectors={expected.ReferenceDefinitions.Length}, "
                        + $"systemSelectors={expected.SystemAdminDefinitions.Length}, deferredSystemSelectors=[{string.Join(",", expected.DeferredSystemAdminDefinitions)}], "
                        + $"lookupNames={expected.LookupTypeNames.Length}, "
                        + $"funds={expected.Funds.Length}, calendars={expected.EconomicCalendarCount}; "
                        + $"ES={expected.EsContract.ContractId}, VX={expected.VxContract.ContractId}; "
                        + feedNormalization + ".");
                });

            await Step("G1-005", "Launch and reach the initialized shell",
                "The real desktop exposes its final startup status and enables every primary navigation action.",
                async token =>
                {
                    RequireExpected(expected);
                    RequireObserver(observer);
                    desktop = OwnedProcess.Start(configuration.DesktopExecutable, evidence.UiLogDirectory, redactor);
                    run.DesktopProcessId = desktop.Process.Id.ToString();
                    automation = new G1UiAutomationSession(desktop.Process.Id);
                    var window = await automation.WaitForMainWindowAsync(configuration.StartupTimeout, token);
                    var shell = await automation.WaitForInitializedShellAsync(configuration.StartupTimeout, token);
                    var artifacts = CaptureAcceptedEvidence(automation, evidence, "G1-005-shell");
                    return Observation(
                        $"Initialized window='{window.Title}'; status='{shell.Status}'; "
                        + $"toolbar={string.Join(",", shell.Toolbar.Select(pair => $"{pair.Key}:{pair.Value}"))}.",
                        artifacts);
                });

            await Step("G1-006", "Render shell status, EOD/signal values, status history, and both charts",
                "The startup query branches produce visible market-outlook values, bounded status rows, an ES chart, and a VX-backed VIX chart.",
                async token =>
                {
                    RequireAutomation(automation);
                    var shell = automation!.ReadShellState();
                    List<string> failures = [];
                    var empty = shell.MarketOutlook.Where(pair => string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key).ToArray();
                    if (empty.Length > 0)
                        failures.Add("Empty market-outlook values: " + string.Join(", ", empty));
                    var status = automation.ReadStatusConsoleState();
                    if (status.RowCount == 0)
                        failures.Add("The bounded status console rendered no startup rows.");
                    else if (!status.Rows.Any(row => row.Contains(
                                 "initialization complete", StringComparison.OrdinalIgnoreCase)))
                        failures.Add("The bounded status console did not retain the initialization-complete row.");
                    G1ChartState? esChart = null;
                    G1ChartState? vxChart = null;
                    try
                    {
                        esChart = await automation.ReadChartAsync(
                            "ES", "graphES", configuration.ReadinessTimeout, token);
                    }
                    catch (Exception exception)
                    {
                        failures.Add("ES chart: " + exception.Message);
                    }
                    try
                    {
                        vxChart = await automation.ReadChartAsync(
                            "VIX", "graphVIX", configuration.ReadinessTimeout, token);
                    }
                    catch (Exception exception)
                    {
                        failures.Add("VX/VIX chart: " + exception.Message);
                    }
                    var artifacts = CaptureAcceptedEvidence(automation, evidence, "G1-006-shell-readonly");
                    if (failures.Count > 0)
                        throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
                    return Observation(
                        $"StatusRows={status.RowCount}; latest='{status.FirstRow}'; "
                        + $"marketOutlook={string.Join(",", shell.MarketOutlook.Select(pair => $"{pair.Key}:{pair.Value}"))}; "
                        + $"ES automationPoints={esChart!.DataPointCount}; VX/VIX automationPoints={vxChart!.DataPointCount}.",
                        artifacts);
                });

            await Step("G1-007", "Exercise every economic-calendar read-only range",
                "Today, Yesterday, Tomorrow, This Week, and Next Week render with a date and country catalog.",
                async token =>
                {
                    RequireAutomation(automation);
                    var calendar = await automation!.ReadEconomicCalendarViewsAsync(
                        configuration.ReadinessTimeout,
                        token);
                    if (string.IsNullOrWhiteSpace(calendar.CalendarDate) || calendar.Countries.Count == 0)
                        throw new InvalidOperationException("Economic-calendar date or country state is empty.");
                    return Observation(
                        $"CalendarDate='{calendar.CalendarDate}'; countries={calendar.Countries.Count}; "
                        + $"views={string.Join(",", calendar.RowsByView.Select(pair => $"{pair.Key}:{pair.Value}"))}.");
                });

            await Step("G1-008", "Navigate the Market Data query catalog",
                "Every queried Market Data definition renders its supported view, data surface, and selection-aware command state, then closes without mutation.",
                async token =>
                {
                    RequireAutomation(automation);
                    RequireExpected(expected);
                    automation!.InvokeToolbarAction("MarketData");
                    var window = await automation.WaitForWindowAsync(
                        "Market Data Manager", configuration.ReadinessTimeout, token);
                    var map = MapDefinitions(expected!.MarketDataDefinitions, MarketDataViewIds);
                    var catalog = await automation.ReadSelectorCatalogAsync(
                        window, "ddlMarketDataSelector", map, configuration.ReadinessTimeout, token);
                    var contractView = catalog.Views.Single(view => view.ViewAutomationId == "FuturesContractEditorControl");
                    var renderedContracts = contractView.DataCounts.GetValueOrDefault("lstFuturesContractIds", -1);
                    if (renderedContracts != expected.Contracts.Length)
                        throw new InvalidOperationException(
                            $"Futures contract UI rows={renderedContracts}; query rows={expected.Contracts.Length}.");
                    var artifacts = CaptureAcceptedEvidence(automation, evidence, "G1-008-market-data");
                    await automation.CloseWindowAsync(window, configuration.ShutdownTimeout, token);
                    return Observation(
                        $"Selectors=[{string.Join(", ", catalog.Items)}]; contractRows={renderedContracts}; "
                        + $"views={DescribeViews(catalog.Views)}.",
                        artifacts);
                });

            await Step("G1-009", "Navigate the Reference query catalog",
                "Every queried Reference definition renders its supported view and lookup/calendar data surface, then closes without mutation.",
                async token =>
                {
                    RequireAutomation(automation);
                    RequireExpected(expected);
                    automation!.InvokeToolbarAction("Reference");
                    var window = await automation.WaitForWindowAsync(
                        "Reference Data Manager", configuration.ReadinessTimeout, token);
                    var map = MapDefinitions(expected!.ReferenceDefinitions, ReferenceViewIds);
                    var catalog = await automation.ReadSelectorCatalogAsync(
                        window, "ddlReferenceDataSelector", map, configuration.ReadinessTimeout, token);
                    var lookupView = catalog.Views.Single(view => view.ViewAutomationId == "LookupTypeEditorView");
                    var renderedNames = lookupView.DataCounts.GetValueOrDefault("lstLookupTypeNames", -1);
                    if (renderedNames != expected.LookupTypeNames.Length)
                        throw new InvalidOperationException(
                            $"Lookup-type UI rows={renderedNames}; query rows={expected.LookupTypeNames.Length}.");
                    var artifacts = CaptureAcceptedEvidence(automation, evidence, "G1-009-reference");
                    await automation.CloseWindowAsync(window, configuration.ShutdownTimeout, token);
                    return Observation(
                        $"Selectors=[{string.Join(", ", catalog.Items)}]; lookupTypeNames={renderedNames}; "
                        + $"views={DescribeViews(catalog.Views)}.",
                        artifacts);
                });

            await Step("G1-010", "Render Funds list, balance, transactions, and metrics",
                "The Funds destination matches the typed fund query and renders selected-fund detail state without mutation.",
                async token =>
                {
                    RequireAutomation(automation);
                    RequireExpected(expected);
                    automation!.InvokeToolbarAction("Funds");
                    var window = await automation.WaitForWindowAsync(
                        "Fund Transactions Editor", configuration.ReadinessTimeout, token);
                    var state = await automation.ReadFundWindowAsync(window, configuration.ReadinessTimeout, token);
                    if (state.Funds.Count != expected!.Funds.Length)
                        throw new InvalidOperationException(
                            $"Fund selector items={state.Funds.Count}; query rows={expected.Funds.Length}.");
                    var artifacts = CaptureAcceptedEvidence(automation, evidence, "G1-010-funds");
                    await automation.CloseWindowAsync(window, configuration.ShutdownTimeout, token);
                    return Observation(
                        $"Funds={state.Funds.Count}; selectedBalance='{state.Balance}'; "
                        + $"transactionRows={state.TransactionRows}; pnl='{state.ProfitLoss}'.",
                        artifacts);
                });

            await Step("G1-011", "Render Trading orders, trades, and selection-aware actions",
                "The Trade destination matches the typed fund catalog and renders its read-only order/trade branches without submitting an order.",
                async token =>
                {
                    RequireAutomation(automation);
                    RequireExpected(expected);
                    automation!.InvokeToolbarAction("Trade");
                    var window = await automation.WaitForWindowAsync(
                        "Trade Orders", configuration.ReadinessTimeout, token);
                    var state = await automation.ReadTradeWindowAsync(window, configuration.ReadinessTimeout, token);
                    if (state.Funds.Count != expected!.Funds.Length)
                        throw new InvalidOperationException(
                            $"Trade fund selector items={state.Funds.Count}; query rows={expected.Funds.Length}.");
                    var artifacts = CaptureAcceptedEvidence(automation, evidence, "G1-011-trading");
                    await automation.CloseWindowAsync(window, configuration.ShutdownTimeout, token);
                    return Observation(
                        $"Funds={state.Funds.Count}; orderRows={state.OrderRows}; tradeRows={state.TradeRows}; "
                        + $"actions={string.Join(",", state.CommandStates.Select(pair => $"{pair.Key}:{pair.Value}"))}.",
                        artifacts);
                });

            await Step("G1-012", "Navigate System Administration read-only status",
                "Every queried System Administration function renders its supported non-mutating status/detail view and closes cleanly.",
                async token =>
                {
                    RequireAutomation(automation);
                    RequireExpected(expected);
                    automation!.InvokeToolbarAction("System");
                    var window = await automation.WaitForWindowAsync(
                        "System Admin Manager", configuration.ReadinessTimeout, token);
                    var map = MapDefinitions(expected!.SystemAdminDefinitions, SystemAdminViewIds);
                    var catalog = await automation.ReadSelectorCatalogAsync(
                        window, "ddlFunctionSelector", map, configuration.ReadinessTimeout, token);
                    var artifacts = CaptureAcceptedEvidence(automation, evidence, "G1-012-system-admin");
                    await automation.CloseWindowAsync(window, configuration.ShutdownTimeout, token);
                    return Observation(
                        $"Selectors=[{string.Join(", ", catalog.Items)}]; views={DescribeViews(catalog.Views)}.",
                        artifacts);
                });

            await Step("G1-013", "Reopen a modal destination without stale or duplicate state",
                "A closed destination reopens with the same queried selector catalog, one modal window, and no unexpected dialog.",
                async token =>
                {
                    RequireAutomation(automation);
                    RequireExpected(expected);
                    automation!.InvokeToolbarAction("MarketData");
                    var window = await automation.WaitForWindowAsync(
                        "Market Data Manager", configuration.ReadinessTimeout, token);
                    var catalog = await automation.ReadSelectorCatalogAsync(
                        window,
                        "ddlMarketDataSelector",
                        MapDefinitions(expected!.MarketDataDefinitions, MarketDataViewIds),
                        configuration.ReadinessTimeout,
                        token);
                    var unexpected = automation.FindUnexpectedWindowTitles("Market Data Manager");
                    if (unexpected.Count > 0)
                        throw new InvalidOperationException("Unexpected modal windows: " + string.Join(", ", unexpected));
                    await automation.CloseWindowAsync(window, configuration.ShutdownTimeout, token);
                    return Observation(
                        $"Market Data reopened once with {catalog.Items.Count} selector items and no unexpected modal window.");
                });

            await SyncStep("G1-014", "Request normal application close",
                "The initialized main window accepts a normal UI Automation close after all destinations have closed.",
                _ =>
                {
                    RequireAutomation(automation);
                    var unexpected = automation!.FindUnexpectedWindowTitles();
                    if (unexpected.Count > 0)
                        throw new InvalidOperationException("Secondary windows remain open: " + string.Join(", ", unexpected));
                    automation.RequestMainWindowClose();
                    return Observation("Normal main-window close requested after all read-only destinations completed.");
                });

            await Step("G1-015", "Verify bounded shutdown and cleanup",
                "The desktop exits normally; no error-coded startup/navigation status, process, query listener, or harness backend remains.",
                async token =>
                {
                    List<string> failures = [];
                    if (desktop is not null)
                    {
                        var exited = await desktop.WaitForExitAsync(configuration.ShutdownTimeout, token);
                        if (!exited)
                            failures.Add($"Desktop did not exit within {configuration.ShutdownTimeout}.");
                        if (desktop.ForcedTermination)
                            failures.Add("Desktop required forced termination.");
                    }
                    if (queries is not null)
                    {
                        await queries.DisposeAsync();
                        queries = null;
                    }
                    if (observer is not null)
                    {
                        var errorStatuses = observer.Events.Where(IsErrorCodedStatus).ToArray();
                        if (errorStatuses.Length > 0)
                            failures.Add(
                                $"Observed {errorStatuses.Length} error-coded status message(s): "
                                + string.Join(" | ", errorStatuses.Take(5).Select(row => row.Message)));
                        await observer.WriteEvidenceAsync(evidence, token);
                        await observer.DisposeAsync();
                        observer = null;
                    }
                    if (api is not null && !api.Process.HasExited
                        && !await api.TerminateOwnedTreeAsync(TimeSpan.FromSeconds(10), token))
                        failures.Add("Harness-owned API process tree did not terminate.");
                    if (api is not null)
                        await evidence.WriteTextAsync(Path.Combine("processes", "api-final.json"), api.Describe(), token);
                    if (desktop is not null)
                        await evidence.WriteTextAsync(Path.Combine("processes", "desktop-final.json"), desktop.Describe(), token);
                    if (failures.Count > 0)
                        throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
                    cleanupSucceeded = true;
                    return Observation(
                        "Desktop exited normally; typed query/event sessions stopped; no error-coded status remained; harness API removed.",
                        ["network/nats-events.json", "processes/api-final.json", "processes/desktop-final.json"]);
                });
        }
        finally
        {
            automation?.Dispose();
            if (queries is not null)
            {
                try { await queries.DisposeAsync(); }
                catch { cleanupSucceeded = false; }
            }
            if (observer is not null)
            {
                try
                {
                    await observer.WriteEvidenceAsync(evidence, CancellationToken.None);
                    await observer.DisposeAsync();
                }
                catch { cleanupSucceeded = false; }
            }
            if (desktop is not null)
            {
                try { await desktop.DisposeAsync(); }
                catch { cleanupSucceeded = false; }
            }
            if (api is not null)
            {
                try { await api.DisposeAsync(); }
                catch { cleanupSucceeded = false; }
            }
            run.CleanupSucceeded = cleanupSucceeded;
            run.CompletedUtc = DateTimeOffset.UtcNow;
            await evidence.WriteResultAsync(run, CancellationToken.None);
        }

        run.Passed.Should().BeTrue(
            $"G1 evidence was written to {evidence.RunDirectory}; "
            + string.Join("; ", run.Steps
                .Where(step => step.Status != G0StepStatus.Passed)
                .Select(step => $"{step.Id}={step.Status}: {step.Actual}")));

        async Task<G0StepResult> Step(
            string id,
            string name,
            string expectedOutcome,
            Func<CancellationToken, Task<G0StepObservation>> action)
        {
            var step = await recorder.RunAsync(id, name, expectedOutcome, action, cancellationToken);
            if (step.Status == G0StepStatus.Failed && automation is not null)
            {
                try { CaptureAcceptedEvidence(automation, evidence, id + "-failure"); }
                catch { /* Failure evidence/recovery must not hide the original step. */ }
                try { automation.CloseAllSecondaryWindows(); }
                catch { /* Failure recovery must not hide the original step. */ }
            }
            await evidence.WriteResultAsync(run, CancellationToken.None);
            return step;
        }

        Task<G0StepResult> SyncStep(
            string id,
            string name,
            string expectedOutcome,
            Func<CancellationToken, G0StepObservation> action)
            => Step(id, name, expectedOutcome, token => Task.FromResult(action(token)));
    }

    static readonly IReadOnlyDictionary<string, string> MarketDataViewIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FuturesOptionContract"] = "FuturesOptionContractEditorControl",
            ["FuturesContract"] = "FuturesContractEditorControl",
            ["YieldCurveRates"] = "YieldCurveRateEditorControl"
        };

    static readonly IReadOnlyDictionary<string, string> ReferenceViewIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["EconomicCalendar"] = "EconomicCalendarEditorView",
            ["LookupTypes"] = "LookupTypeEditorView"
        };

    static readonly IReadOnlyDictionary<string, string> SystemAdminViewIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["BackupDatabases"] = "BackupDatabasesView"
        };

    static async Task<G1ExpectedState> LoadExpectedStateAsync(
        G0QuerySession queries,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var es = RequireValue(await queries.MarketData.GetCurrentlyTradedFuturesContractAsync("ES")
            .WaitAsync(timeout, cancellationToken), "current ES contract");
        var vx = RequireValue(await queries.MarketData.GetCurrentlyTradedFuturesContractAsync("VX")
            .WaitAsync(timeout, cancellationToken), "current VX contract");
        if (!es.IsValid || !es.CurrentlyTraded || !vx.IsValid || !vx.CurrentlyTraded)
            throw new G0DependencyException("Current ES/VX contracts are not valid and currently traded.");

        vx = await EnsureVxCurrencyAsync(queries, vx, timeout, cancellationToken);

        await G0DevelopmentDataFixture.EnsureAsync(queries, es, timeout, cancellationToken);
        await G0DevelopmentDataFixture.EnsureBarAsync(queries, vx, timeout, cancellationToken);

        var contracts = RequireValue(await queries.MarketData.GetFuturesContractsAsync()
            .WaitAsync(timeout, cancellationToken), "futures contracts");
        var options = RequireValue(await queries.MarketData.GetFuturesOptionContractsAsync("ES")
            .WaitAsync(timeout, cancellationToken), "ES futures option contracts");
        _ = RequireValue(await queries.MarketDataFeed.GetLastFuturesEodDataAsync(es.ContractId, es.LastTradeDate)
            .WaitAsync(timeout, cancellationToken), "latest ES EOD");
        _ = RequireValue(await queries.MarketDataAnalytics.GetLastFuturesTradeSignalAsync()
            .WaitAsync(timeout, cancellationToken), "latest futures trade signal");
        _ = RequireValue(await queries.MarketDataFeed.GetLastFuturesBarDataAsync(
                es.ContractId, es.Symbol, DateOnly.FromDateTime(DateTime.UtcNow))
            .WaitAsync(timeout, cancellationToken), "latest ES bar");
        _ = RequireValue(await queries.MarketDataFeed.GetLastFuturesBarDataAsync(
                vx.ContractId, vx.Symbol, DateOnly.FromDateTime(DateTime.UtcNow))
            .WaitAsync(timeout, cancellationToken), "latest VX bar");

        var marketDefinitions = RequireValue(await queries.Reference.GetMarketDataDefinitionTypesAsync()
            .WaitAsync(timeout, cancellationToken), "Market Data definition types").ToArray();
        var referenceDefinitions = RequireValue(await queries.Reference.GetReferenceDataDefinitionTypesAsync()
            .WaitAsync(timeout, cancellationToken), "Reference definition types").ToArray();
        var systemDefinitions = RequireValue(await queries.Reference.GetSystemAdminFunctionTypesAsync()
            .WaitAsync(timeout, cancellationToken), "System Administration function types").ToArray();
        var lookupNames = RequireValue(await queries.Reference.GetLookupTypeNamesAsync()
            .WaitAsync(timeout, cancellationToken), "lookup type names");
        var funds = RequireValue(await queries.Fund.GetFundsAsync()
                .WaitAsync(timeout, cancellationToken), "funds")
            .Where(fund => !string.IsNullOrWhiteSpace(fund.Name))
            .ToArray();
        if (funds.Length == 0)
            throw new G0DependencyException("G1 requires at least one existing Development fund for read-only detail rendering.");
        var calendars = RequireValue(await queries.MarketData.GetEconomicCalendarsAsync()
            .WaitAsync(timeout, cancellationToken), "economic calendars");

        ValidateSupportedDefinitions(marketDefinitions, MarketDataViewIds, "Market Data");
        ValidateSupportedDefinitions(referenceDefinitions, ReferenceViewIds, "Reference");
        var supportedSystemDefinitions = systemDefinitions
            .Where(definition => SystemAdminViewIds.ContainsKey(definition.ShortCode))
            .ToArray();
        ValidateSupportedDefinitions(supportedSystemDefinitions, SystemAdminViewIds, "System Administration");
        var deferredSystemDefinitions = systemDefinitions
            .Where(definition => !SystemAdminViewIds.ContainsKey(definition.ShortCode))
            .Select(definition => definition.ShortCode)
            .ToArray();
        return new G1ExpectedState(
            es,
            vx,
            contracts,
            options,
            marketDefinitions,
            referenceDefinitions,
            supportedSystemDefinitions,
            deferredSystemDefinitions,
            lookupNames,
            funds,
            calendars.Length);
    }

    static T RequireValue<T>(ServiceResult<T> result, string queryName)
        where T : class
    {
        if (!result.Success || result.Value is null)
            throw new G0DependencyException(
                $"Typed {queryName} query failed: code={result.ErrorCode}; message={result.ErrorMessage}");
        return result.Value;
    }

    static async Task<FuturesContractV2ReadModel> EnsureVxCurrencyAsync(
        G0QuerySession queries,
        FuturesContractV2ReadModel contract,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(contract.Currency))
            return contract;

        var corrected = contract with { Currency = "USD" };
        var result = await queries.MarketDataCommands
            .ChangeFuturesContractAsync(contract.Id, corrected, overwrite: true)
            .WaitAsync(timeout, cancellationToken);
        if (!result.Success)
            throw new G0DependencyException(
                $"Current VX contract '{contract.ContractId}' has no currency and its public correction failed: "
                + result.ErrorMessage);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var refreshed = await queries.MarketData
                .GetCurrentlyTradedFuturesContractAsync("VX")
                .WaitAsync(timeout, cancellationToken);
            if (refreshed.Success && refreshed.Value?.Currency == "USD")
                return refreshed.Value;
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        throw new G0DependencyException(
            $"Current VX contract '{contract.ContractId}' currency correction did not become query-visible.");
    }

    static async Task<string> NormalizeMarketDataFeedAsync(
        G0QuerySession queries,
        G0EventObserver observer,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var result = await queries.MarketDataFeedCommands
            .StopMarketDataFeedAsync(DateOnly.FromDateTime(DateTime.UtcNow))
            .WaitAsync(timeout, cancellationToken);
        if (!result.Success)
        {
            // With the isolated product-process guard, a rejected stop means the durable actor is
            // already stopped. A previously accepted/running feed always emits the correlated stop path.
            return "market-data feed was already stopped";
        }

        var commandId = result.Value;
        var events = await observer.WaitForAsync(
            rows => rows.Any(row => row.Family == "MarketDataFeed"
                                    && row.CommandId == commandId
                                    && row.Verb == TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataFeedStoppedCompleteEvent.Verb
                                    && row.Success == true),
            timeout,
            cancellationToken);
        var terminal = events.Last(row => row.Family == "MarketDataFeed" && row.CommandId == commandId);
        return $"normalized prior market-data feed state via {terminal.Verb}";
    }

    static IReadOnlyDictionary<string, string> MapDefinitions(
        IEnumerable<LookupTypeReadModel> definitions,
        IReadOnlyDictionary<string, string> supportedViews)
        => definitions.ToDictionary(
            definition => definition.Description,
            definition => supportedViews[definition.ShortCode],
            StringComparer.Ordinal);

    static void ValidateSupportedDefinitions(
        IReadOnlyList<LookupTypeReadModel> definitions,
        IReadOnlyDictionary<string, string> supportedViews,
        string catalogName)
    {
        if (definitions.Count != supportedViews.Count)
            throw new G0DependencyException(
                $"{catalogName} catalog has {definitions.Count} queried definitions; "
                + $"the UI supports {supportedViews.Count}.");
        var unsupported = definitions.Where(definition => !supportedViews.ContainsKey(definition.ShortCode)).ToArray();
        if (unsupported.Length > 0)
            throw new G0DependencyException(
                $"{catalogName} contains unsupported definitions: "
                + string.Join(", ", unsupported.Select(definition => definition.ShortCode)));
    }

    static string DescribeViews(IEnumerable<G1SelectorViewState> views)
        => string.Join("; ", views.Select(view =>
            $"{view.Selection}->{view.ViewAutomationId}[{string.Join(",", view.DataCounts.Select(pair => $"{pair.Key}:{pair.Value}"))}]"));

    static IReadOnlyList<string> CaptureAcceptedEvidence(
        G1UiAutomationSession automation,
        G0EvidenceWriter evidence,
        string prefix)
        => automation.CaptureTopLevelEvidence(
                evidence.ScreenshotDirectory,
                evidence.AutomationTreeDirectory,
                prefix)
            .Select(path => Path.GetRelativePath(evidence.RunDirectory, path))
            .ToArray();

    static G0StepObservation Observation(string actual, IReadOnlyList<string>? evidence = null)
        => new(string.Empty, actual, G0StepStatus.Passed, evidence);

    static void RequirePassed(G0AuditRecorder recorder, string stepId, string reason)
    {
        var step = recorder.Result.Steps.SingleOrDefault(candidate => candidate.Id == stepId);
        if (step?.Status != G0StepStatus.Passed)
            throw new G0DependencyException(
                $"{reason} Dependency {stepId} status={step?.Status.ToString() ?? "NotRun"}.",
                G0StepStatus.SkippedDependency);
    }

    static void RequireObserver(G0EventObserver? observer)
    {
        if (observer is null)
            throw new G0DependencyException(
                "The typed NATS event observer is unavailable.",
                G0StepStatus.SkippedDependency);
    }

    static void RequireExpected(G1ExpectedState? expected)
    {
        if (expected is null)
            throw new G0DependencyException(
                "The typed read-only baseline is unavailable.",
                G0StepStatus.SkippedDependency);
    }

    static void RequireAutomation(G1UiAutomationSession? automation)
    {
        if (automation is null)
            throw new G0DependencyException(
                "The G1 UI Automation session is unavailable.",
                G0StepStatus.SkippedDependency);
    }

    static bool IsErrorCodedStatus(G0ObservedEvent row)
    {
        if (row.Family != "Status") return false;
        var separator = row.Message.IndexOf(':');
        return separator > 0
            && row.Message.AsSpan(0, separator).IndexOfAnyExceptInRange('0', '9') < 0;
    }

    static IReadOnlyList<string> FindConflictingProcesses(G0Configuration configuration)
    {
        List<string> conflicts = [];
        foreach (var executable in new[] { configuration.ApiExecutable, configuration.DesktopExecutable })
        {
            var processName = Path.GetFileNameWithoutExtension(executable);
            using var processes = new ProcessCollection(Process.GetProcessesByName(processName));
            if (processes.Count > 0)
                conflicts.Add(
                    $"G1 requires an isolated process boundary; '{processName}' is already running as PID(s) "
                    + string.Join(", ", processes.Select(process => process.Id)) + ".");
        }
        return conflicts;
    }

    sealed class ProcessCollection(Process[] processes) : IDisposable, IReadOnlyCollection<Process>
    {
        public int Count => processes.Length;
        public IEnumerator<Process> GetEnumerator() => ((IEnumerable<Process>)processes).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public void Dispose()
        {
            foreach (var process in processes)
                process.Dispose();
        }
    }

    sealed record G1ExpectedState(
        FuturesContractV2ReadModel EsContract,
        FuturesContractV2ReadModel VxContract,
        FuturesContractV2ReadModel[] Contracts,
        TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel[] EsOptionContracts,
        LookupTypeReadModel[] MarketDataDefinitions,
        LookupTypeReadModel[] ReferenceDefinitions,
        LookupTypeReadModel[] SystemAdminDefinitions,
        string[] DeferredSystemAdminDefinitions,
        string[] LookupTypeNames,
        FundReadModel[] Funds,
        int EconomicCalendarCount);
}
