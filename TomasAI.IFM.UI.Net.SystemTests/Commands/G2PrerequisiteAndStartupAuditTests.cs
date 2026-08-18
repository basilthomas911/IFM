using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

namespace TomasAI.IFM.UI.Net.SystemTests.Commands;

[Trait("Category", "G2StartupProcess")]
public sealed class G2PrerequisiteAndStartupAuditTests
{
    const int ExpectedStepCount = 9;

    [Fact]
    public async Task Development_command_audit_satisfies_G2_001_through_G2_009()
    {
        if (!G0Configuration.G2StartupLiveRunEnabled)
            return;

        var configuration = G2Configuration.Load();
        var process = configuration.Process;
        var redactor = new SecretRedactor([Environment.GetEnvironmentVariable("FMP_API_KEY")]);
        var evidence = new G0EvidenceWriter(process, redactor);
        var run = new G0RunResult
        {
            Gate = "G2-001-009",
            ExpectedStepCount = ExpectedStepCount,
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
                    api = OwnedProcess.Start(
                        process.ApiExecutable,
                        evidence.ApiLogDirectory,
                        redactor,
                        new Dictionary<string, string?> { ["ASPNETCORE_ENVIRONMENT"] = process.EnvironmentName });
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
                        $"API PID {api.Process.Id} is Healthy; registeredActorTypes={readiness.RegisteredActorTypes}.",
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
                    await evidence.WriteTextAsync(
                        Path.Combine("processes", "g2-baseline.json"),
                        JsonSerializer.Serialize(baseline, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    return Observation(
                        $"valueDate={baseline.ValueDate:yyyy-MM-dd}; importDate={baseline.ImportDate:yyyy-MM-dd}; "
                        + $"yieldRows={baseline.YieldCurveImportDateRows.Length}; "
                        + $"calendarRows={baseline.EconomicCalendarImportDateRows.Sum(pair => pair.Value.Length)}; "
                        + $"designatedFund={(baseline.DesignatedFund is null ? "absent" : $"{baseline.DesignatedFund.FundId}:{baseline.DesignatedFund.Name}")}; "
                        + $"fundTransactions={baseline.DesignatedFundTransactions.Length}; fundOrders={baseline.DesignatedFundOrders.Length}; fundTrades={baseline.DesignatedFundTrades.Length}.",
                        ["processes/g2-baseline.json"]);
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
        }
        finally
        {
            if (automation is not null && desktop is not null && !desktop.Process.HasExited)
            {
                try
                {
                    automation.CloseAllSecondaryWindows();
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
                    Scope = "G2-001-009 harness cleanup; this is not G2-037 or G2-038 acceptance",
                    Succeeded = run.CleanupSucceeded,
                    Failures = cleanupFailures
                }, new JsonSerializerOptions { WriteIndented = true }));
            await evidence.WriteResultAsync(run, CancellationToken.None);
        }

        run.Passed.Should().BeTrue(
            $"G2-001 through G2-009 evidence was written to {evidence.RunDirectory}; "
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
}
