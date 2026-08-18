using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

namespace TomasAI.IFM.UI.Net.SystemTests.Commands;

[Trait("Category", "G2StartupProcess")]
public sealed class G2PrerequisiteAndStartupAuditTests
{
    const int ExpectedStepCount = 19;

    [Fact]
    public async Task Development_command_audit_satisfies_G2_001_through_G2_019()
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
        if (securitiesSlice && yieldCurveSlice)
            throw new InvalidOperationException(
                "IFM_G2_SECURITIES_SLICE and IFM_G2_YIELD_CURVE_SLICE cannot both be enabled.");
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
                    : "G2-001-019",
            ExpectedStepCount = securitiesSlice ? 13 : yieldCurveSlice ? 11 : ExpectedStepCount,
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
        Window? marketDataWindow = null;
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
                    if (yieldCurveSlice)
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
                        + $"feedSource={(yieldCurveSlice ? "Synthetic (isolated yield/FMP slice)" : "Development configuration")}.",
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
                    if (!yieldCurveSlice
                        && (baseline.SecuritiesFixtureContract is not null
                            || baseline.SecuritiesFixtureOption is not null))
                        throw new G0DependencyException(
                            $"Exact G2 securities fixture already exists: futures={configuration.SecuritiesFuturesContractId}; "
                            + $"option={configuration.SecuritiesOptionContractId}.");
                    if (!securitiesSlice && baseline.YieldCurveManualDateRows.Length > 0)
                        throw new G0DependencyException(
                            $"Exact G2 manual yield-curve fixture already exists for {configuration.YieldCurveManualDate:yyyy-MM-dd}.");
                    if (!yieldCurveSlice)
                        securitiesFixture = await G2SecuritiesFixture.CreateAsync(
                            queries,
                            configuration,
                            process.ReadinessTimeout,
                            token);
                    if (!securitiesSlice)
                        yieldCurveFixture = await G2YieldCurveFixture.CreateAsync(
                            queries,
                            configuration,
                            process.ReadinessTimeout,
                            token);
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
                    return Observation(
                        $"valueDate={baseline.ValueDate:yyyy-MM-dd}; importDate={baseline.ImportDate:yyyy-MM-dd}; "
                        + $"securitiesFixture={(securitiesFixture is null ? "not-in-slice" : $"{securitiesFixture.FuturesContractId}/{securitiesFixture.OptionContractId}")}; "
                        + $"manualYieldDate={configuration.YieldCurveManualDate:yyyy-MM-dd}; "
                        + $"manualYieldRows={baseline.YieldCurveManualDateRows.Length}; "
                        + $"yieldRows={baseline.YieldCurveImportDateRows.Length}; "
                        + $"calendarRows={baseline.EconomicCalendarImportDateRows.Sum(pair => pair.Value.Length)}; "
                        + $"designatedFund={(baseline.DesignatedFund is null ? "absent" : $"{baseline.DesignatedFund.FundId}:{baseline.DesignatedFund.Name}")}; "
                        + $"fundTransactions={baseline.DesignatedFundTransactions.Length}; fundOrders={baseline.DesignatedFundOrders.Length}; fundTrades={baseline.DesignatedFundTrades.Length}.",
                        ["processes/g2-baseline.json", "processes/g2-securities-fixture.json", "processes/g2-yield-curve-fixture.json"]);
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

            if (!securitiesSlice && !yieldCurveSlice)
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

            if (!securitiesSlice && !yieldCurveSlice)
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

            if (!yieldCurveSlice)
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

            if (!securitiesSlice)
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
                            : "G2-001-019 harness cleanup and imported-date restoration; this is not G2-037 or G2-038 acceptance",
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

    sealed record G2SecuritiesCleanupEvidence(
        bool Succeeded,
        IReadOnlyList<string> Actions,
        string Error);

    sealed record G2YieldCurveCleanupEvidence(
        bool Succeeded,
        IReadOnlyList<string> Actions,
        string Error);
}
