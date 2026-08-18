using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FluentAssertions;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

namespace TomasAI.IFM.UI.Net.SystemTests.Commands;

[Trait("Category", "G2StartupProcess")]
public sealed class G2PrerequisiteAndStartupAuditTests
{
    const int ExpectedStepCount = 29;

    [Fact]
    public async Task Development_command_audit_satisfies_G2_001_through_G2_029()
    {
        if (!G0Configuration.G2StartupLiveRunEnabled)
            return;

        var securitiesSlice = string.Equals(
            Environment.GetEnvironmentVariable("IFM_G2_SECURITIES_SLICE"),
            "1",
            StringComparison.Ordinal);
        var yieldCurveSlice = string.Equals(
            Environment.GetEnvironmentVariable("IFM_G2_YIELD_CURVE_SLICE"),
            "1",
            StringComparison.Ordinal);
        var economicCalendarSlice = string.Equals(
            Environment.GetEnvironmentVariable("IFM_G2_ECONOMIC_CALENDAR_SLICE"),
            "1",
            StringComparison.Ordinal);
        var lookupSlice = string.Equals(
            Environment.GetEnvironmentVariable("IFM_G2_LOOKUP_SLICE"),
            "1",
            StringComparison.Ordinal);
        var fundSlice = string.Equals(
            Environment.GetEnvironmentVariable("IFM_G2_FUND_SLICE"),
            "1",
            StringComparison.Ordinal);
        if (new[] { securitiesSlice, yieldCurveSlice, economicCalendarSlice, lookupSlice, fundSlice }.Count(enabled => enabled) > 1)
            throw new InvalidOperationException(
                "IFM_G2_SECURITIES_SLICE, IFM_G2_YIELD_CURVE_SLICE, and "
                + "IFM_G2_ECONOMIC_CALENDAR_SLICE, IFM_G2_LOOKUP_SLICE, and IFM_G2_FUND_SLICE are mutually exclusive.");
        var configuration = G2Configuration.Load();
        var process = configuration.Process;
        var redactor = new SecretRedactor([Environment.GetEnvironmentVariable("FMP_API_KEY")]);
        var evidence = new G0EvidenceWriter(process, redactor);
        var run = new G0RunResult
        {
            Gate = securitiesSlice
                ? "G2-001-007+010-015"
                : yieldCurveSlice
                    ? "G2-001-007+016-019"
                    : economicCalendarSlice
                        ? "G2-001-007+020-023"
                        : lookupSlice
                            ? "G2-001-007+024-026"
                            : fundSlice
                                ? "G2-001-007+027-029"
                                : "G2-001-029",
            ExpectedStepCount = securitiesSlice || yieldCurveSlice || economicCalendarSlice || lookupSlice || fundSlice
                ? securitiesSlice ? 13 : lookupSlice || fundSlice ? 10 : 11
                : ExpectedStepCount,
            RunId = process.RunId,
            Environment = process.EnvironmentName,
            StartedUtc = DateTimeOffset.UtcNow,
            ApiExecutable = process.ApiExecutable,
            DesktopExecutable = process.DesktopExecutable,
            Endpoints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["apiReadiness"] = process.ApiReadyUri.ToString(),
                ["nats"] = process.NatsUri.ToString(),
                ["postgresql"] = $"{process.PostgreSql.Host}:{process.PostgreSql.Port}",
                ["scyllaDb"] = $"{process.ScyllaDb.Host}:{process.ScyllaDb.Port}",
                ["redis"] = $"{process.Redis.Host}:{process.Redis.Port}"
            }
        };
        var recorder = new G0AuditRecorder(run);
        using var auditTimeout = new CancellationTokenSource(process.AuditTimeout);
        var cancellationToken = auditTimeout.Token;

        OwnedProcess? api = null;
        OwnedProcess? desktop = null;
        G0EventObserver? startupObserver = null;
        G2CommandEventObserver? commandObserver = null;
        G0QuerySession? queries = null;
        G1UiAutomationSession? automation = null;
        G2BaselineSnapshot? baseline = null;
        G2SecuritiesFixture? securitiesFixture = null;
        G2YieldCurveFixture? yieldCurveFixture = null;
        G2EconomicCalendarFixture? economicCalendarFixture = null;
        G2LookupTypeFixture? lookupTypeFixture = null;
        G2FundFixture? fundFixture = null;
        FundReadModel? designatedFund = null;
        decimal? fundStartingBalance = null;
        Window? marketDataWindow = null;
        Window? referenceWindow = null;
        Window? fundWindow = null;
        var cleanupFailures = new List<string>();

        try
        {
            await SyncStep("G2-001", "Validate configuration and exclusive process ownership",
                "Development executable/evidence paths are valid, the target is non-production, and no competing IFM product process exists.",
                _ =>
                {
                    var errors = configuration.Validate()
                        .Concat(FindConflictingProcesses(process))
                        .ToArray();
                    if (errors.Length > 0)
                        throw new G0DependencyException(string.Join(Environment.NewLine, errors));
                    return Observation(
                        $"Configuration valid; environment={process.EnvironmentName}; evidence={evidence.RunDirectory}.",
                        ["result.json", "summary.md"]);
                });

            await Step("G2-002", "Probe NATS, PostgreSQL, ScyllaDB, and Redis",
                "All configured dependencies accept connections before any G2 mutation is permitted.",
                async token =>
                {
                    RequirePassed(recorder, "G2-001", "Validated non-production ownership is required before probing.");
                    List<string> failures = [];
                    foreach (var endpoint in new[]
                             {
                                 new G0Endpoint("NATS", process.NatsUri.Host, process.NatsUri.Port),
                                 process.PostgreSql,
                                 process.ScyllaDb,
                                 process.Redis
                             })
                    {
                        try
                        {
                            await InfrastructureProbe.ProbeTcpAsync(endpoint, process.ReadinessTimeout, token);
                        }
                        catch (G0DependencyException exception)
                        {
                            failures.Add(exception.Message);
                        }
                    }
                    if (failures.Count > 0)
                        throw new G0DependencyException(string.Join(Environment.NewLine, failures));
                    startupObserver = new G0EventObserver(process.NatsUri);
                    await startupObserver.StartAsync(token);
                    return Observation("NATS, PostgreSQL, ScyllaDB, and Redis are reachable; startup event evidence is active.");
                });

            await Step("G2-003", "Validate and record the mutation safety policy",
                "Only test database identities, an ignored run-specific backup destination, fixed provider parameters, a unique prefix, and a designated fund fixture are approved.",
                async token =>
                {
                    RequirePassed(recorder, "G2-001", "Valid G2 configuration is required by the safety policy.");
                    var errors = configuration.Validate();
                    if (errors.Count > 0)
                        throw new G0DependencyException(string.Join(Environment.NewLine, errors));
                    Directory.CreateDirectory(configuration.BackupDestinationRoot);
                    await evidence.WriteTextAsync(
                        Path.Combine("processes", "g2-safety-policy.json"),
                        JsonSerializer.Serialize(configuration.ToSafeEvidence(), new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    return Observation(
                        $"runPrefix={configuration.RunPrefix}; importDate={configuration.ImportDate:yyyy-MM-dd}; "
                        + $"countries=[{string.Join(',', configuration.ImportCountryCodes)}]; "
                        + $"databases=[{string.Join(',', configuration.DatabaseIdentities.Select(item => item.DatabaseName))}]; "
                        + $"fundFixture='{configuration.FundFixtureName}'; backupRoot='{configuration.BackupDestinationRoot}'.",
                        ["processes/g2-safety-policy.json"]);
                });

            await Step("G2-004", "Start the actor backend",
                $"The harness-owned API becomes Healthy with {process.ExpectedActorTypeCount} registered actor types.",
                async token =>
                {
                    RequirePassed(recorder, "G2-002", "All Development dependencies are required by the API.");
                    RequirePassed(recorder, "G2-003", "The mutation safety policy must be approved before starting G2.");
                    Dictionary<string, string?> apiEnvironment = new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ASPNETCORE_ENVIRONMENT"] = process.EnvironmentName
                    };
                    if (yieldCurveSlice || economicCalendarSlice || fundSlice)
                        apiEnvironment["AppSettings__Databento__DataSource"] = "Synthetic";
                    api = OwnedProcess.Start(
                        process.ApiExecutable,
                        evidence.ApiLogDirectory,
                        redactor,
                        apiEnvironment);
                    run.ApiProcessId = api.Process.Id.ToString();
                    await evidence.WriteTextAsync(Path.Combine("processes", "api-start.json"), api.Describe(), token);
                    var readiness = await InfrastructureProbe.WaitForApiReadinessAsync(
                        process.ApiReadyUri,
                        process.ReadinessTimeout,
                        token,
                        () => (api.Process.HasExited, api.Process.HasExited ? api.Process.ExitCode : null));
                    await evidence.WriteTextAsync(
                        Path.Combine("network", "api-readiness.json"),
                        JsonSerializer.Serialize(readiness, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    if (!string.Equals(readiness.Status, "Healthy", StringComparison.OrdinalIgnoreCase)
                        || readiness.RegisteredActorTypes != process.ExpectedActorTypeCount)
                        throw new InvalidOperationException(
                            $"API readiness was {readiness.Status}; actorTypes={readiness.RegisteredActorTypes}.");
                    return Observation(
                        $"API PID {api.Process.Id} is Healthy; registeredActorTypes={readiness.RegisteredActorTypes}; "
                        + $"feedSource={(yieldCurveSlice || economicCalendarSlice || fundSlice ? "Synthetic (isolated non-feed slice)" : "Development configuration")}.",
                        ["processes/api-start.json", "network/api-readiness.json"]);
                });

            await Step("G2-005", "Establish the reversible baseline",
                "Typed queries capture import-date, run-owned, and designated-fund state before desktop startup; the unique run prefix owns no pre-existing mutable row.",
                async token =>
                {
                    RequirePassed(recorder, "G2-004", "A ready actor backend is required by baseline queries.");
                    queries = new G0QuerySession(process.NatsUri);
                    await queries.StartAsync(process.RunId, token, "G2");
                    baseline = await G2BaselineCapture.CaptureAsync(
                        queries,
                        configuration,
                        process.ReadinessTimeout,
                        token);
                    if (baseline.RunOwnedFuturesContracts.Length > 0
                        || baseline.RunOwnedFuturesOptions.Length > 0
                        || baseline.RunOwnedLookupTypes.Length > 0)
                        throw new G0DependencyException(
                            $"Unique run prefix '{configuration.RunPrefix}' already owns mutable Development state.");
                    if (!yieldCurveSlice && !economicCalendarSlice && !lookupSlice && !fundSlice
                        && (baseline.SecuritiesFixtureContract is not null
                            || baseline.SecuritiesFixtureOption is not null))
                        throw new G0DependencyException(
                            $"Exact G2 securities fixture already exists: futures={configuration.SecuritiesFuturesContractId}; "
                            + $"option={configuration.SecuritiesOptionContractId}.");
                    if (!securitiesSlice && !economicCalendarSlice && !lookupSlice && !fundSlice
                        && baseline.YieldCurveManualDateRows.Length > 0)
                        throw new G0DependencyException(
                            $"Exact G2 manual yield-curve fixture already exists for {configuration.YieldCurveManualDate:yyyy-MM-dd}.");
                    if (!securitiesSlice && !yieldCurveSlice && !lookupSlice && !fundSlice
                        && baseline.EconomicCalendarManualDateRows.Length > 0)
                        throw new G0DependencyException(
                            $"Exact G2 manual economic-calendar fixture date already contains "
                            + $"{baseline.EconomicCalendarManualDateRows.Length} row(s) for "
                            + $"{configuration.EconomicCalendarManualDate:yyyy-MM-dd}/{configuration.ImportCountryCodes[0]}.");
                    if (!yieldCurveSlice && !economicCalendarSlice && !lookupSlice && !fundSlice)
                        securitiesFixture = await G2SecuritiesFixture.CreateAsync(
                            queries,
                            configuration,
                            process.ReadinessTimeout,
                            token);
                    if (!securitiesSlice && !economicCalendarSlice && !lookupSlice && !fundSlice)
                        yieldCurveFixture = await G2YieldCurveFixture.CreateAsync(
                            queries,
                            configuration,
                            process.ReadinessTimeout,
                            token);
                    if (!securitiesSlice && !yieldCurveSlice && !lookupSlice && !fundSlice)
                        economicCalendarFixture = await G2EconomicCalendarFixture.CreateAsync(
                            queries,
                            configuration,
                            process.ReadinessTimeout,
                            token);
                    if (!securitiesSlice && !yieldCurveSlice && !economicCalendarSlice && !fundSlice)
                        lookupTypeFixture = await G2LookupTypeFixture.CreateAsync(
                            queries,
                            configuration,
                            process.ReadinessTimeout,
                            token);
                    fundFixture = G2FundFixture.Create(configuration);
                    if (baseline.DesignatedFund is { IsProduction: true })
                        throw new G0DependencyException(
                            $"Designated G2 fund '{configuration.FundFixtureName}' is marked as production.");
                    if (baseline.DesignatedFundTransactions.Any(transaction =>
                            transaction.Description.Contains(configuration.RunPrefix, StringComparison.OrdinalIgnoreCase)))
                        throw new G0DependencyException(
                            $"Unique run prefix '{configuration.RunPrefix}' already owns a designated-fund transaction.");
                    await evidence.WriteTextAsync(
                        Path.Combine("processes", "g2-baseline.json"),
                        JsonSerializer.Serialize(baseline, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    await evidence.WriteTextAsync(
                        Path.Combine("processes", "g2-securities-fixture.json"),
                        JsonSerializer.Serialize(securitiesFixture, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    await evidence.WriteTextAsync(
                        Path.Combine("processes", "g2-yield-curve-fixture.json"),
                        JsonSerializer.Serialize(yieldCurveFixture, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    await evidence.WriteTextAsync(
                        Path.Combine("processes", "g2-economic-calendar-fixture.json"),
                        JsonSerializer.Serialize(economicCalendarFixture, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    await evidence.WriteTextAsync(
                        Path.Combine("processes", "g2-lookup-type-fixture.json"),
                        JsonSerializer.Serialize(lookupTypeFixture, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    await evidence.WriteTextAsync(
                        Path.Combine("processes", "g2-fund-fixture.json"),
                        JsonSerializer.Serialize(fundFixture, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    return Observation(
                        $"valueDate={baseline.ValueDate:yyyy-MM-dd}; importDate={baseline.ImportDate:yyyy-MM-dd}; "
                        + $"securitiesFixture={(securitiesFixture is null ? "not-in-slice" : $"{securitiesFixture.FuturesContractId}/{securitiesFixture.OptionContractId}")}; "
                        + $"manualYieldDate={configuration.YieldCurveManualDate:yyyy-MM-dd}; "
                        + $"manualYieldRows={baseline.YieldCurveManualDateRows.Length}; "
                        + $"yieldRows={baseline.YieldCurveImportDateRows.Length}; "
                        + $"manualCalendarDate={configuration.EconomicCalendarManualDate:yyyy-MM-dd}; "
                        + $"manualCalendarRows={baseline.EconomicCalendarManualDateRows.Length}; "
                        + $"calendarRows={baseline.EconomicCalendarImportDateRows.Sum(pair => pair.Value.Length)}; "
                        + $"lookupFixture={(lookupTypeFixture is null ? "not-in-slice" : lookupTypeFixture.AddedLookupType.LookupTypeName)}; "
                        + $"runOwnedLookupRows={baseline.RunOwnedLookupTypes.Length}; "
                        + $"designatedFund={(baseline.DesignatedFund is null ? "absent" : $"{baseline.DesignatedFund.FundId}:{baseline.DesignatedFund.Name}")}; "
                        + $"fundTransactions={baseline.DesignatedFundTransactions.Length}; fundOrders={baseline.DesignatedFundOrders.Length}; fundTrades={baseline.DesignatedFundTrades.Length}.",
                        ["processes/g2-baseline.json", "processes/g2-securities-fixture.json", "processes/g2-yield-curve-fixture.json", "processes/g2-economic-calendar-fixture.json", "processes/g2-lookup-type-fixture.json", "processes/g2-fund-fixture.json"]);
                });

            await Step("G2-006", "Launch the desktop and await initialized shell",
                "The real WinForms shell completes startup and enables all supported primary navigation actions.",
                async token =>
                {
                    RequirePassed(recorder, "G2-005", "The reversible baseline must precede desktop startup.");
                    desktop = OwnedProcess.Start(process.DesktopExecutable, evidence.UiLogDirectory, redactor);
                    run.DesktopProcessId = desktop.Process.Id.ToString();
                    await evidence.WriteTextAsync(Path.Combine("processes", "desktop-start.json"), desktop.Describe(), token);
                    automation = new G1UiAutomationSession(desktop.Process.Id);
                    var window = await automation.WaitForMainWindowAsync(process.StartupTimeout, token);
                    var shell = await automation.WaitForInitializedShellAsync(process.StartupTimeout, token);
                    var artifacts = CaptureAcceptedEvidence(automation, evidence, "G2-006-shell");
                    return Observation(
                        $"Initialized window='{window.Title}'; status='{shell.Status}'; "
                        + $"toolbar={string.Join(',', shell.Toolbar.Select(pair => $"{pair.Key}:{pair.Value}"))}.",
                        ["processes/desktop-start.json", .. artifacts]);
                });

            await Step("G2-007", "Establish command evidence listeners",
                "All required G2 command families have complete/fail routes and the shared exact-ID NATS observer is running before any maintenance submission.",
                async token =>
                {
                    RequirePassed(recorder, "G2-006", "An initialized shell is required before arming UI command evidence.");
                    var registrations = G2CommandEventObserver.Registrations;
                    string[] requiredFamilies =
                    [
                        "MarketDataFeed", "FuturesContract", "FuturesOptionContract", "YieldCurve",
                        "EconomicCalendar", "LookupType", "Fund", "FundTransaction", "FundOrder",
                        "EndOfDay", "DatabaseBackup"
                    ];
                    var incomplete = requiredFamilies.Where(family =>
                            !registrations.Any(item => item.Family == family && item.Success == true)
                            || !registrations.Any(item => item.Family == family && item.Success == false))
                        .ToArray();
                    if (incomplete.Length > 0)
                        throw new InvalidOperationException(
                            "G2 command evidence lacks complete/fail routes for: " + string.Join(", ", incomplete));
                    if (registrations.GroupBy(item => (item.Actor, item.Verb)).Any(group => group.Count() != 1))
                        throw new InvalidOperationException("G2 command evidence contains duplicate actor/verb routes.");

                    commandObserver = new G2CommandEventObserver(process.NatsUri);
                    await commandObserver.StartAsync(process.RunId, token);
                    if (commandObserver.State != EventListenerState.Running)
                        throw new InvalidOperationException($"G2 command observer state is {commandObserver.State}.");
                    await commandObserver.WriteEvidenceAsync(evidence, token);
                    return Observation(
                        $"Command observer is Running with families={requiredFamilies.Length}, "
                        + $"mailboxes={registrations.Select(item => item.Actor).Distinct(StringComparer.Ordinal).Count()}, "
                        + $"sourceRoutes={registrations.Count(item => item.Success is null)}, "
                        + $"terminalRoutes={registrations.Count(item => item.Success.HasValue)}.",
                        ["network/g2-command-listener-catalog.json", "network/g2-command-events.json"]);
                });

            if (!securitiesSlice && !yieldCurveSlice && !economicCalendarSlice && !lookupSlice && !fundSlice)
                await Step("G2-008", "Start the current market-data feed from the UI",
                "One UI start command is correlated from source event to successful terminal event and the shell shows the feed as active.",
                async token =>
                {
                    RequirePassed(recorder, "G2-007", "The exact-ID command observer is required before the UI feed action.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");

                    var initial = await ui.WaitForMarketDataFeedStateAsync(
                        isActive: true,
                        process.ReadinessTimeout,
                        token);
                    var normalized = await ToggleMarketDataFeedAsync(
                        ui,
                        observer,
                        sourceEventName: nameof(MarketDataFeedStoppedEvent),
                        expectedActiveState: false,
                        process.ReadinessTimeout,
                        token);
                    var started = await ToggleMarketDataFeedAsync(
                        ui,
                        observer,
                        sourceEventName: nameof(MarketDataFeedStartedEvent),
                        expectedActiveState: true,
                        process.ReadinessTimeout,
                        token);
                    await WriteFeedEvidenceAsync(evidence, observer, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-008-feed-started");
                    return Observation(
                        $"Startup state={initial.Action}; normalizedStop={normalized.CommandId}; "
                        + $"startCommand={started.CommandId}; terminal={started.TerminalEventName}; "
                        + $"uiAction={started.UiState.Action}.",
                        ["network/g2-market-data-feed-events.json", .. artifacts]);
                });

            if (!securitiesSlice && !yieldCurveSlice && !economicCalendarSlice && !lookupSlice && !fundSlice)
                await Step("G2-009", "Stop the current market-data feed from the UI",
                "One UI stop command is correlated from source event to successful terminal event and the shell shows the feed as inactive.",
                async token =>
                {
                    RequirePassed(recorder, "G2-008", "The G2-owned market-data feed must be active before it is stopped.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var stopped = await ToggleMarketDataFeedAsync(
                        ui,
                        observer,
                        sourceEventName: nameof(MarketDataFeedStoppedEvent),
                        expectedActiveState: false,
                        process.ReadinessTimeout,
                        token);
                    await WriteFeedEvidenceAsync(evidence, observer, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-009-feed-stopped");
                    return Observation(
                        $"stopCommand={stopped.CommandId}; terminal={stopped.TerminalEventName}; "
                        + $"uiAction={stopped.UiState.Action}; backend stop completed before inactive-state acceptance.",
                        ["network/g2-market-data-feed-events.json", .. artifacts]);
                });

            if (!yieldCurveSlice && !economicCalendarSlice && !lookupSlice && !fundSlice)
            {
            await Step("G2-010", "Add a futures contract from the UI",
                "The UI adds the exact run-owned futures fixture, its source and successful terminal events correlate by command ID, and the typed query returns the durable row.",
                async token =>
                {
                    RequirePassed(
                        recorder,
                        securitiesSlice ? "G2-007" : "G2-009",
                        securitiesSlice
                            ? "The safety/startup prerequisites must complete before securities maintenance."
                            : "The isolated feed lifecycle must complete before securities maintenance.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = securitiesFixture ?? throw new InvalidOperationException("G2 securities fixture is unavailable.");

                    ui.InvokeToolbarAction("MarketData");
                    marketDataWindow = await ui.WaitForWindowAsync(
                        "Market Data Manager", process.ReadinessTimeout, token);
                    var transition = await ExecuteSecuritiesMutationAsync(
                        observer,
                        "FuturesContract",
                        nameof(FuturesContractAddedEvent),
                        operationToken => ui.AddFuturesContractAsync(
                            marketDataWindow, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForFuturesContractAsync(
                        querySession,
                        fixture.FuturesContractId,
                        expectedDescription: null,
                        present: true,
                        process.ReadinessTimeout,
                        token);
                    await WriteSecuritiesEvidenceAsync(evidence, observer, "G2-010", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-010-futures-added");
                    return Observation(
                        $"contract={fixture.FuturesContractId}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; description='{durable?.Description}'.",
                        ["network/g2-securities-command-events.json", "queries/G2-010.json", .. artifacts]);
                });

            await Step("G2-011", "Change the futures contract from the UI",
                "The UI changes the run-owned futures description without changing its identity, correlates a successful terminal event, and the typed query returns the changed durable row.",
                async token =>
                {
                    RequirePassed(recorder, "G2-010", "The run-owned futures fixture must exist before change.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = securitiesFixture ?? throw new InvalidOperationException("G2 securities fixture is unavailable.");
                    var window = marketDataWindow ?? throw new InvalidOperationException("Market Data Manager is unavailable.");

                    var transition = await ExecuteSecuritiesMutationAsync(
                        observer,
                        "FuturesContract",
                        nameof(FuturesContractChangedEvent),
                        operationToken => ui.ChangeFuturesContractAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForFuturesContractAsync(
                        querySession,
                        fixture.FuturesContractId,
                        fixture.FuturesChangedDescription,
                        present: true,
                        process.ReadinessTimeout,
                        token);
                    var refreshedUi = await ui.ReloadFuturesContractAsync(
                        window,
                        fixture,
                        process.ReadinessTimeout,
                        token);
                    await WriteSecuritiesEvidenceAsync(evidence, observer, "G2-011", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-011-futures-changed");
                    return Observation(
                        $"contract={fixture.FuturesContractId}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; description='{durable?.Description}'; "
                        + $"uiDescription='{refreshedUi.Description}'.",
                        ["network/g2-securities-command-events.json", "queries/G2-011.json", .. artifacts]);
                });

            await Step("G2-012", "Add a futures option contract from the UI",
                "The UI adds the exact run-owned call-option fixture, correlates source and successful terminal events, and the typed symbol query returns the durable row.",
                async token =>
                {
                    RequirePassed(recorder, "G2-011", "The underlying futures fixture must be durably changed before adding its option.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = securitiesFixture ?? throw new InvalidOperationException("G2 securities fixture is unavailable.");
                    var window = marketDataWindow ?? throw new InvalidOperationException("Market Data Manager is unavailable.");

                    var transition = await ExecuteSecuritiesMutationAsync(
                        observer,
                        "FuturesOptionContract",
                        nameof(FuturesOptionContractAddedEvent),
                        operationToken => ui.AddFuturesOptionContractAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForFuturesOptionContractAsync(
                        querySession,
                        fixture,
                        expectedDescription: null,
                        present: true,
                        process.ReadinessTimeout,
                        token);
                    await WriteSecuritiesEvidenceAsync(evidence, observer, "G2-012", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-012-option-added");
                    return Observation(
                        $"contract={fixture.OptionContractId}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; description='{durable?.Description}'.",
                        ["network/g2-securities-command-events.json", "queries/G2-012.json", .. artifacts]);
                });

            await Step("G2-013", "Change the futures option contract from the UI",
                "The UI changes the run-owned option description without changing its identity, correlates a successful terminal event, and the typed query returns the changed durable row.",
                async token =>
                {
                    RequirePassed(recorder, "G2-012", "The run-owned option fixture must exist before change.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = securitiesFixture ?? throw new InvalidOperationException("G2 securities fixture is unavailable.");
                    var window = marketDataWindow ?? throw new InvalidOperationException("Market Data Manager is unavailable.");

                    var transition = await ExecuteSecuritiesMutationAsync(
                        observer,
                        "FuturesOptionContract",
                        nameof(FuturesOptionContractChangedEvent),
                        operationToken => ui.ChangeFuturesOptionContractAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForFuturesOptionContractAsync(
                        querySession,
                        fixture,
                        fixture.OptionChangedDescription,
                        present: true,
                        process.ReadinessTimeout,
                        token);
                    var refreshedUi = await ui.ReloadFuturesOptionContractAsync(
                        window,
                        fixture,
                        process.ReadinessTimeout,
                        token);
                    await WriteSecuritiesEvidenceAsync(evidence, observer, "G2-013", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-013-option-changed");
                    return Observation(
                        $"contract={fixture.OptionContractId}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; description='{durable?.Description}'; "
                        + $"uiDescription='{refreshedUi.Description}'.",
                        ["network/g2-securities-command-events.json", "queries/G2-013.json", .. artifacts]);
                });

            await Step("G2-014", "Remove the futures option contract from the UI",
                "The UI confirms removal of the exact run-owned option, correlates a successful terminal event, and the typed symbol query proves the durable row is absent.",
                async token =>
                {
                    RequirePassed(recorder, "G2-013", "The changed option fixture must exist before removal.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = securitiesFixture ?? throw new InvalidOperationException("G2 securities fixture is unavailable.");
                    var window = marketDataWindow ?? throw new InvalidOperationException("Market Data Manager is unavailable.");

                    var transition = await ExecuteSecuritiesMutationAsync(
                        observer,
                        "FuturesOptionContract",
                        nameof(FuturesOptionContractRemovedEvent),
                        operationToken => ui.RemoveFuturesOptionContractAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForFuturesOptionContractAsync(
                        querySession,
                        fixture,
                        expectedDescription: null,
                        present: false,
                        process.ReadinessTimeout,
                        token);
                    await WriteSecuritiesEvidenceAsync(evidence, observer, "G2-014", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-014-option-removed");
                    return Observation(
                        $"contract={fixture.OptionContractId}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; durablePresent={durable is not null}.",
                        ["network/g2-securities-command-events.json", "queries/G2-014.json", .. artifacts]);
                });

            await Step("G2-015", "Remove the futures contract from the UI",
                "The UI confirms removal of the exact run-owned futures contract after its option is absent, correlates a successful terminal event, and the typed query proves the durable row is absent.",
                async token =>
                {
                    RequirePassed(recorder, "G2-014", "The child option fixture must be absent before removing its futures contract.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = securitiesFixture ?? throw new InvalidOperationException("G2 securities fixture is unavailable.");
                    var window = marketDataWindow ?? throw new InvalidOperationException("Market Data Manager is unavailable.");

                    var transition = await ExecuteSecuritiesMutationAsync(
                        observer,
                        "FuturesContract",
                        nameof(FuturesContractRemovedEvent),
                        operationToken => ui.RemoveFuturesContractAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForFuturesContractAsync(
                        querySession,
                        fixture.FuturesContractId,
                        expectedDescription: null,
                        present: false,
                        process.ReadinessTimeout,
                        token);
                    await WriteSecuritiesEvidenceAsync(evidence, observer, "G2-015", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-015-futures-removed");
                    await ui.CloseWindowAsync(window, process.ReadinessTimeout, token);
                    marketDataWindow = null;
                    return Observation(
                        $"contract={fixture.FuturesContractId}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; durablePresent={durable is not null}.",
                        ["network/g2-securities-command-events.json", "queries/G2-015.json", .. artifacts]);
                });
            }

            if (!securitiesSlice && !economicCalendarSlice && !lookupSlice && !fundSlice)
            {
            await Step("G2-016", "Add an isolated yield-curve record manually",
                "The real editor submits the manual yield-curve add command without FMP, source and successful terminal events correlate by command ID, and durable/UI state contains the exact row.",
                async token =>
                {
                    RequirePassed(
                        recorder,
                        yieldCurveSlice ? "G2-007" : "G2-015",
                        yieldCurveSlice
                            ? "The safety/startup prerequisites must complete before yield-curve maintenance."
                            : "Securities maintenance must complete before yield-curve maintenance.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = yieldCurveFixture ?? throw new InvalidOperationException("G2 yield-curve fixture is unavailable.");

                    ui.InvokeToolbarAction("MarketData");
                    marketDataWindow = await ui.WaitForWindowAsync(
                        "Market Data Manager", process.ReadinessTimeout, token);
                    var transition = await ExecuteYieldCurveMutationAsync(
                        observer,
                        nameof(YieldCurveRateAddedEvent),
                        operationToken => ui.AddYieldCurveRateAsync(
                            marketDataWindow, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForYieldCurveRateAsync(
                        querySession,
                        fixture.ManualDate,
                        fixture.AddedRate,
                        present: true,
                        process.ReadinessTimeout,
                        token);
                    await WriteYieldCurveEvidenceAsync(evidence, observer, "G2-016", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-016-yield-curve-added");
                    return Observation(
                        $"date={fixture.ManualDate:yyyy-MM-dd}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; oneMonth={durable?.OneMonth:F2}; "
                        + $"uiRows={transition.UiState.Rows.Count}.",
                        ["network/g2-yield-curve-command-events.json", "queries/G2-016.json", .. artifacts]);
                });

            await Step("G2-017", "Change the isolated yield-curve record manually",
                "The real editor submits the domain change command, exact-ID completion succeeds, and durable/refreshed UI state contains all changed rates.",
                async token =>
                {
                    RequirePassed(recorder, "G2-016", "The isolated manual yield-curve row must exist before change.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = yieldCurveFixture ?? throw new InvalidOperationException("G2 yield-curve fixture is unavailable.");
                    var window = marketDataWindow ?? throw new InvalidOperationException("Market Data Manager is unavailable.");

                    var transition = await ExecuteYieldCurveMutationAsync(
                        observer,
                        nameof(YieldCurveRateChangedEvent),
                        operationToken => ui.ChangeYieldCurveRateAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForYieldCurveRateAsync(
                        querySession,
                        fixture.ManualDate,
                        fixture.ChangedRate,
                        present: true,
                        process.ReadinessTimeout,
                        token);
                    var refreshedUi = await ui.ReloadYieldCurveRateAsync(
                        window,
                        fixture,
                        fixture.ManualDate,
                        fixture.ChangedRate,
                        present: true,
                        process.ReadinessTimeout,
                        token);
                    await WriteYieldCurveEvidenceAsync(evidence, observer, "G2-017", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-017-yield-curve-changed");
                    return Observation(
                        $"date={fixture.ManualDate:yyyy-MM-dd}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; oneMonth={durable?.OneMonth:F2}; "
                        + $"uiRows={refreshedUi.Rows.Count}.",
                        ["network/g2-yield-curve-command-events.json", "queries/G2-017.json", .. artifacts]);
                });

            await Step("G2-018", "Remove the isolated yield-curve record manually",
                "The real editor confirms the domain remove command, exact-ID completion succeeds, and typed durable plus refreshed visible state prove the row is absent.",
                async token =>
                {
                    RequirePassed(recorder, "G2-017", "The changed manual yield-curve row must exist before removal.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = yieldCurveFixture ?? throw new InvalidOperationException("G2 yield-curve fixture is unavailable.");
                    var window = marketDataWindow ?? throw new InvalidOperationException("Market Data Manager is unavailable.");

                    var transition = await ExecuteYieldCurveMutationAsync(
                        observer,
                        nameof(YieldCurveRateRemovedEvent),
                        operationToken => ui.RemoveYieldCurveRateAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForYieldCurveRateAsync(
                        querySession,
                        fixture.ManualDate,
                        expectedRate: null,
                        present: false,
                        process.ReadinessTimeout,
                        token);
                    var refreshedUi = await ui.ReloadYieldCurveRateAsync(
                        window,
                        fixture,
                        fixture.ManualDate,
                        expectedRate: null,
                        present: false,
                        process.ReadinessTimeout,
                        token);
                    await WriteYieldCurveEvidenceAsync(evidence, observer, "G2-018", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-018-yield-curve-removed");
                    return Observation(
                        $"date={fixture.ManualDate:yyyy-MM-dd}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; durablePresent={durable is not null}; "
                        + $"uiRows={refreshedUi.Rows.Count}.",
                        ["network/g2-yield-curve-command-events.json", "queries/G2-018.json", .. artifacts]);
                });

            await Step("G2-019", "Import one FMP treasury-curve date from the UI",
                "The UI-selected date reaches the parameter-only import event, the domain handler acquires through IReferenceDataApi and emits exact-ID completion, and its canonical 0..N provider result matches durable and visible state.",
                async token =>
                {
                    RequirePassed(recorder, "G2-018", "Manual yield-curve maintenance must be clean before the provider import.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = yieldCurveFixture ?? throw new InvalidOperationException("G2 yield-curve fixture is unavailable.");
                    var window = marketDataWindow ?? throw new InvalidOperationException("Market Data Manager is unavailable.");

                    var transition = await ExecuteYieldCurveMutationAsync(
                        observer,
                        nameof(YieldCurveRatesImportedEvent),
                        operationToken => ui.ImportYieldCurveRatesAsync(
                            window, fixture, process.StartupTimeout, operationToken),
                        process.StartupTimeout,
                        token);
                    if (transition.ImportDate != fixture.ImportDate)
                        throw new InvalidOperationException(
                            $"The correlated FMP terminal event reported import date {transition.ImportDate:yyyy-MM-dd}; expected {fixture.ImportDate:yyyy-MM-dd}.");
                    var providerRates = transition.ImportedYieldCurveRates
                        ?? throw new InvalidOperationException("The successful FMP terminal event did not carry its canonical provider result.");
                    if (providerRates.Any(rate => rate.ValueDate != fixture.ImportDate)
                        || providerRates.Select(rate => rate.ValueDate).Distinct().Count() != providerRates.Length)
                        throw new InvalidOperationException(
                            "The single-date FMP terminal result contains an out-of-range or duplicate value date.");
                    var durable = await WaitForYieldCurveRatesAsync(
                        querySession,
                        fixture.ImportDate,
                        providerRates,
                        process.StartupTimeout,
                        token);
                    var expectedUiRate = providerRates.SingleOrDefault();
                    var refreshedUi = await ui.ReloadYieldCurveRateAsync(
                        window,
                        fixture,
                        fixture.ImportDate,
                        expectedUiRate,
                        present: expectedUiRate is not null,
                        process.ReadinessTimeout,
                        token);
                    await WriteYieldCurveEvidenceAsync(
                        evidence, observer, "G2-019", transition,
                        new { ProviderResult = providerRates, DurableState = durable, VisibleState = refreshedUi },
                        token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-019-fmp-treasury-imported");
                    await ui.CloseWindowAsync(window, process.ReadinessTimeout, token);
                    marketDataWindow = null;
                    return Observation(
                        $"importDate={fixture.ImportDate:yyyy-MM-dd}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; providerRows={providerRates.Length}; "
                        + $"durableRows={durable.Length}; uiRows={refreshedUi.Rows.Count}; adapter={process.FmpAdapter}.",
                        ["network/g2-yield-curve-command-events.json", "queries/G2-019.json", .. artifacts]);
                });
            }

            if (!securitiesSlice && !yieldCurveSlice && !lookupSlice && !fundSlice)
            {
            await Step("G2-020", "Add an isolated economic-calendar record manually",
                "The real editor submits the manual MarketData add command without FMP, source and successful terminal events correlate by command ID, and bounded durable/UI state contains the exact row.",
                async token =>
                {
                    RequirePassed(
                        recorder,
                        economicCalendarSlice ? "G2-007" : "G2-019",
                        economicCalendarSlice
                            ? "The safety/startup prerequisites must complete before economic-calendar maintenance."
                            : "Yield-curve maintenance must complete before economic-calendar maintenance.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = economicCalendarFixture ?? throw new InvalidOperationException("G2 economic-calendar fixture is unavailable.");

                    ui.InvokeToolbarAction("Reference");
                    referenceWindow = await ui.WaitForWindowAsync(
                        "Reference Data Manager", process.ReadinessTimeout, token);
                    var transition = await ExecuteEconomicCalendarMutationAsync(
                        observer,
                        nameof(EconomicCalendarAddedEvent),
                        operationToken => ui.AddEconomicCalendarAsync(
                            referenceWindow, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForEconomicCalendarsAsync(
                        querySession,
                        fixture.ManualDate,
                        fixture.CountryCode,
                        [fixture.AddedCalendar],
                        process.ReadinessTimeout,
                        token);
                    await WriteEconomicCalendarEvidenceAsync(
                        evidence, observer, "G2-020", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-020-economic-calendar-added");
                    return Observation(
                        $"id={fixture.AddedCalendar.Id}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; actual={durable.Single().Actual}; "
                        + $"uiRows={transition.UiState.Items.Count}.",
                        ["network/g2-economic-calendar-command-events.json", "queries/G2-020.json", .. artifacts]);
                });

            await Step("G2-021", "Change the isolated economic-calendar record manually",
                "The editor changes the run-owned values without changing identity, exact-ID completion succeeds, and bounded durable/refreshed UI state contains every changed field.",
                async token =>
                {
                    RequirePassed(recorder, "G2-020", "The isolated manual economic-calendar row must exist before change.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = economicCalendarFixture ?? throw new InvalidOperationException("G2 economic-calendar fixture is unavailable.");
                    var window = referenceWindow ?? throw new InvalidOperationException("Reference Data Manager is unavailable.");

                    var transition = await ExecuteEconomicCalendarMutationAsync(
                        observer,
                        nameof(EconomicCalendarChangedEvent),
                        operationToken => ui.ChangeEconomicCalendarAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForEconomicCalendarsAsync(
                        querySession,
                        fixture.ManualDate,
                        fixture.CountryCode,
                        [fixture.ChangedCalendar],
                        process.ReadinessTimeout,
                        token);
                    var refreshedUi = await ui.ReloadEconomicCalendarAsync(
                        window,
                        fixture.ManualDate,
                        fixture.CountryCode,
                        fixture.ChangedCalendar,
                        present: true,
                        process.ReadinessTimeout,
                        token);
                    await WriteEconomicCalendarEvidenceAsync(
                        evidence, observer, "G2-021", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-021-economic-calendar-changed");
                    return Observation(
                        $"id={fixture.ChangedCalendar.Id}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; actual={durable.Single().Actual}; "
                        + $"uiRows={refreshedUi.Items.Count}.",
                        ["network/g2-economic-calendar-command-events.json", "queries/G2-021.json", .. artifacts]);
                });

            await Step("G2-022", "Remove the isolated economic-calendar record manually",
                "The real editor confirms the domain remove command, exact-ID completion succeeds, and bounded durable plus refreshed visible state prove the row is absent.",
                async token =>
                {
                    RequirePassed(recorder, "G2-021", "The changed manual economic-calendar row must exist before removal.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = economicCalendarFixture ?? throw new InvalidOperationException("G2 economic-calendar fixture is unavailable.");
                    var window = referenceWindow ?? throw new InvalidOperationException("Reference Data Manager is unavailable.");

                    var transition = await ExecuteEconomicCalendarMutationAsync(
                        observer,
                        nameof(EconomicCalendarRemovedEvent),
                        operationToken => ui.RemoveEconomicCalendarAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForEconomicCalendarsAsync(
                        querySession,
                        fixture.ManualDate,
                        fixture.CountryCode,
                        [],
                        process.ReadinessTimeout,
                        token);
                    var refreshedUi = await ui.ReloadEconomicCalendarAsync(
                        window,
                        fixture.ManualDate,
                        fixture.CountryCode,
                        fixture.ChangedCalendar,
                        present: false,
                        process.ReadinessTimeout,
                        token);
                    await WriteEconomicCalendarEvidenceAsync(
                        evidence, observer, "G2-022", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-022-economic-calendar-removed");
                    return Observation(
                        $"id={fixture.ChangedCalendar.Id}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; durableRows={durable.Length}; "
                        + $"uiRows={refreshedUi.Items.Count}.",
                        ["network/g2-economic-calendar-command-events.json", "queries/G2-022.json", .. artifacts]);
                });

            await Step("G2-023", "Import one FMP economic-calendar date from the UI",
                "The UI-selected date/country reach the parameter-only import event, the domain handler acquires through IReferenceDataApi and emits exact-ID completion, and its canonical 0..N provider result agrees with bounded durable and visible state.",
                async token =>
                {
                    RequirePassed(recorder, "G2-022", "Manual economic-calendar maintenance must be clean before the provider import.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = economicCalendarFixture ?? throw new InvalidOperationException("G2 economic-calendar fixture is unavailable.");
                    var snapshot = baseline ?? throw new InvalidOperationException("G2 baseline is unavailable.");
                    var window = referenceWindow ?? throw new InvalidOperationException("Reference Data Manager is unavailable.");

                    var transition = await ExecuteEconomicCalendarMutationAsync(
                        observer,
                        nameof(EconomicCalendarsImportedEvent),
                        operationToken => ui.ImportEconomicCalendarsAsync(
                            window, fixture, process.StartupTimeout, operationToken),
                        process.StartupTimeout,
                        token);
                    if (transition.ImportDate != fixture.ImportDate)
                        throw new InvalidOperationException(
                            $"The correlated FMP terminal event reported import date {transition.ImportDate:yyyy-MM-dd}; expected {fixture.ImportDate:yyyy-MM-dd}.");
                    if (transition.ImportCountryCodes is null
                        || !transition.ImportCountryCodes.SequenceEqual([fixture.CountryCode], StringComparer.Ordinal))
                        throw new InvalidOperationException(
                            "The correlated FMP terminal event did not preserve the UI-selected country filter.");
                    var providerRows = transition.ImportedEconomicCalendars
                        ?? throw new InvalidOperationException(
                            "The successful FMP terminal event did not carry its canonical economic-calendar result.");
                    if (providerRows.Any(row => DateOnly.FromDateTime(row.EventDate) != fixture.ImportDate
                                                || !string.Equals(row.CountryCode, fixture.CountryCode, StringComparison.Ordinal))
                        || providerRows.Select(EconomicCalendarIdentity).Distinct(StringComparer.Ordinal).Count() != providerRows.Length)
                        throw new InvalidOperationException(
                            "The single-date FMP terminal result contains an out-of-range, wrong-country, or duplicate logical row.");
                    var expectedRows = MergeEconomicCalendars(
                        snapshot.EconomicCalendarImportDateRows[fixture.CountryCode],
                        providerRows);
                    var durable = await WaitForEconomicCalendarsAsync(
                        querySession,
                        fixture.ImportDate,
                        fixture.CountryCode,
                        expectedRows,
                        process.StartupTimeout,
                        token);
                    if (!EconomicCalendarsEqualWithMetadata(durable, expectedRows))
                        throw new InvalidOperationException(
                            "The bounded durable economic-calendar result did not preserve provider/baseline provenance metadata.");
                    if (transition.UiState.SelectedDate != fixture.ImportDate
                        || !string.Equals(transition.UiState.SelectedCountryCode, fixture.CountryCode, StringComparison.Ordinal)
                        || transition.UiState.Items.Count != durable.Length
                        || providerRows.Any(provider => !transition.UiState.Items.Any(item =>
                            item.Contains(provider.EventName, StringComparison.Ordinal))))
                        throw new InvalidOperationException(
                            "The economic-calendar editor did not visibly render the selected import date/country and accepted provider rows.");
                    await WriteEconomicCalendarEvidenceAsync(
                        evidence,
                        observer,
                        "G2-023",
                        transition,
                        new { ProviderResult = providerRows, ExpectedState = expectedRows, DurableState = durable, VisibleState = transition.UiState },
                        token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-023-fmp-economic-calendar-imported");
                    await ui.CloseWindowAsync(window, process.ReadinessTimeout, token);
                    referenceWindow = null;
                    return Observation(
                        $"importDate={fixture.ImportDate:yyyy-MM-dd}; country={fixture.CountryCode}; "
                        + $"command={transition.CommandId}; terminal={transition.TerminalEventName}; "
                        + $"providerRows={providerRows.Length}; durableRows={durable.Length}; "
                        + $"uiRows={transition.UiState.Items.Count}; adapter={process.FmpAdapter}.",
                        ["network/g2-economic-calendar-command-events.json", "queries/G2-023.json", .. artifacts]);
                });
            }

            if (!securitiesSlice && !yieldCurveSlice && !economicCalendarSlice && !fundSlice)
            {
            await Step("G2-024", "Add an isolated lookup value from the UI",
                "The real lookup editor submits one run-owned value, exact source/success events correlate by command ID, and the typed durable query plus refreshed UI show every business field.",
                async token =>
                {
                    RequirePassed(
                        recorder,
                        lookupSlice ? "G2-007" : "G2-023",
                        lookupSlice
                            ? "The safety/startup prerequisites must complete before lookup maintenance."
                            : "Economic-calendar maintenance must complete before lookup maintenance.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = lookupTypeFixture ?? throw new InvalidOperationException("G2 lookup fixture is unavailable.");

                    ui.InvokeToolbarAction("Reference");
                    referenceWindow = await ui.WaitForWindowAsync(
                        "Reference Data Manager", process.ReadinessTimeout, token);
                    var transition = await ExecuteLookupTypeMutationAsync(
                        observer,
                        nameof(LookupTypeAddedEvent),
                        operationToken => ui.AddLookupTypeAsync(
                            referenceWindow, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForLookupTypesAsync(
                        querySession,
                        fixture.AddedLookupType,
                        present: true,
                        process.ReadinessTimeout,
                        token);
                    await WriteLookupTypeEvidenceAsync(
                        evidence, observer, "G2-024", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-024-lookup-added");
                    return Observation(
                        $"id={fixture.AddedLookupType.Id}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; durableRows={durable.Length}; "
                        + $"uiShortCodes={transition.UiState.ShortCodes.Count}.",
                        ["network/g2-lookup-type-command-events.json", "queries/G2-024.json", .. artifacts]);
                });

            await Step("G2-025", "Change the isolated lookup value from the UI",
                "The editor changes the run-owned short code and description without changing partition/order identity, exact-ID completion succeeds, and durable/refreshed UI state contains only the changed value.",
                async token =>
                {
                    RequirePassed(recorder, "G2-024", "The isolated lookup value must exist before change.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = lookupTypeFixture ?? throw new InvalidOperationException("G2 lookup fixture is unavailable.");
                    var window = referenceWindow ?? throw new InvalidOperationException("Reference Data Manager is unavailable.");

                    var transition = await ExecuteLookupTypeMutationAsync(
                        observer,
                        nameof(LookupTypeChangedEvent),
                        operationToken => ui.ChangeLookupTypeAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForLookupTypesAsync(
                        querySession,
                        fixture.ChangedLookupType,
                        present: true,
                        process.ReadinessTimeout,
                        token);
                    if (durable.Any(row => string.Equals(
                            row.ShortCode,
                            fixture.AddedLookupType.ShortCode,
                            StringComparison.Ordinal)))
                        throw new InvalidOperationException("The original lookup short code remained after change.");
                    await WriteLookupTypeEvidenceAsync(
                        evidence, observer, "G2-025", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-025-lookup-changed");
                    return Observation(
                        $"id={fixture.ChangedLookupType.Id}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; shortCode={durable.Single().ShortCode}; "
                        + $"description='{transition.UiState.Description}'.",
                        ["network/g2-lookup-type-command-events.json", "queries/G2-025.json", .. artifacts]);
                });

            await Step("G2-026", "Remove the isolated lookup value from the UI",
                "The real editor confirms the run-owned removal, exact-ID completion succeeds, and the typed partition query plus refreshed lookup-name list prove the isolated partition is absent.",
                async token =>
                {
                    RequirePassed(recorder, "G2-025", "The changed lookup value must exist before removal.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = lookupTypeFixture ?? throw new InvalidOperationException("G2 lookup fixture is unavailable.");
                    var window = referenceWindow ?? throw new InvalidOperationException("Reference Data Manager is unavailable.");

                    var transition = await ExecuteLookupTypeMutationAsync(
                        observer,
                        nameof(LookupTypeRemovedEvent),
                        operationToken => ui.RemoveLookupTypeAsync(
                            window, fixture, process.ReadinessTimeout, operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durable = await WaitForLookupTypesAsync(
                        querySession,
                        fixture.ChangedLookupType,
                        present: false,
                        process.ReadinessTimeout,
                        token);
                    if (transition.UiState.LookupTypeNames.Contains(
                            fixture.ChangedLookupType.LookupTypeName,
                            StringComparer.Ordinal))
                        throw new InvalidOperationException("The removed lookup partition remains visible in the editor.");
                    await WriteLookupTypeEvidenceAsync(
                        evidence, observer, "G2-026", transition, durable, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-026-lookup-removed");
                    await ui.CloseWindowAsync(window, process.ReadinessTimeout, token);
                    referenceWindow = null;
                    return Observation(
                        $"id={fixture.ChangedLookupType.Id}; command={transition.CommandId}; "
                        + $"terminal={transition.TerminalEventName}; durableRows={durable.Length}; "
                        + "isolated lookup partition is absent from durable and visible state.",
                        ["network/g2-lookup-type-command-events.json", "queries/G2-026.json", .. artifacts]);
                });
            }

            if (!securitiesSlice && !yieldCurveSlice && !economicCalendarSlice && !lookupSlice)
            {
            await Step("G2-027", "Resolve the designated reusable fund fixture",
                "The named non-production fund is selected through the real UI; when absent, explicit retention approval permits one public UI/domain creation whose source, terminal event, durable row, and visible selector agree.",
                async token =>
                {
                    RequirePassed(
                        recorder,
                        fundSlice ? "G2-007" : "G2-026",
                        fundSlice
                            ? "The safety/startup prerequisites must complete before resolving the retained fund fixture."
                            : "Lookup maintenance must complete before fund transaction verification.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = fundFixture ?? throw new InvalidOperationException("G2 fund fixture is unavailable.");
                    var snapshot = baseline ?? throw new InvalidOperationException("G2 baseline is unavailable.");

                    G2FundCreationTransitionEvidence? creation = null;
                    designatedFund = snapshot.DesignatedFund;
                    if (designatedFund is null)
                    {
                        if (!configuration.RetainFundFixture)
                            throw new G0DependencyException(
                                $"Designated fund '{fixture.FundName}' does not exist. Set "
                                + "IFM_G2_RETAIN_FUND_FIXTURE=1 to approve one retained non-production fixture creation.");
                        ui.InvokeToolbarAction("Trade");
                        var tradeWindow = await ui.WaitForWindowAsync(
                            "Trade Orders", process.ReadinessTimeout, token);
                        creation = await ExecuteFundCreationMutationAsync(
                            observer,
                            operationToken => ui.CreateFundAsync(
                                tradeWindow, fixture, process.ReadinessTimeout, operationToken),
                            process.ReadinessTimeout,
                            token);
                        designatedFund = await WaitForFundAsync(
                            querySession,
                            fixture.FundName,
                            process.ReadinessTimeout,
                            token);
                        await ui.CloseWindowAsync(tradeWindow, process.ReadinessTimeout, token);
                    }

                    if (designatedFund.IsProduction)
                        throw new InvalidOperationException(
                            $"Designated fund {designatedFund.FundId}:{designatedFund.Name} is marked as production.");
                    if (!string.Equals(designatedFund.Name, fixture.FundName, StringComparison.Ordinal))
                        throw new InvalidOperationException("The resolved fund name does not match the designated fixture.");
                    fundStartingBalance = await WaitForFundBalanceAsync(
                        querySession,
                        designatedFund.FundId,
                        expectedBalance: null,
                        process.ReadinessTimeout,
                        token);
                    designatedFund = designatedFund with { Balance = fundStartingBalance.Value };

                    ui.InvokeToolbarAction("Funds");
                    fundWindow = await ui.WaitForWindowAsync(
                        "Fund Transactions Editor", process.ReadinessTimeout, token);
                    var visible = await ui.SelectFundAsync(
                        fundWindow, fixture.FundName, process.ReadinessTimeout, token);
                    if (ParseCurrency(visible.Balance) != fundStartingBalance.Value)
                        throw new InvalidOperationException(
                            $"Fund balance mismatch: durable={fundStartingBalance.Value}; UI='{visible.Balance}'.");
                    await evidence.WriteTextAsync(
                        Path.Combine("queries", "G2-027.json"),
                        JsonSerializer.Serialize(new
                        {
                            FixtureRetained = snapshot.DesignatedFund is not null,
                            Creation = creation,
                            Durable = designatedFund,
                            DurableBalance = fundStartingBalance,
                            Visible = visible
                        }, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    await WriteFundCommandEvidenceAsync(evidence, observer, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-027-fund-fixture");
                    return Observation(
                        $"fund={designatedFund.FundId}:{designatedFund.Name}; "
                        + $"created={(creation is not null)}; production={designatedFund.IsProduction}; "
                        + $"balance={fundStartingBalance.Value}; uiRows={visible.Rows.Count}.",
                        ["network/g2-fund-command-events.json", "queries/G2-027.json", .. artifacts]);
                });

            await Step("G2-028", "Create a reversible fund cash transaction",
                "A run-referenced cash deposit completes through the public UI/domain command, changes the queried and displayed balance by the configured amount, and appears exactly once in immutable transaction history.",
                async token =>
                {
                    RequirePassed(recorder, "G2-027", "The reusable fund fixture must be selected before its transaction.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = fundFixture ?? throw new InvalidOperationException("G2 fund fixture is unavailable.");
                    var fund = designatedFund ?? throw new InvalidOperationException("G2 designated fund is unavailable.");
                    var window = fundWindow ?? throw new InvalidOperationException("Fund Transactions Editor is unavailable.");
                    var originalBalance = fundStartingBalance
                        ?? throw new InvalidOperationException("G2 starting fund balance is unavailable.");

                    var transition = await ExecuteFundTransactionMutationAsync(
                        observer,
                        operationToken => ui.CreateCashTransactionAsync(
                            window,
                            fixture.FundName,
                            FundTransactionType.CashDeposit,
                            fixture.TransactionAmount,
                            fixture.DepositDescription,
                            process.ReadinessTimeout,
                            operationToken),
                        process.ReadinessTimeout,
                        token);
                    var expectedBalance = originalBalance + fixture.TransactionAmount;
                    var durableBalance = await WaitForFundBalanceAsync(
                        querySession, fund.FundId, expectedBalance, process.ReadinessTimeout, token);
                    var durableTransactions = await WaitForFundTransactionsAsync(
                        querySession,
                        fund.FundId,
                        baseline!.ValueDate,
                        [fixture.DepositDescription],
                        process.ReadinessTimeout,
                        token);
                    var deposit = durableTransactions.Single(transaction =>
                        string.Equals(transaction.Description, fixture.DepositDescription, StringComparison.Ordinal));
                    ValidateCashTransaction(
                        deposit, FundTransactionType.CashDeposit, fixture.TransactionAmount, expectedBalance);
                    if (ParseCurrency(transition.UiState.Balance) != expectedBalance)
                        throw new InvalidOperationException(
                            $"Deposit balance mismatch: expected={expectedBalance}; UI='{transition.UiState.Balance}'.");
                    await WriteFundTransactionEvidenceAsync(
                        evidence, observer, "G2-028", transition, durableBalance, durableTransactions, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-028-fund-deposit");
                    return Observation(
                        $"fund={fund.FundId}; command={transition.CommandId}; terminal={transition.TerminalEventName}; "
                        + $"amount={fixture.TransactionAmount}; balance={durableBalance}; transactionId={deposit.TransactionId}.",
                        ["network/g2-fund-command-events.json", "queries/G2-028.json", .. artifacts]);
                });

            await Step("G2-029", "Compensate the fund cash transaction",
                "An equal run-referenced cash withdrawal completes through the public UI/domain command, restores the exact baseline balance, and leaves both correlated append-only transaction rows visible and durable.",
                async token =>
                {
                    RequirePassed(recorder, "G2-028", "The run-owned cash deposit must complete before compensation.");
                    var ui = automation ?? throw new InvalidOperationException("G2 UI automation is unavailable.");
                    var observer = commandObserver ?? throw new InvalidOperationException("G2 command observer is unavailable.");
                    var querySession = queries ?? throw new InvalidOperationException("G2 typed queries are unavailable.");
                    var fixture = fundFixture ?? throw new InvalidOperationException("G2 fund fixture is unavailable.");
                    var fund = designatedFund ?? throw new InvalidOperationException("G2 designated fund is unavailable.");
                    var window = fundWindow ?? throw new InvalidOperationException("Fund Transactions Editor is unavailable.");
                    var originalBalance = fundStartingBalance
                        ?? throw new InvalidOperationException("G2 starting fund balance is unavailable.");

                    var transition = await ExecuteFundTransactionMutationAsync(
                        observer,
                        operationToken => ui.CreateCashTransactionAsync(
                            window,
                            fixture.FundName,
                            FundTransactionType.CashWithdrawal,
                            fixture.TransactionAmount,
                            fixture.WithdrawalDescription,
                            process.ReadinessTimeout,
                            operationToken),
                        process.ReadinessTimeout,
                        token);
                    var durableBalance = await WaitForFundBalanceAsync(
                        querySession, fund.FundId, originalBalance, process.ReadinessTimeout, token);
                    var durableTransactions = await WaitForFundTransactionsAsync(
                        querySession,
                        fund.FundId,
                        baseline!.ValueDate,
                        [fixture.DepositDescription, fixture.WithdrawalDescription],
                        process.ReadinessTimeout,
                        token);
                    var withdrawal = durableTransactions.Single(transaction =>
                        string.Equals(transaction.Description, fixture.WithdrawalDescription, StringComparison.Ordinal));
                    ValidateCashTransaction(
                        withdrawal, FundTransactionType.CashWithdrawal, fixture.TransactionAmount, originalBalance);
                    var visible = await ui.WaitForFundTransactionStateAsync(
                        window,
                        fixture.FundName,
                        [fixture.DepositDescription, fixture.WithdrawalDescription],
                        process.ReadinessTimeout,
                        token);
                    if (ParseCurrency(visible.Balance) != originalBalance)
                        throw new InvalidOperationException(
                            $"Compensated balance mismatch: expected={originalBalance}; UI='{visible.Balance}'.");
                    await WriteFundTransactionEvidenceAsync(
                        evidence, observer, "G2-029", transition, durableBalance, durableTransactions, token);
                    var artifacts = CaptureAcceptedEvidence(ui, evidence, "G2-029-fund-compensated");
                    await ui.CloseWindowAsync(window, process.ReadinessTimeout, token);
                    fundWindow = null;
                    return Observation(
                        $"fund={fund.FundId}; command={transition.CommandId}; terminal={transition.TerminalEventName}; "
                        + $"restoredBalance={durableBalance}; runTransactions=2; appendOnlyHistory=true.",
                        ["network/g2-fund-command-events.json", "queries/G2-029.json", .. artifacts]);
                });
            }
        }
        finally
        {
            if (automation is not null)
            {
                try { automation.CloseAllSecondaryWindows(); }
                catch (Exception exception) { cleanupFailures.Add("Secondary-window cleanup failed: " + exception.Message); }
            }

            if (queries is not null
                && commandObserver is not null
                && fundFixture is not null
                && designatedFund is not null
                && fundStartingBalance is not null
                && baseline is not null
                && api is not null
                && !api.Process.HasExited)
            {
                var cleanup = await CleanupFundTransactionFixtureAsync(
                    queries,
                    commandObserver,
                    fundFixture,
                    designatedFund,
                    baseline.ValueDate,
                    fundStartingBalance.Value,
                    process.ReadinessTimeout);
                await evidence.WriteTextAsync(
                    Path.Combine("processes", "g2-fund-transaction-cleanup.json"),
                    JsonSerializer.Serialize(cleanup, new JsonSerializerOptions { WriteIndented = true }));
                if (!cleanup.Succeeded)
                    cleanupFailures.Add("Fund transaction compensation/reconciliation failed: " + cleanup.Error);
            }

            if (queries is not null
                && commandObserver is not null
                && lookupTypeFixture is not null
                && baseline is not null
                && api is not null
                && !api.Process.HasExited)
            {
                var cleanup = await CleanupLookupTypeFixtureAsync(
                    queries,
                    commandObserver,
                    lookupTypeFixture,
                    baseline,
                    process.ReadinessTimeout);
                await evidence.WriteTextAsync(
                    Path.Combine("processes", "g2-lookup-type-cleanup.json"),
                    JsonSerializer.Serialize(cleanup, new JsonSerializerOptions { WriteIndented = true }));
                if (!cleanup.Succeeded)
                    cleanupFailures.Add("Lookup-type cleanup/baseline restoration failed: " + cleanup.Error);
            }

            if (queries is not null
                && commandObserver is not null
                && economicCalendarFixture is not null
                && baseline is not null
                && api is not null
                && !api.Process.HasExited)
            {
                var cleanup = await CleanupEconomicCalendarFixtureAsync(
                    queries,
                    commandObserver,
                    economicCalendarFixture,
                    baseline,
                    process.ReadinessTimeout);
                await evidence.WriteTextAsync(
                    Path.Combine("processes", "g2-economic-calendar-cleanup.json"),
                    JsonSerializer.Serialize(cleanup, new JsonSerializerOptions { WriteIndented = true }));
                if (!cleanup.Succeeded)
                    cleanupFailures.Add("Economic-calendar cleanup/baseline restoration failed: " + cleanup.Error);
            }

            if (queries is not null
                && commandObserver is not null
                && yieldCurveFixture is not null
                && baseline is not null
                && api is not null
                && !api.Process.HasExited)
            {
                var cleanup = await CleanupYieldCurveFixtureAsync(
                    queries,
                    commandObserver,
                    yieldCurveFixture,
                    baseline,
                    process.ReadinessTimeout);
                await evidence.WriteTextAsync(
                    Path.Combine("processes", "g2-yield-curve-cleanup.json"),
                    JsonSerializer.Serialize(cleanup, new JsonSerializerOptions { WriteIndented = true }));
                if (!cleanup.Succeeded)
                    cleanupFailures.Add("Yield-curve cleanup/baseline restoration failed: " + cleanup.Error);
            }

            if (queries is not null
                && commandObserver is not null
                && securitiesFixture is not null
                && api is not null
                && !api.Process.HasExited)
            {
                var cleanup = await CleanupSecuritiesFixtureAsync(
                    queries,
                    commandObserver,
                    securitiesFixture,
                    process.ReadinessTimeout);
                await evidence.WriteTextAsync(
                    Path.Combine("processes", "g2-securities-cleanup.json"),
                    JsonSerializer.Serialize(cleanup, new JsonSerializerOptions { WriteIndented = true }));
                if (!cleanup.Succeeded)
                    cleanupFailures.Add("Run-owned securities cleanup failed: " + cleanup.Error);
            }

            if (automation is not null && desktop is not null && !desktop.Process.HasExited)
            {
                try
                {
                    automation.RequestMainWindowClose();
                    if (!await desktop.WaitForExitAsync(process.ShutdownTimeout, CancellationToken.None))
                        cleanupFailures.Add($"Desktop did not exit normally within {process.ShutdownTimeout}.");
                    if (desktop.ForcedTermination)
                        cleanupFailures.Add("Desktop required forced termination.");
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add("Desktop normal-close cleanup failed: " + exception.Message);
                }
            }
            automation?.Dispose();

            if (commandObserver is not null)
            {
                try
                {
                    await commandObserver.WriteEvidenceAsync(evidence, CancellationToken.None);
                    await commandObserver.DisposeAsync();
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add("G2 command observer cleanup failed: " + exception.Message);
                }
            }
            if (queries is not null)
            {
                try { await queries.DisposeAsync(); }
                catch (Exception exception) { cleanupFailures.Add("G2 query cleanup failed: " + exception.Message); }
            }
            if (startupObserver is not null)
            {
                try
                {
                    await startupObserver.WriteEvidenceAsync(evidence, CancellationToken.None);
                    await startupObserver.DisposeAsync();
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add("Startup observer cleanup failed: " + exception.Message);
                }
            }
            if (api is not null && !api.Process.HasExited)
            {
                try
                {
                    if (!await api.TerminateOwnedTreeAsync(TimeSpan.FromSeconds(10), CancellationToken.None))
                        cleanupFailures.Add("Harness-owned API process tree did not terminate.");
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add("API cleanup failed: " + exception.Message);
                }
            }

            if (desktop is not null)
                await evidence.WriteTextAsync(Path.Combine("processes", "desktop-final.json"), desktop.Describe());
            if (api is not null)
                await evidence.WriteTextAsync(Path.Combine("processes", "api-final.json"), api.Describe());

            if (desktop is not null)
            {
                try { await desktop.DisposeAsync(); }
                catch (Exception exception) { cleanupFailures.Add("Desktop process disposal failed: " + exception.Message); }
            }
            if (api is not null)
            {
                try { await api.DisposeAsync(); }
                catch (Exception exception) { cleanupFailures.Add("API process disposal failed: " + exception.Message); }
            }

            run.CleanupSucceeded = cleanupFailures.Count == 0;
            run.CompletedUtc = DateTimeOffset.UtcNow;
            await evidence.WriteTextAsync(
                Path.Combine("processes", "partial-slice-cleanup.json"),
                JsonSerializer.Serialize(new
                {
                    Scope = securitiesSlice
                        ? "G2-001-007 plus G2-010-015 harness cleanup; this is not G2-037 or G2-038 acceptance"
                        : yieldCurveSlice
                            ? "G2-001-007 plus G2-016-019 harness cleanup and imported-date restoration; this is not G2-037 or G2-038 acceptance"
                        : economicCalendarSlice
                            ? "G2-001-007 plus G2-020-023 harness cleanup and imported-date restoration; this is not G2-037 or G2-038 acceptance"
                            : lookupSlice
                                ? "G2-001-007 plus G2-024-026 harness cleanup; this is not G2-037 or G2-038 acceptance"
                                : fundSlice
                                    ? "G2-001-007 plus G2-027-029 fund reconciliation; this is not G2-037 or G2-038 acceptance"
                                    : "G2-001-029 harness cleanup, imported-date restoration, and fund reconciliation; this is not G2-037 or G2-038 acceptance",
                    Succeeded = run.CleanupSucceeded,
                    Failures = cleanupFailures
                }, new JsonSerializerOptions { WriteIndented = true }));
            await evidence.WriteResultAsync(run, CancellationToken.None);
        }

        run.Passed.Should().BeTrue(
            $"{run.Gate} evidence was written to {evidence.RunDirectory}; "
            + string.Join("; ", run.Steps
                .Where(step => step.Status != G0StepStatus.Passed)
                .Select(step => $"{step.Id}={step.Status}: {step.Actual}"))
            + (cleanupFailures.Count == 0 ? string.Empty : "; cleanup=" + string.Join(" | ", cleanupFailures)));

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
                catch { /* Failure evidence must not hide the original step. */ }
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

    static async Task<G2MarketDataFeedTransitionEvidence> ToggleMarketDataFeedAsync(
        G1UiAutomationSession automation,
        G2CommandEventObserver observer,
        string sourceEventName,
        bool expectedActiveState,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var invokedUtc = DateTimeOffset.UtcNow;
        automation.InvokeMarketDataFeedAction();
        var sourceEvents = await observer.WaitForAsync(
            rows => rows.Any(row => row.ObservedUtc >= invokedUtc
                                    && row.Family == "MarketDataFeed"
                                    && row.EventName == sourceEventName
                                    && row.Success is null),
            timeout,
            cancellationToken);
        var source = sourceEvents.Last(row => row.ObservedUtc >= invokedUtc
                                              && row.Family == "MarketDataFeed"
                                              && row.EventName == sourceEventName
                                              && row.Success is null);
        var terminalEvents = await observer.WaitForAsync(
            rows => rows.Any(row => row.CommandId == source.CommandId && row.Success.HasValue),
            timeout,
            cancellationToken);
        var terminal = terminalEvents.Last(row => row.CommandId == source.CommandId && row.Success.HasValue);
        if (terminal.Success != true)
            throw new InvalidOperationException(
                $"{sourceEventName} command {source.CommandId} failed: {terminal.ErrorMessage}");
        var uiState = await automation.WaitForMarketDataFeedStateAsync(
            expectedActiveState,
            timeout,
            cancellationToken);
        return new G2MarketDataFeedTransitionEvidence(
            source.CommandId,
            source.EventName,
            terminal.EventName,
            source.ObservedUtc,
            terminal.ObservedUtc,
            uiState);
    }

    static async Task<G2SecuritiesTransitionEvidence> ExecuteSecuritiesMutationAsync(
        G2CommandEventObserver observer,
        string family,
        string sourceEventName,
        Func<CancellationToken, Task<G2SecuritiesEditorUiState>> invokeUi,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var invokedUtc = DateTimeOffset.UtcNow;
        var uiTask = invokeUi(operationSource.Token);
        var sourceTask = observer.WaitForAsync(
            rows => rows.Any(row => row.ObservedUtc >= invokedUtc
                                    && row.Family == family
                                    && row.EventName == sourceEventName
                                    && row.Success is null),
            timeout,
            operationSource.Token);
        if (await Task.WhenAny(sourceTask, uiTask).ConfigureAwait(false) == uiTask)
            await uiTask.ConfigureAwait(false);
        var sourceEvents = await sourceTask.ConfigureAwait(false);
        var source = sourceEvents.Last(row => row.ObservedUtc >= invokedUtc
                                              && row.Family == family
                                              && row.EventName == sourceEventName
                                              && row.Success is null);
        var terminalEvents = await observer.WaitForAsync(
            rows => rows.Any(row => row.CommandId == source.CommandId && row.Success.HasValue),
            timeout,
            cancellationToken);
        var terminal = terminalEvents.Last(row => row.CommandId == source.CommandId && row.Success.HasValue);
        if (terminal.Success != true)
        {
            operationSource.Cancel();
            try { await uiTask.ConfigureAwait(false); }
            catch { /* Preserve the correlated backend failure below. */ }
            throw new InvalidOperationException(
                $"{sourceEventName} command {source.CommandId} failed: {terminal.ErrorMessage}");
        }

        var uiState = await uiTask.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        return new G2SecuritiesTransitionEvidence(
            source.CommandId,
            family,
            source.EventName,
            terminal.EventName,
            source.ObservedUtc,
            terminal.ObservedUtc,
            uiState);
    }

    static async Task<G2YieldCurveTransitionEvidence> ExecuteYieldCurveMutationAsync(
        G2CommandEventObserver observer,
        string sourceEventName,
        Func<CancellationToken, Task<G2YieldCurveEditorUiState>> invokeUi,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var invokedUtc = DateTimeOffset.UtcNow;
        var uiTask = invokeUi(operationSource.Token);
        var sourceTask = observer.WaitForAsync(
            rows => rows.Any(row => row.ObservedUtc >= invokedUtc
                                    && row.Family == "YieldCurve"
                                    && row.EventName == sourceEventName
                                    && row.Success is null),
            timeout,
            operationSource.Token);
        if (await Task.WhenAny(sourceTask, uiTask).ConfigureAwait(false) == uiTask)
            await uiTask.ConfigureAwait(false);
        var sourceEvents = await sourceTask.ConfigureAwait(false);
        var source = sourceEvents.Last(row => row.ObservedUtc >= invokedUtc
                                              && row.Family == "YieldCurve"
                                              && row.EventName == sourceEventName
                                              && row.Success is null);
        var terminalEvents = await observer.WaitForAsync(
            rows => rows.Any(row => row.CommandId == source.CommandId && row.Success.HasValue),
            timeout,
            cancellationToken);
        var terminal = terminalEvents.Last(row => row.CommandId == source.CommandId && row.Success.HasValue);
        if (terminal.Success != true)
        {
            operationSource.Cancel();
            try { await uiTask.ConfigureAwait(false); }
            catch { /* Preserve the correlated backend failure below. */ }
            throw new InvalidOperationException(
                $"{sourceEventName} command {source.CommandId} failed: {terminal.ErrorMessage}");
        }

        var uiState = await uiTask.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        return new G2YieldCurveTransitionEvidence(
            source.CommandId,
            source.EventName,
            terminal.EventName,
            source.ObservedUtc,
            terminal.ObservedUtc,
            terminal.ImportDate,
            terminal.ImportedYieldCurveRates,
            uiState);
    }

    static async Task<G2EconomicCalendarTransitionEvidence> ExecuteEconomicCalendarMutationAsync(
        G2CommandEventObserver observer,
        string sourceEventName,
        Func<CancellationToken, Task<G2EconomicCalendarEditorUiState>> invokeUi,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var invokedUtc = DateTimeOffset.UtcNow;
        var uiTask = invokeUi(operationSource.Token);
        var sourceTask = observer.WaitForAsync(
            rows => rows.Any(row => row.ObservedUtc >= invokedUtc
                                    && row.Family == "EconomicCalendar"
                                    && row.EventName == sourceEventName
                                    && row.Success is null),
            timeout,
            operationSource.Token);
        if (await Task.WhenAny(sourceTask, uiTask).ConfigureAwait(false) == uiTask)
            await uiTask.ConfigureAwait(false);
        var sourceEvents = await sourceTask.ConfigureAwait(false);
        var source = sourceEvents.Last(row => row.ObservedUtc >= invokedUtc
                                              && row.Family == "EconomicCalendar"
                                              && row.EventName == sourceEventName
                                              && row.Success is null);
        var terminalEvents = await observer.WaitForAsync(
            rows => rows.Any(row => row.CommandId == source.CommandId && row.Success.HasValue),
            timeout,
            cancellationToken);
        var terminal = terminalEvents.Last(row => row.CommandId == source.CommandId && row.Success.HasValue);
        if (terminal.Success != true)
        {
            operationSource.Cancel();
            try { await uiTask.ConfigureAwait(false); }
            catch { /* Preserve the correlated backend failure below. */ }
            throw new InvalidOperationException(
                $"{sourceEventName} command {source.CommandId} failed: {terminal.ErrorMessage}");
        }

        var uiState = await uiTask.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        return new G2EconomicCalendarTransitionEvidence(
            source.CommandId,
            source.EventName,
            terminal.EventName,
            source.ObservedUtc,
            terminal.ObservedUtc,
            terminal.ImportDate,
            terminal.ImportCountryCodes,
            terminal.ImportedEconomicCalendars,
            uiState);
    }

    static async Task<G2LookupTypeTransitionEvidence> ExecuteLookupTypeMutationAsync(
        G2CommandEventObserver observer,
        string sourceEventName,
        Func<CancellationToken, Task<G2LookupTypeEditorUiState>> invokeUi,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var invokedUtc = DateTimeOffset.UtcNow;
        var uiTask = invokeUi(operationSource.Token);
        var sourceTask = observer.WaitForAsync(
            rows => rows.Any(row => row.ObservedUtc >= invokedUtc
                                    && row.Family == "LookupType"
                                    && row.EventName == sourceEventName
                                    && row.Success is null),
            timeout,
            operationSource.Token);
        if (await Task.WhenAny(sourceTask, uiTask).ConfigureAwait(false) == uiTask)
            await uiTask.ConfigureAwait(false);
        var sourceEvents = await sourceTask.ConfigureAwait(false);
        var source = sourceEvents.Last(row => row.ObservedUtc >= invokedUtc
                                              && row.Family == "LookupType"
                                              && row.EventName == sourceEventName
                                              && row.Success is null);
        var terminalEvents = await observer.WaitForAsync(
            rows => rows.Any(row => row.CommandId == source.CommandId && row.Success.HasValue),
            timeout,
            cancellationToken);
        var terminal = terminalEvents.Last(row => row.CommandId == source.CommandId && row.Success.HasValue);
        if (terminal.Success != true)
        {
            operationSource.Cancel();
            try { await uiTask.ConfigureAwait(false); }
            catch { /* Preserve the correlated backend failure below. */ }
            throw new InvalidOperationException(
                $"{sourceEventName} command {source.CommandId} failed: {terminal.ErrorMessage}");
        }

        var uiState = await uiTask.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        return new G2LookupTypeTransitionEvidence(
            source.CommandId,
            source.EventName,
            terminal.EventName,
            source.ObservedUtc,
            terminal.ObservedUtc,
            uiState);
    }

    static async Task<G2FundCreationTransitionEvidence> ExecuteFundCreationMutationAsync(
        G2CommandEventObserver observer,
        Func<CancellationToken, Task<G2CreatedFundUiState>> invokeUi,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var invokedUtc = DateTimeOffset.UtcNow;
        var uiTask = invokeUi(operationSource.Token);
        var sourceTask = observer.WaitForAsync(
            rows => rows.Any(row => row.ObservedUtc >= invokedUtc
                                    && row.Family == "Fund"
                                    && row.EventName == nameof(FundCreatedEvent)
                                    && row.Success is null),
            timeout,
            operationSource.Token);
        if (await Task.WhenAny(sourceTask, uiTask).ConfigureAwait(false) == uiTask)
            await uiTask.ConfigureAwait(false);
        var sourceEvents = await sourceTask.ConfigureAwait(false);
        var source = sourceEvents.Last(row => row.ObservedUtc >= invokedUtc
                                              && row.Family == "Fund"
                                              && row.EventName == nameof(FundCreatedEvent)
                                              && row.Success is null);
        var terminalEvents = await observer.WaitForAsync(
            rows => rows.Any(row => row.CommandId == source.CommandId && row.Success.HasValue),
            timeout,
            cancellationToken);
        var terminal = terminalEvents.Last(row => row.CommandId == source.CommandId && row.Success.HasValue);
        if (terminal.Success != true)
        {
            operationSource.Cancel();
            try { await uiTask.ConfigureAwait(false); }
            catch { /* Preserve the correlated backend failure below. */ }
            throw new InvalidOperationException(
                $"{nameof(FundCreatedEvent)} command {source.CommandId} failed: {terminal.ErrorMessage}");
        }
        var uiState = await uiTask.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (source.Fund is null
            || terminal.Fund is null
            || source.Fund.FundId != uiState.FundId
            || terminal.Fund.FundId != uiState.FundId)
            throw new InvalidOperationException("Fund creation source, terminal, and UI identities do not agree.");
        return new G2FundCreationTransitionEvidence(
            source.CommandId,
            source.EventName,
            terminal.EventName,
            source.ObservedUtc,
            terminal.ObservedUtc,
            source.Fund,
            terminal.Fund,
            uiState);
    }

    static async Task<G2FundTransactionTransitionEvidence> ExecuteFundTransactionMutationAsync(
        G2CommandEventObserver observer,
        Func<CancellationToken, Task<G2FundTransactionUiState>> invokeUi,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var operationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var invokedUtc = DateTimeOffset.UtcNow;
        var uiTask = invokeUi(operationSource.Token);
        var sourceTask = observer.WaitForAsync(
            rows => rows.Any(row => row.ObservedUtc >= invokedUtc
                                    && row.Family == "FundTransaction"
                                    && row.EventName == nameof(FundTransactionEvent)
                                    && row.Success is null),
            timeout,
            operationSource.Token);
        if (await Task.WhenAny(sourceTask, uiTask).ConfigureAwait(false) == uiTask)
            await uiTask.ConfigureAwait(false);
        var sourceEvents = await sourceTask.ConfigureAwait(false);
        var source = sourceEvents.Last(row => row.ObservedUtc >= invokedUtc
                                              && row.Family == "FundTransaction"
                                              && row.EventName == nameof(FundTransactionEvent)
                                              && row.Success is null);
        var terminalEvents = await observer.WaitForAsync(
            rows => rows.Any(row => row.CommandId == source.CommandId && row.Success.HasValue),
            timeout,
            cancellationToken);
        var terminal = terminalEvents.Last(row => row.CommandId == source.CommandId && row.Success.HasValue);
        if (terminal.Success != true)
        {
            operationSource.Cancel();
            try { await uiTask.ConfigureAwait(false); }
            catch { /* Preserve the correlated backend failure below. */ }
            throw new InvalidOperationException(
                $"{nameof(FundTransactionEvent)} command {source.CommandId} failed: {terminal.ErrorMessage}");
        }
        var uiState = await uiTask.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        if (source.FundTransaction is null || terminal.FundTransaction is null)
            throw new InvalidOperationException("Fund transaction source or terminal payload is missing.");
        if (source.FundTransaction.FundId != terminal.FundTransaction.FundId
            || source.FundTransaction.TransactionType != terminal.FundTransaction.TransactionType
            || source.FundTransaction.Amount != terminal.FundTransaction.Amount
            || !string.Equals(source.FundTransaction.Description, terminal.FundTransaction.Description, StringComparison.Ordinal))
            throw new InvalidOperationException("Fund transaction source and terminal payloads do not agree.");
        return new G2FundTransactionTransitionEvidence(
            source.CommandId,
            source.EventName,
            terminal.EventName,
            source.ObservedUtc,
            terminal.ObservedUtc,
            source.FundTransaction,
            terminal.FundTransaction,
            uiState);
    }

    static async Task<FundReadModel> WaitForFundAsync(
        G0QuerySession queries,
        string fundName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var result = await queries.Fund.GetFundsAsync().ConfigureAwait(false);
            if (result.Success && result.Value is { } funds)
            {
                var fund = funds.SingleOrDefault(item => string.Equals(item.Name, fundName, StringComparison.Ordinal));
                if (fund is not null)
                    return fund;
            }
            await DelayForProjectionAsync(timeoutSource.Token, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException($"Fund '{fundName}' did not become durable within {timeout}.");
    }

    static async Task<decimal> WaitForFundBalanceAsync(
        G0QuerySession queries,
        int fundId,
        decimal? expectedBalance,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var result = await queries.Fund.GetFundBalanceAsync(fundId).ConfigureAwait(false);
            if (result.Success && result.Value is { } balance
                && (expectedBalance is null || balance.Value == expectedBalance.Value))
                return balance.Value;
            await DelayForProjectionAsync(timeoutSource.Token, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException(
            expectedBalance is null
                ? $"Fund {fundId} balance did not become queryable within {timeout}."
                : $"Fund {fundId} balance did not become {expectedBalance.Value} within {timeout}.");
    }

    static async Task<FundTransactionReadModel[]> WaitForFundTransactionsAsync(
        G0QuerySession queries,
        int fundId,
        DateOnly valueDate,
        string[] requiredDescriptions,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var result = await queries.Fund.GetFundTransactionsAsync(fundId, valueDate, valueDate)
                .ConfigureAwait(false);
            if (result.Success && result.Value is { } transactions
                && requiredDescriptions.All(description => transactions.Count(transaction =>
                    string.Equals(transaction.Description, description, StringComparison.Ordinal)) == 1))
                return transactions;
            await DelayForProjectionAsync(timeoutSource.Token, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException(
            $"Fund {fundId} did not expose exactly one transaction for each required run reference within {timeout}.");
    }

    static void ValidateCashTransaction(
        FundTransactionReadModel transaction,
        FundTransactionType transactionType,
        decimal amount,
        decimal expectedBalance)
    {
        if (transaction.TransactionType != transactionType
            || transaction.Amount != amount
            || transaction.Balance != expectedBalance
            || transaction.TransactionId <= 0)
            throw new InvalidOperationException(
                $"Unexpected cash transaction: type={transaction.TransactionType}; amount={transaction.Amount}; "
                + $"balance={transaction.Balance}; id={transaction.TransactionId}.");
    }

    static decimal ParseCurrency(string value)
    {
        foreach (var culture in new[] { CultureInfo.CurrentCulture, CultureInfo.GetCultureInfo("en-CA"), CultureInfo.GetCultureInfo("en-US") })
        {
            if (decimal.TryParse(value, NumberStyles.Currency, culture, out var parsed))
                return parsed;
        }
        throw new InvalidOperationException($"Could not parse displayed currency value '{value}'.");
    }

    static async Task<FuturesContractV2ReadModel?> WaitForFuturesContractAsync(
        G0QuerySession queries,
        string contractId,
        string? expectedDescription,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var contracts = RequireQueryValue(
                await queries.MarketData.GetFuturesContractsAsync().WaitAsync(timeoutSource.Token),
                "futures contracts");
            var contract = contracts.SingleOrDefault(row => string.Equals(
                row.ContractId,
                contractId,
                StringComparison.Ordinal));
            if ((!present && contract is null)
                || (present
                    && contract is not null
                    && (expectedDescription is null
                        || string.Equals(contract.Description, expectedDescription, StringComparison.Ordinal))))
                return contract;
            try { await Task.Delay(TimeSpan.FromMilliseconds(150), timeoutSource.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { break; }
        }
        throw new TimeoutException(
            $"Typed futures query did not show '{contractId}' as {(present ? "present with the expected state" : "absent")}.");
    }

    static async Task<FuturesOptionContractReadModel?> WaitForFuturesOptionContractAsync(
        G0QuerySession queries,
        G2SecuritiesFixture fixture,
        string? expectedDescription,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var contracts = RequireQueryValue(
                await queries.MarketData.GetFuturesOptionContractsAsync(fixture.Symbol)
                    .WaitAsync(timeoutSource.Token),
                $"{fixture.Symbol} futures option contracts");
            var contract = contracts.SingleOrDefault(row => string.Equals(
                row.ContractId,
                fixture.OptionContractId,
                StringComparison.Ordinal));
            if ((!present && contract is null)
                || (present
                    && contract is not null
                    && (expectedDescription is null
                        || string.Equals(contract.Description, expectedDescription, StringComparison.Ordinal))))
                return contract;
            try { await Task.Delay(TimeSpan.FromMilliseconds(150), timeoutSource.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { break; }
        }
        throw new TimeoutException(
            $"Typed option query did not show '{fixture.OptionContractId}' as {(present ? "present with the expected state" : "absent")}.");
    }

    static async Task<YieldCurveRateReadModel?> WaitForYieldCurveRateAsync(
        G0QuerySession queries,
        DateOnly valueDate,
        YieldCurveRateReadModel? expectedRate,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var expected = present
            ? new[] { expectedRate ?? throw new ArgumentNullException(nameof(expectedRate)) }
            : [];
        var rates = await WaitForYieldCurveRatesAsync(
            queries, valueDate, expected, timeout, cancellationToken);
        return rates.SingleOrDefault();
    }

    static async Task<YieldCurveRateReadModel[]> WaitForYieldCurveRatesAsync(
        G0QuerySession queries,
        DateOnly valueDate,
        IReadOnlyList<YieldCurveRateReadModel> expectedRates,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var rates = RequireQueryValue(
                await queries.MarketData.GetYieldCurveRatesAsync(valueDate, valueDate)
                    .WaitAsync(timeoutSource.Token),
                $"yield-curve rates for {valueDate:yyyy-MM-dd}");
            if (YieldCurveRatesEqual(rates, expectedRates))
                return rates;
            try { await Task.Delay(TimeSpan.FromMilliseconds(150), timeoutSource.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { break; }
        }
        throw new TimeoutException(
            $"Typed yield-curve query for '{valueDate:yyyy-MM-dd}' did not match the expected {expectedRates.Count} row(s).");
    }

    static bool YieldCurveRatesEqual(
        IReadOnlyCollection<YieldCurveRateReadModel> actual,
        IReadOnlyCollection<YieldCurveRateReadModel> expected)
        => actual.OrderBy(rate => rate.ValueDate)
            .SequenceEqual(expected.OrderBy(rate => rate.ValueDate));

    static async Task<EconomicCalendarReadModel[]> WaitForEconomicCalendarsAsync(
        G0QuerySession queries,
        DateOnly eventDate,
        string countryCode,
        IReadOnlyList<EconomicCalendarReadModel> expectedRows,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var queryDate = DateTime.SpecifyKind(eventDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        while (!timeoutSource.IsCancellationRequested)
        {
            var rows = RequireQueryValue(
                await queries.MarketData.GetEconomicCalendarsAsync(
                        queryDate,
                        EconomicCalendarViewType.Today,
                        countryCode)
                    .WaitAsync(timeoutSource.Token),
                $"economic calendars for {eventDate:yyyy-MM-dd}/{countryCode}");
            if (EconomicCalendarsEqual(rows, expectedRows))
                return rows;
            try { await Task.Delay(TimeSpan.FromMilliseconds(150), timeoutSource.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { break; }
        }
        throw new TimeoutException(
            $"Typed economic-calendar query for '{eventDate:yyyy-MM-dd}/{countryCode}' "
            + $"did not match the expected {expectedRows.Count} row(s).");
    }

    static EconomicCalendarReadModel[] MergeEconomicCalendars(
        IEnumerable<EconomicCalendarReadModel> baseline,
        IEnumerable<EconomicCalendarReadModel> imported)
    {
        var rows = baseline.ToDictionary(EconomicCalendarIdentity, StringComparer.Ordinal);
        foreach (var row in imported)
            rows[EconomicCalendarIdentity(row)] = row;
        return rows.Values
            .OrderBy(row => row.EventDate)
            .ThenBy(row => row.EventName, StringComparer.Ordinal)
            .ToArray();
    }

    static bool EconomicCalendarsEqual(
        IEnumerable<EconomicCalendarReadModel> actual,
        IEnumerable<EconomicCalendarReadModel> expected)
    {
        var actualRows = actual.OrderBy(EconomicCalendarIdentity, StringComparer.Ordinal).ToArray();
        var expectedRows = expected.OrderBy(EconomicCalendarIdentity, StringComparer.Ordinal).ToArray();
        return actualRows.Length == expectedRows.Length
               && actualRows.Zip(expectedRows).All(pair => EconomicCalendarEquivalent(pair.First, pair.Second));
    }

    static bool EconomicCalendarEquivalent(
        EconomicCalendarReadModel left,
        EconomicCalendarReadModel right)
        => string.Equals(EconomicCalendarIdentity(left), EconomicCalendarIdentity(right), StringComparison.Ordinal)
           && string.Equals(left.Actual, right.Actual, StringComparison.Ordinal)
           && string.Equals(left.Forecast, right.Forecast, StringComparison.Ordinal)
           && string.Equals(left.Prior, right.Prior, StringComparison.Ordinal)
           && string.Equals(left.Impact, right.Impact, StringComparison.Ordinal)
           && string.Equals(left.Unit, right.Unit, StringComparison.Ordinal)
           && string.Equals(left.Change, right.Change, StringComparison.Ordinal)
           && string.Equals(left.ChangePercentage, right.ChangePercentage, StringComparison.Ordinal);

    static bool EconomicCalendarsEqualWithMetadata(
        IEnumerable<EconomicCalendarReadModel> actual,
        IEnumerable<EconomicCalendarReadModel> expected)
    {
        var actualRows = actual.OrderBy(EconomicCalendarIdentity, StringComparer.Ordinal).ToArray();
        var expectedRows = expected.OrderBy(EconomicCalendarIdentity, StringComparer.Ordinal).ToArray();
        return actualRows.Length == expectedRows.Length
               && actualRows.Zip(expectedRows).All(pair =>
                   EconomicCalendarEquivalent(pair.First, pair.Second)
                   && NormalizeTimestamp(pair.First.CreatedOn) == NormalizeTimestamp(pair.Second.CreatedOn)
                   && string.Equals(pair.First.CreatedBy, pair.Second.CreatedBy, StringComparison.Ordinal));
    }

    static async Task<LookupTypeReadModel[]> WaitForLookupTypesAsync(
        G0QuerySession queries,
        LookupTypeReadModel expected,
        bool present,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            var rows = RequireQueryValue(
                    await queries.Reference.GetLookupTypesAsync(expected.LookupTypeName)
                        .WaitAsync(timeoutSource.Token),
                    $"lookup types for {expected.LookupTypeName}")
                .OrderBy(row => row.OrderId)
                .ToArray();
            if ((!present && rows.Length == 0)
                || (present
                    && rows.Length == 1
                    && LookupTypeEquivalent(rows[0], expected)))
                return rows;
            try { await Task.Delay(TimeSpan.FromMilliseconds(150), timeoutSource.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { break; }
        }
        throw new TimeoutException(
            $"Typed lookup query for '{expected.LookupTypeName}' did not show its isolated value as "
            + $"{(present ? "present with every expected business field" : "absent")}.");
    }

    static bool LookupTypeEquivalent(LookupTypeReadModel left, LookupTypeReadModel right)
        => string.Equals(left.LookupTypeName, right.LookupTypeName, StringComparison.Ordinal)
           && string.Equals(left.ShortCode, right.ShortCode, StringComparison.Ordinal)
           && left.OrderId == right.OrderId
           && string.Equals(left.Description, right.Description, StringComparison.Ordinal);

    static string EconomicCalendarIdentity(EconomicCalendarReadModel row)
        => $"{NormalizeTimestamp(row.EventDate):O}|{row.CountryCode}|{row.EventName}";

    static DateTime NormalizeTimestamp(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        return new DateTime(
            utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond,
            DateTimeKind.Utc);
    }

    static T RequireQueryValue<T>(ServiceResult<T> result, string queryName)
        where T : class
    {
        if (!result.Success || result.Value is null)
            throw new InvalidOperationException(
                $"Typed {queryName} query failed: code={result.ErrorCode}; message={result.ErrorMessage}");
        return result.Value;
    }

    static async Task WriteSecuritiesEvidenceAsync(
        G0EvidenceWriter evidence,
        G2CommandEventObserver observer,
        string stepId,
        G2SecuritiesTransitionEvidence transition,
        object? durableState,
        CancellationToken cancellationToken)
    {
        await evidence.WriteTextAsync(
            Path.Combine("network", "g2-securities-command-events.json"),
            JsonSerializer.Serialize(
                observer.Events.Where(row => row.Family is "FuturesContract" or "FuturesOptionContract"),
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        await evidence.WriteTextAsync(
            Path.Combine("queries", stepId + ".json"),
            JsonSerializer.Serialize(new { Transition = transition, DurableState = durableState },
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    static async Task WriteYieldCurveEvidenceAsync(
        G0EvidenceWriter evidence,
        G2CommandEventObserver observer,
        string stepId,
        G2YieldCurveTransitionEvidence transition,
        object? durableState,
        CancellationToken cancellationToken)
    {
        await evidence.WriteTextAsync(
            Path.Combine("network", "g2-yield-curve-command-events.json"),
            JsonSerializer.Serialize(
                observer.Events.Where(row => row.Family == "YieldCurve"),
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        await evidence.WriteTextAsync(
            Path.Combine("queries", stepId + ".json"),
            JsonSerializer.Serialize(new { Transition = transition, DurableState = durableState },
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    static async Task WriteEconomicCalendarEvidenceAsync(
        G0EvidenceWriter evidence,
        G2CommandEventObserver observer,
        string stepId,
        G2EconomicCalendarTransitionEvidence transition,
        object? durableState,
        CancellationToken cancellationToken)
    {
        await evidence.WriteTextAsync(
            Path.Combine("network", "g2-economic-calendar-command-events.json"),
            JsonSerializer.Serialize(
                observer.Events.Where(row => row.Family == "EconomicCalendar"),
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        await evidence.WriteTextAsync(
            Path.Combine("queries", stepId + ".json"),
            JsonSerializer.Serialize(new { Transition = transition, DurableState = durableState },
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    static async Task WriteLookupTypeEvidenceAsync(
        G0EvidenceWriter evidence,
        G2CommandEventObserver observer,
        string stepId,
        G2LookupTypeTransitionEvidence transition,
        object? durableState,
        CancellationToken cancellationToken)
    {
        await evidence.WriteTextAsync(
            Path.Combine("network", "g2-lookup-type-command-events.json"),
            JsonSerializer.Serialize(
                observer.Events.Where(row => row.Family == "LookupType"),
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        await evidence.WriteTextAsync(
            Path.Combine("queries", stepId + ".json"),
            JsonSerializer.Serialize(new { Transition = transition, DurableState = durableState },
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    static Task WriteFundCommandEvidenceAsync(
        G0EvidenceWriter evidence,
        G2CommandEventObserver observer,
        CancellationToken cancellationToken)
        => evidence.WriteTextAsync(
            Path.Combine("network", "g2-fund-command-events.json"),
            JsonSerializer.Serialize(
                observer.Events.Where(row => row.Family is "Fund" or "FundTransaction"),
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

    static async Task WriteFundTransactionEvidenceAsync(
        G0EvidenceWriter evidence,
        G2CommandEventObserver observer,
        string stepId,
        G2FundTransactionTransitionEvidence transition,
        decimal durableBalance,
        FundTransactionReadModel[] durableTransactions,
        CancellationToken cancellationToken)
    {
        await WriteFundCommandEvidenceAsync(evidence, observer, cancellationToken);
        await evidence.WriteTextAsync(
            Path.Combine("queries", stepId + ".json"),
            JsonSerializer.Serialize(new
            {
                Transition = transition,
                DurableBalance = durableBalance,
                DurableTransactions = durableTransactions
            }, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    static async Task DelayForProjectionAsync(
        CancellationToken timeoutToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), timeoutToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The polling loop will observe its timeout on the next condition check.
        }
    }

    static async Task<G2FundCleanupEvidence> CleanupFundTransactionFixtureAsync(
        G0QuerySession queries,
        G2CommandEventObserver observer,
        G2FundFixture fixture,
        FundReadModel fund,
        DateOnly valueDate,
        decimal startingBalance,
        TimeSpan timeout)
    {
        List<string> actions = [];
        try
        {
            var transactions = RequireQueryValue(
                await queries.Fund.GetFundTransactionsAsync(fund.FundId, valueDate, valueDate)
                    .WaitAsync(timeout),
                "designated fund transactions during cleanup");
            var deposits = transactions.Where(transaction => string.Equals(
                    transaction.Description, fixture.DepositDescription, StringComparison.Ordinal))
                .ToArray();
            var withdrawals = transactions.Where(transaction => string.Equals(
                    transaction.Description, fixture.WithdrawalDescription, StringComparison.Ordinal))
                .ToArray();
            if (deposits.Length > 1 || withdrawals.Length > 1)
                throw new InvalidOperationException(
                    $"Run-owned fund history is not unique: deposits={deposits.Length}; withdrawals={withdrawals.Length}.");
            if (withdrawals.Length > deposits.Length)
                throw new InvalidOperationException("Run-owned withdrawal exists without its matching deposit.");

            if (deposits.Length == 1 && withdrawals.Length == 0)
            {
                var currentBalance = RequireQueryValue(
                    await queries.Fund.GetFundBalanceAsync(fund.FundId).WaitAsync(timeout),
                    "designated fund balance during cleanup").Value;
                var compensation = fixture.Transaction(
                    fund with { Balance = currentBalance },
                    valueDate,
                    FundTransactionType.CashWithdrawal,
                    fixture.WithdrawalDescription);
                var result = await queries.FundCommands.CreateFundTransactionAsync(compensation)
                    .WaitAsync(timeout);
                var commandId = RequireCommandId(result, "cleanup fund compensation");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                await WaitForFundTransactionsAsync(
                    queries,
                    fund.FundId,
                    valueDate,
                    [fixture.DepositDescription, fixture.WithdrawalDescription],
                    timeout,
                    CancellationToken.None);
                actions.Add($"Created compensating withdrawal with command {commandId}.");
            }

            var restoredBalance = await WaitForFundBalanceAsync(
                queries,
                fund.FundId,
                startingBalance,
                timeout,
                CancellationToken.None);
            actions.Add($"Verified restored balance {restoredBalance} for retained fund {fund.FundId}.");
            return new G2FundCleanupEvidence(true, actions, string.Empty);
        }
        catch (Exception exception)
        {
            return new G2FundCleanupEvidence(false, actions, exception.ToString());
        }
    }

    static async Task<G2SecuritiesCleanupEvidence> CleanupSecuritiesFixtureAsync(
        G0QuerySession queries,
        G2CommandEventObserver observer,
        G2SecuritiesFixture fixture,
        TimeSpan timeout)
    {
        List<string> actions = [];
        try
        {
            var options = RequireQueryValue(
                await queries.MarketData.GetFuturesOptionContractsAsync(fixture.Symbol)
                    .WaitAsync(timeout),
                $"{fixture.Symbol} futures option contracts during cleanup");
            var option = options.SingleOrDefault(row => string.Equals(
                row.ContractId,
                fixture.OptionContractId,
                StringComparison.Ordinal));
            if (option is not null)
            {
                var result = await queries.MarketDataCommands.RemoveFuturesOptionContractAsync(
                    fixture.OptionContractId,
                    true);
                var commandId = RequireCommandId(result, "cleanup option removal");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                await WaitForFuturesOptionContractAsync(
                    queries, fixture, null, present: false, timeout, CancellationToken.None);
                actions.Add($"Removed option {fixture.OptionContractId} with command {commandId}.");
            }

            var futuresContracts = RequireQueryValue(
                await queries.MarketData.GetFuturesContractsAsync().WaitAsync(timeout),
                "futures contracts during cleanup");
            var futures = futuresContracts.SingleOrDefault(row => string.Equals(
                row.ContractId,
                fixture.FuturesContractId,
                StringComparison.Ordinal));
            if (futures is not null)
            {
                var result = await queries.MarketDataCommands.RemoveFuturesContractAsync(
                    new FuturesContractId(fixture.FuturesContractId, fixture.Symbol, fixture.MaturityDate),
                    true);
                var commandId = RequireCommandId(result, "cleanup futures removal");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                await WaitForFuturesContractAsync(
                    queries, fixture.FuturesContractId, null, present: false, timeout, CancellationToken.None);
                actions.Add($"Removed futures contract {fixture.FuturesContractId} with command {commandId}.");
            }

            return new G2SecuritiesCleanupEvidence(true, actions, string.Empty);
        }
        catch (Exception exception)
        {
            return new G2SecuritiesCleanupEvidence(false, actions, exception.Message);
        }
    }

    static async Task<G2YieldCurveCleanupEvidence> CleanupYieldCurveFixtureAsync(
        G0QuerySession queries,
        G2CommandEventObserver observer,
        G2YieldCurveFixture fixture,
        G2BaselineSnapshot baseline,
        TimeSpan timeout)
    {
        List<string> actions = [];
        try
        {
            var manualRows = RequireQueryValue(
                await queries.MarketData.GetYieldCurveRatesAsync(fixture.ManualDate, fixture.ManualDate)
                    .WaitAsync(timeout),
                "manual yield-curve fixture during cleanup");
            foreach (var row in manualRows)
            {
                var result = await queries.MarketDataCommands.RemoveYieldCurveRateAsync(row.ValueDate, true);
                var commandId = RequireCommandId(result, "cleanup manual yield-curve removal");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                actions.Add($"Removed manual yield-curve row {row.ValueDate:yyyy-MM-dd} with command {commandId}.");
            }
            await WaitForYieldCurveRatesAsync(
                queries, fixture.ManualDate, [], timeout, CancellationToken.None);

            var currentImportRows = RequireQueryValue(
                await queries.MarketData.GetYieldCurveRatesAsync(fixture.ImportDate, fixture.ImportDate)
                    .WaitAsync(timeout),
                "import-date yield curve during cleanup");
            if (baseline.YieldCurveImportDateRows.Length > 1 || currentImportRows.Length > 1)
                throw new InvalidOperationException(
                    "Yield-curve baseline restoration requires at most one canonical row per value date.");

            var baselineRow = baseline.YieldCurveImportDateRows.SingleOrDefault();
            var currentRow = currentImportRows.SingleOrDefault();
            if (baselineRow is null && currentRow is not null)
            {
                var result = await queries.MarketDataCommands.RemoveYieldCurveRateAsync(currentRow.ValueDate, true);
                var commandId = RequireCommandId(result, "import-date baseline removal");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                actions.Add($"Removed imported row {currentRow.ValueDate:yyyy-MM-dd} with command {commandId}.");
            }
            else if (baselineRow is not null && currentRow is null)
            {
                var result = await queries.MarketDataCommands.AddYieldCurveRateAsync(baselineRow, true);
                var commandId = RequireCommandId(result, "import-date baseline add");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                actions.Add($"Restored absent baseline row {baselineRow.ValueDate:yyyy-MM-dd} with command {commandId}.");
            }
            else if (baselineRow is not null && currentRow is not null && !baselineRow.Equals(currentRow))
            {
                var result = await queries.MarketDataCommands.ChangeYieldCurveRateAsync(baselineRow, true);
                var commandId = RequireCommandId(result, "import-date baseline change");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                actions.Add($"Restored changed baseline row {baselineRow.ValueDate:yyyy-MM-dd} with command {commandId}.");
            }

            await WaitForYieldCurveRatesAsync(
                queries,
                fixture.ImportDate,
                baseline.YieldCurveImportDateRows,
                timeout,
                CancellationToken.None);
            if (actions.Count == 0)
                actions.Add("No yield-curve compensation was required; manual and import-date state already matched baseline.");
            return new G2YieldCurveCleanupEvidence(true, actions, string.Empty);
        }
        catch (Exception exception)
        {
            return new G2YieldCurveCleanupEvidence(false, actions, exception.Message);
        }
    }

    static async Task<G2EconomicCalendarCleanupEvidence> CleanupEconomicCalendarFixtureAsync(
        G0QuerySession queries,
        G2CommandEventObserver observer,
        G2EconomicCalendarFixture fixture,
        G2BaselineSnapshot baseline,
        TimeSpan timeout)
    {
        List<string> actions = [];
        try
        {
            var manualRows = await QueryEconomicCalendarsAsync(
                queries, fixture.ManualDate, fixture.CountryCode, timeout);
            foreach (var row in manualRows.Where(row => string.Equals(
                         row.EventName,
                         fixture.AddedCalendar.EventName,
                         StringComparison.Ordinal)))
            {
                var result = await queries.MarketDataCommands.RemoveEconomicCalendarAsync(row.Id, true);
                var commandId = RequireCommandId(result, "cleanup manual economic-calendar removal");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                actions.Add($"Removed manual economic-calendar row {row.Id} with command {commandId}.");
            }
            await WaitForEconomicCalendarsAsync(
                queries,
                fixture.ManualDate,
                fixture.CountryCode,
                baseline.EconomicCalendarManualDateRows,
                timeout,
                CancellationToken.None);

            var baselineRows = baseline.EconomicCalendarImportDateRows[fixture.CountryCode];
            var currentRows = await QueryEconomicCalendarsAsync(
                queries, fixture.ImportDate, fixture.CountryCode, timeout);
            var baselineById = baselineRows.ToDictionary(EconomicCalendarIdentity, StringComparer.Ordinal);
            var currentById = currentRows.ToDictionary(EconomicCalendarIdentity, StringComparer.Ordinal);

            foreach (var extra in currentById.Where(pair => !baselineById.ContainsKey(pair.Key)).Select(pair => pair.Value))
            {
                var result = await queries.MarketDataCommands.RemoveEconomicCalendarAsync(extra.Id, true);
                var commandId = RequireCommandId(result, "import-date economic-calendar baseline removal");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                actions.Add($"Removed imported economic-calendar row {extra.Id} with command {commandId}.");
            }

            foreach (var missing in baselineById.Where(pair => !currentById.ContainsKey(pair.Key)).Select(pair => pair.Value))
            {
                var result = await queries.MarketDataCommands.AddEconomicCalendarAsync(missing);
                var commandId = RequireCommandId(result, "import-date economic-calendar baseline add");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                actions.Add($"Restored absent economic-calendar row {missing.Id} with command {commandId}.");
            }

            foreach (var changed in baselineById
                         .Where(pair => currentById.TryGetValue(pair.Key, out var current)
                                        && !EconomicCalendarsEqualWithMetadata([current], [pair.Value]))
                         .Select(pair => pair.Value))
            {
                var result = await queries.MarketDataCommands.ChangeEconomicCalendarAsync(
                    changed.Id,
                    changed,
                    true);
                var commandId = RequireCommandId(result, "import-date economic-calendar baseline change");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                actions.Add($"Restored changed economic-calendar row {changed.Id} with command {commandId}.");
            }

            var restoredRows = await WaitForEconomicCalendarsAsync(
                queries,
                fixture.ImportDate,
                fixture.CountryCode,
                baselineRows,
                timeout,
                CancellationToken.None);
            if (!EconomicCalendarsEqualWithMetadata(restoredRows, baselineRows))
                throw new InvalidOperationException(
                    "Economic-calendar baseline business values were restored but provenance metadata did not match.");
            if (actions.Count == 0)
                actions.Add("No economic-calendar compensation was required; manual and import-date state already matched baseline.");
            return new G2EconomicCalendarCleanupEvidence(true, actions, string.Empty);
        }
        catch (Exception exception)
        {
            return new G2EconomicCalendarCleanupEvidence(false, actions, exception.Message);
        }
    }

    static async Task<G2LookupTypeCleanupEvidence> CleanupLookupTypeFixtureAsync(
        G0QuerySession queries,
        G2CommandEventObserver observer,
        G2LookupTypeFixture fixture,
        G2BaselineSnapshot baseline,
        TimeSpan timeout)
    {
        List<string> actions = [];
        try
        {
            var name = fixture.AddedLookupType.LookupTypeName;
            var baselineRows = baseline.RunOwnedLookupTypes
                .Where(row => string.Equals(row.LookupTypeName, name, StringComparison.Ordinal))
                .OrderBy(row => row.OrderId)
                .ToArray();
            var currentRows = RequireQueryValue(
                    await queries.Reference.GetLookupTypesAsync(name).WaitAsync(timeout),
                    $"lookup types for {name} during cleanup")
                .OrderByDescending(row => row.OrderId)
                .ToArray();

            foreach (var row in currentRows)
            {
                var result = await queries.ReferenceCommands.RemoveLookupTypeAsync(row.Id, true);
                var commandId = RequireCommandId(result, "cleanup lookup-type removal");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                actions.Add($"Removed lookup value {row.Id} with command {commandId}.");
            }

            foreach (var row in baselineRows)
            {
                var result = await queries.ReferenceCommands.AddLookupTypeAsync(row);
                var commandId = RequireCommandId(result, "lookup-type baseline add");
                await AwaitSuccessfulTerminalAsync(observer, commandId, timeout);
                actions.Add($"Restored lookup value {row.Id} with command {commandId}.");
            }

            var restoredRows = RequireQueryValue(
                    await queries.Reference.GetLookupTypesAsync(name).WaitAsync(timeout),
                    $"restored lookup types for {name}")
                .OrderBy(row => row.OrderId)
                .ToArray();
            if (restoredRows.Length != baselineRows.Length
                || restoredRows.Zip(baselineRows).Any(pair => !LookupTypeEquivalent(pair.First, pair.Second)))
                throw new InvalidOperationException("Lookup-type cleanup did not restore the isolated baseline partition.");
            if (actions.Count == 0)
                actions.Add("No lookup-type compensation was required; the isolated partition already matched baseline.");
            return new G2LookupTypeCleanupEvidence(true, actions, string.Empty);
        }
        catch (Exception exception)
        {
            return new G2LookupTypeCleanupEvidence(false, actions, exception.Message);
        }
    }

    static async Task<EconomicCalendarReadModel[]> QueryEconomicCalendarsAsync(
        G0QuerySession queries,
        DateOnly eventDate,
        string countryCode,
        TimeSpan timeout)
    {
        var queryDate = DateTime.SpecifyKind(eventDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        return RequireQueryValue(
            await queries.MarketData.GetEconomicCalendarsAsync(
                    queryDate,
                    EconomicCalendarViewType.Today,
                    countryCode)
                .WaitAsync(timeout),
            $"economic calendars for {eventDate:yyyy-MM-dd}/{countryCode} during cleanup");
    }

    static Guid RequireCommandId(ServiceResult<Guid> result, string operation)
    {
        if (!result.Success || result.Value == Guid.Empty)
            throw new InvalidOperationException(
                $"{operation} was not accepted: code={result.ErrorCode}; message={result.ErrorMessage}");
        return result.Value;
    }

    static async Task AwaitSuccessfulTerminalAsync(
        G2CommandEventObserver observer,
        Guid commandId,
        TimeSpan timeout)
    {
        var events = await observer.WaitForAsync(
            rows => rows.Any(row => row.CommandId == commandId && row.Success.HasValue),
            timeout,
            CancellationToken.None);
        var terminal = events.Last(row => row.CommandId == commandId && row.Success.HasValue);
        if (terminal.Success != true)
            throw new InvalidOperationException(
                $"Cleanup command {commandId} failed: {terminal.ErrorMessage}");
    }

    static async Task WriteFeedEvidenceAsync(
        G0EvidenceWriter evidence,
        G2CommandEventObserver observer,
        CancellationToken cancellationToken)
        => await evidence.WriteTextAsync(
            Path.Combine("network", "g2-market-data-feed-events.json"),
            JsonSerializer.Serialize(
                observer.Events.Where(row => row.Family == "MarketDataFeed"),
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

    static void RequirePassed(G0AuditRecorder recorder, string stepId, string reason)
    {
        var step = recorder.Result.Steps.SingleOrDefault(candidate => candidate.Id == stepId);
        if (step?.Status != G0StepStatus.Passed)
            throw new G0DependencyException(
                $"{reason} Dependency {stepId} status={step?.Status.ToString() ?? "NotRun"}.",
                G0StepStatus.SkippedDependency);
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
                    $"G2 requires an isolated process boundary; '{processName}' is already running as PID(s) "
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

    sealed record G2MarketDataFeedTransitionEvidence(
        Guid CommandId,
        string SourceEventName,
        string TerminalEventName,
        DateTimeOffset SourceObservedUtc,
        DateTimeOffset TerminalObservedUtc,
        G2MarketDataFeedUiState UiState);

    sealed record G2SecuritiesTransitionEvidence(
        Guid CommandId,
        string Family,
        string SourceEventName,
        string TerminalEventName,
        DateTimeOffset SourceObservedUtc,
        DateTimeOffset TerminalObservedUtc,
        G2SecuritiesEditorUiState UiState);

    sealed record G2YieldCurveTransitionEvidence(
        Guid CommandId,
        string SourceEventName,
        string TerminalEventName,
        DateTimeOffset SourceObservedUtc,
        DateTimeOffset TerminalObservedUtc,
        DateOnly? ImportDate,
        YieldCurveRateReadModel[]? ImportedYieldCurveRates,
        G2YieldCurveEditorUiState UiState);

    sealed record G2EconomicCalendarTransitionEvidence(
        Guid CommandId,
        string SourceEventName,
        string TerminalEventName,
        DateTimeOffset SourceObservedUtc,
        DateTimeOffset TerminalObservedUtc,
        DateOnly? ImportDate,
        string[]? ImportCountryCodes,
        EconomicCalendarReadModel[]? ImportedEconomicCalendars,
        G2EconomicCalendarEditorUiState UiState);

    sealed record G2LookupTypeTransitionEvidence(
        Guid CommandId,
        string SourceEventName,
        string TerminalEventName,
        DateTimeOffset SourceObservedUtc,
        DateTimeOffset TerminalObservedUtc,
        G2LookupTypeEditorUiState UiState);

    sealed record G2FundCreationTransitionEvidence(
        Guid CommandId,
        string SourceEventName,
        string TerminalEventName,
        DateTimeOffset SourceObservedUtc,
        DateTimeOffset TerminalObservedUtc,
        FundReadModel SourceFund,
        FundReadModel TerminalFund,
        G2CreatedFundUiState UiState);

    sealed record G2FundTransactionTransitionEvidence(
        Guid CommandId,
        string SourceEventName,
        string TerminalEventName,
        DateTimeOffset SourceObservedUtc,
        DateTimeOffset TerminalObservedUtc,
        FundTransactionReadModel SourceTransaction,
        FundTransactionReadModel TerminalTransaction,
        G2FundTransactionUiState UiState);

    sealed record G2FundCleanupEvidence(
        bool Succeeded,
        IReadOnlyList<string> Actions,
        string Error);

    sealed record G2SecuritiesCleanupEvidence(
        bool Succeeded,
        IReadOnlyList<string> Actions,
        string Error);

    sealed record G2YieldCurveCleanupEvidence(
        bool Succeeded,
        IReadOnlyList<string> Actions,
        string Error);

    sealed record G2EconomicCalendarCleanupEvidence(
        bool Succeeded,
        IReadOnlyList<string> Actions,
        string Error);

    sealed record G2LookupTypeCleanupEvidence(
        bool Succeeded,
        IReadOnlyList<string> Actions,
        string Error);
}
