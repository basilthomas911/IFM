using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

namespace TomasAI.IFM.UI.Net.SystemTests.Startup;

[Trait("Category", "G0Process")]
public sealed class G0StartupAuditTests
{
    [Fact]
    public async Task Development_desktop_startup_and_shutdown_satisfy_G0()
    {
        if (!G0Configuration.LiveRunEnabled)
            return;

        var configuration = G0Configuration.Load();
        var redactor = new SecretRedactor([Environment.GetEnvironmentVariable("FMP_API_KEY")]);
        var evidence = new G0EvidenceWriter(configuration, redactor);
        var run = new G0RunResult
        {
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
        G0UiAutomationSession? automation = null;
        FuturesContractV2ReadModel? esContract = null;
        DateOnly? valueDate = null;
        int? importedYieldCurveRows = null;
        int? importedEconomicCalendarRows = null;
        bool cleanupSucceeded = false;

        try
        {
            await SyncStep("G0-001", "Validate test configuration and create evidence directory",
                "Development paths/endpoints are valid and FMP authentication is declared.",
                _ =>
                {
                    var errors = configuration.Validate();
                    if (errors.Count > 0)
                        throw new G0DependencyException(string.Join(Environment.NewLine, errors));
                    return Observation(
                        $"Configuration valid; FMP adapter={configuration.FmpAdapter}; credentialsConfigured={configuration.FmpCredentialPresent}; evidence={evidence.RunDirectory}",
                        ["result.json", "summary.md"]);
                });

            await Step("G0-002", "Probe NATS and start typed evidence listeners",
                $"NATS accepts a connection at {configuration.NatsUri} and all typed observers run.",
                async token =>
                {
                    await InfrastructureProbe.ProbeTcpAsync(
                        new G0Endpoint("NATS", configuration.NatsUri.Host, configuration.NatsUri.Port),
                        configuration.ReadinessTimeout,
                        token);
                    observer = new G0EventObserver(configuration.NatsUri);
                    await observer.StartAsync(token);
                    return Observation("NATS reachable; eight typed event-family listeners are running.");
                });

            await Step("G0-003", "Probe PostgreSQL, ScyllaDB, and Redis",
                "Every configured Development data service accepts a connection.",
                async token =>
                {
                    List<string> failures = [];
                    foreach (var endpoint in new[] { configuration.PostgreSql, configuration.ScyllaDb, configuration.Redis })
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
                    return Observation("PostgreSQL, ScyllaDB, and Redis accepted TCP connections.");
                });

            await Step("G0-004", "Start the Development API Server",
                "The harness-owned API process starts and remains alive.",
                async token =>
                {
                    RequirePassed(recorder, "G0-002", "NATS is required by the API Server.");
                    RequirePassed(recorder, "G0-003", "Development data services are required by the API Server.");
                    if (!File.Exists(configuration.ApiExecutable))
                        throw new G0DependencyException($"API executable is missing: {configuration.ApiExecutable}");
                    api = OwnedProcess.Start(
                        configuration.ApiExecutable,
                        evidence.ApiLogDirectory,
                        redactor,
                        new Dictionary<string, string?>
                        {
                            ["ASPNETCORE_ENVIRONMENT"] = configuration.EnvironmentName
                        });
                    run.ApiProcessId = api.Process.Id.ToString();
                    await evidence.WriteTextAsync(Path.Combine("processes", "api-start.json"), api.Describe(), token);
                    if (api.Process.HasExited)
                        throw new InvalidOperationException($"API process exited with code {api.Process.ExitCode}.");
                    return Observation($"API PID {api.Process.Id} started and is owned by this run.", ["processes/api-start.json"]);
                });

            await Step("G0-005", "Verify API readiness and actor runtime",
                $"Readiness is Healthy with {configuration.ExpectedActorTypeCount} registered actor types and configured FMP.",
                async token =>
                {
                    RequirePassed(recorder, "G0-004", "The API process must be running.");
                    var readiness = await InfrastructureProbe.WaitForApiReadinessAsync(
                        configuration.ApiReadyUri,
                        configuration.ReadinessTimeout,
                        token,
                        () => (api!.Process.HasExited, api.Process.HasExited ? api.Process.ExitCode : null));
                    await evidence.WriteTextAsync(
                        Path.Combine("network", "api-readiness.json"),
                        JsonSerializer.Serialize(readiness, new JsonSerializerOptions { WriteIndented = true }),
                        token);
                    if (!string.Equals(readiness.Status, "Healthy", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"API readiness status was {readiness.Status}.");
                    if (readiness.RegisteredActorTypes != configuration.ExpectedActorTypeCount)
                        throw new InvalidOperationException(
                            $"Registered actor-type count was {readiness.RegisteredActorTypes?.ToString() ?? "missing"}; expected {configuration.ExpectedActorTypeCount}.");
                    return Observation(
                        $"API Healthy; registeredActorTypes={readiness.RegisteredActorTypes}.",
                        ["network/api-readiness.json"]);
                });

            await Step("G0-006", "Launch the desktop executable",
                "The harness-owned IFM desktop process starts and remains alive.",
                async token =>
                {
                    RequirePassed(recorder, "G0-005", "A ready actor backend is required by the desktop.");
                    RequirePassed(recorder, "G0-002", "Typed evidence listeners must precede desktop startup.");
                    desktop = OwnedProcess.Start(configuration.DesktopExecutable, evidence.UiLogDirectory, redactor);
                    run.DesktopProcessId = desktop.Process.Id.ToString();
                    await evidence.WriteTextAsync(Path.Combine("processes", "desktop-start.json"), desktop.Describe(), token);
                    if (desktop.Process.HasExited)
                        throw new InvalidOperationException($"Desktop process exited with code {desktop.Process.ExitCode}.");
                    return Observation($"Desktop PID {desktop.Process.Id} started and is owned by this run.", ["processes/desktop-start.json"]);
                });

            await Step("G0-007", "Await desktop NATS readiness",
                $"The desktop establishes NATS transport to port {configuration.NatsUri.Port}, directly or through typed UI-initiated traffic when a local container proxy owns the socket.",
                async token =>
                {
                    RequireDesktop(desktop);
                    RequireObserver(observer);
                    var readiness = await WaitForDesktopNatsReadinessAsync(
                        desktop!.Process.Id,
                        observer!,
                        configuration.NatsUri.Port,
                        configuration.ReadinessTimeout,
                        token);
                    await WriteConnectionsAsync(evidence, "desktop-nats-ready.json", readiness.ProcessConnections, token);
                    await WriteConnectionsAsync(evidence, "nats-endpoint-ready.json", readiness.EndpointConnections, token);
                    return Observation(
                        $"Desktop NATS transport established; evidence={readiness.EvidenceKind}.",
                        ["network/desktop-nats-ready.json", "network/nats-endpoint-ready.json"]);
                });

            await Step("G0-008", "Find the responsive main window",
                "IFMAppView has the expected Development title and responds through UI Automation.",
                async token =>
                {
                    RequireDesktop(desktop);
                    automation = new G0UiAutomationSession(desktop!.Process.Id);
                    var window = await automation.WaitForMainWindowAsync(configuration.StartupTimeout, token);
                    if (!window.Title.Contains("Investment Fund Manager", StringComparison.OrdinalIgnoreCase)
                        || !window.Title.Contains("DEV", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"Unexpected main-window title: '{window.Title}'.");
                    automation.DumpAutomationTree(Path.Combine(evidence.AutomationTreeDirectory, "main-window.txt"));
                    return Observation($"Responsive window found: {window.Title}", ["automation-trees/main-window.txt"]);
                });

            await Step("G0-009", "Audit desktop network transport",
                "Typed UI command traffic proves NATS use and the desktop has no connection to API HTTP port 22543.",
                async token =>
                {
                    RequirePassed(recorder, "G0-007", "A desktop NATS connection must exist.");
                    RequireObserver(observer);
                    await observer!.WaitForAsync(
                        HasUiInitiatedNatsTraffic,
                        configuration.ReadinessTimeout,
                        token);
                    var connections = await InfrastructureProbe.GetProcessTcpConnectionsAsync(desktop!.Process.Id, token);
                    await WriteConnectionsAsync(evidence, "desktop-network-audit.json", connections, token);
                    if (connections.Any(row => InfrastructureProbe.GetPort(row.RemoteEndpoint) == configuration.ApiReadyUri.Port))
                        throw new InvalidOperationException("Desktop directly connected to the API HTTP port.");
                    return Observation(
                        "Typed UI-initiated NATS traffic was observed and the desktop has no API HTTP connection.",
                        ["network/desktop-network-audit.json", "network/nats-events.json"]);
                });

            await Step("G0-010", "Observe initial listeners and reference-data command intake",
                "Status activity and both parameter-only reference-data import events are observed.",
                async token =>
                {
                    RequireDesktop(desktop);
                    RequireObserver(observer);
                    var events = await observer!.WaitForAsync(
                        rows => rows.Any(row => row.Family == "Status")
                            && rows.Any(row => row.Family == "YieldCurve" && row.Verb == "Imported")
                            && rows.Any(row => row.Family == "EconomicCalendar" && row.Verb == "Imported"),
                        configuration.StartupTimeout,
                        token);
                    return Observation(
                        $"Observed status traffic and both import requests; eventCount={events.Count}.",
                        ["network/nats-events.json"]);
                });

            await Step("G0-011", "Query the current ES futures contract",
                "A valid currently traded ES contract is returned through the NATS client API.",
                async token =>
                {
                    RequirePassed(recorder, "G0-005", "Queries require a ready backend.");
                    queries = new G0QuerySession(configuration.NatsUri);
                    await queries.StartAsync(configuration.RunId, token);
                    var result = await queries.MarketData.GetCurrentlyTradedFuturesContractAsync("ES")
                        .WaitAsync(configuration.ReadinessTimeout, token);
                    if (!result.Success || result.Value is null || !result.Value.IsValid || !result.Value.CurrentlyTraded)
                        throw new G0DependencyException(
                            $"Current ES contract is unavailable: code={result.ErrorCode}; message={result.ErrorMessage}");
                    esContract = result.Value;
                    return Observation($"Current ES contract={esContract.ContractId}; lastTradeDate={esContract.LastTradeDate:yyyy-MM-dd}.");
                });

            await Step("G0-012", "Load conditional EOD, trade-signal, and bar seed state",
                "The current ES contract has deterministic latest EOD, signal, and bar state.",
                async token =>
                {
                    RequireQueries(queries);
                    RequireContract(esContract);
                    var seed = await G0DevelopmentDataFixture.EnsureAsync(
                        queries!, esContract!, configuration.ReadinessTimeout, token);
                    var eod = await queries!.MarketDataFeed
                        .GetLastFuturesEodDataAsync(esContract!.ContractId, esContract.LastTradeDate)
                        .WaitAsync(configuration.ReadinessTimeout, token);
                    var signal = await queries.MarketDataAnalytics.GetLastFuturesTradeSignalAsync()
                        .WaitAsync(configuration.ReadinessTimeout, token);
                    var bar = await queries.MarketDataFeed
                        .GetLastFuturesBarDataAsync(esContract.ContractId, esContract.Symbol, DateOnly.FromDateTime(DateTime.UtcNow))
                        .WaitAsync(configuration.ReadinessTimeout, token);
                    List<string> unavailable = [];
                    if (!eod.Success || eod.Value is null) unavailable.Add($"EOD: {eod.ErrorMessage}");
                    if (!signal.Success || signal.Value is null) unavailable.Add($"trade signal: {signal.ErrorMessage}");
                    if (!bar.Success || bar.Value is null) unavailable.Add($"bar: {bar.ErrorMessage}");
                    if (unavailable.Count > 0)
                        throw new G0DependencyException("Deterministic startup state is incomplete: " + string.Join("; ", unavailable));
                    return Observation(
                        $"Latest ES EOD, trade-signal, and bar records loaded; "
                        + $"Development seed date={seed.ValueDate:yyyy-MM-dd}; EOD inserted={seed.EodSeeded}; bar inserted={seed.BarSeeded}.");
                });

            await Step("G0-013", "Observe automatic FMP yield-curve import",
                "Exactly one request has a matching successful terminal event and durable result.",
                async token =>
                {
                    RequireDesktop(desktop);
                    RequireObserver(observer);
                    RequireQueries(queries);
                    var pair = await WaitForSuccessfulImportAsync(observer!, "YieldCurve", configuration.StartupTimeout, token);
                    importedYieldCurveRows = pair.Terminal.RecordCount ?? 0;
                    if (importedYieldCurveRows > 0)
                    {
                        var stored = await queries!.MarketData.GetLastYieldCurveRateAsync()
                            .WaitAsync(configuration.ReadinessTimeout, token);
                        if (!stored.Success || stored.Value is null)
                            throw new InvalidOperationException("Yield-curve terminal event succeeded but durable query returned no record.");
                    }
                    return Observation(
                        $"Yield-curve commandId={pair.Request.CommandId}; importedRows={importedYieldCurveRows}; durable={(importedYieldCurveRows == 0 ? "valid-zero-row" : "confirmed")}.");
                });

            await Step("G0-014", "Observe automatic FMP economic-calendar import",
                "Exactly one request has a matching successful terminal event and durable result.",
                async token =>
                {
                    RequireObserver(observer);
                    RequireQueries(queries);
                    var pair = await WaitForSuccessfulImportAsync(observer!, "EconomicCalendar", configuration.StartupTimeout, token);
                    importedEconomicCalendarRows = pair.Terminal.RecordCount ?? 0;
                    if (importedEconomicCalendarRows > 0)
                    {
                        var stored = await queries!.MarketData.GetEconomicCalendarsAsync()
                            .WaitAsync(configuration.ReadinessTimeout, token);
                        if (!stored.Success || stored.Value is null || stored.Value.Length == 0)
                            throw new InvalidOperationException("Economic-calendar terminal event succeeded but durable query returned no records.");
                    }
                    return Observation(
                        $"Economic-calendar commandId={pair.Request.CommandId}; importedRows={importedEconomicCalendarRows}; durable={(importedEconomicCalendarRows == 0 ? "valid-zero-row" : "confirmed")}.");
                });

            await SyncStep("G0-015", "Verify startup-import lifecycle policy",
                "Each dataset has one request, one correlated terminal event, no retry, and successful completion.",
                _ =>
                {
                    RequireDesktop(desktop);
                    RequireObserver(observer);
                    foreach (var family in new[] { "YieldCurve", "EconomicCalendar" })
                    {
                        var rows = observer!.Events.Where(row => row.Family == family).ToArray();
                        var requests = rows.Where(row => row.Verb == "Imported").ToArray();
                        var terminals = rows.Where(row => row.Success.HasValue).ToArray();
                        if (requests.Length != 1 || terminals.Length != 1)
                            throw new InvalidOperationException(
                                $"{family} observed requests={requests.Length}, terminalEvents={terminals.Length}; expected exactly one of each and no retry.");
                        if (requests[0].CommandId == Guid.Empty || requests[0].CommandId != terminals[0].CommandId)
                            throw new InvalidOperationException($"{family} terminal event did not match the submitted command ID.");
                        if (terminals[0].Success != true)
                            throw new InvalidOperationException($"{family} import failed: {terminals[0].Message}");
                    }
                    return Observation("Both imports completed once with exact command correlation; no retry was observed.");
                });

            await Step("G0-016", "Query and render the application value date",
                "A valid value date is returned through NATS before live startup continues.",
                async token =>
                {
                    RequireQueries(queries);
                    var result = await queries!.MarketData.GetValueDateAsync()
                        .WaitAsync(configuration.ReadinessTimeout, token);
                    if (!result.Success || result.Value is null
                        || result.Value.Value == DateOnly.MinValue
                        || result.Value.Value == DateOnly.MaxValue)
                        throw new G0DependencyException(
                            $"Application value date unavailable: code={result.ErrorCode}; message={result.ErrorMessage}");
                    valueDate = result.Value.Value;
                    return Observation($"Application value date={valueDate:yyyy-MM-dd}.");
                });

            await Step("G0-017", "Render economic-calendar state",
                "Country, date, and list controls are present; non-empty imports render rows.",
                async token =>
                {
                    RequireAutomation(automation);
                    var state = await WaitForEconomicCalendarAsync(
                        automation!,
                        importedEconomicCalendarRows.GetValueOrDefault() > 0,
                        configuration.StartupTimeout,
                        token);
                    return Observation(
                        $"CalendarDate='{state.CalendarDate}'; Country='{state.Country}'; automationRows={state.AutomationDescendantCount}.");
                });

            await Step("G0-018", "Observe required consumer startup",
                "The UI starts only composite market-outlook, bar, and placement presentation consumers.",
                async token =>
                {
                    RequireDesktop(desktop);
                    RequireObserver(observer);
                    string[] expectedMessages =
                    [
                        "Starting Market Outlook Event Consumer",
                        "Starting Futures Bar Data Event Consumer",
                        "Starting Trade Placement Event Consumer"
                    ];
                    var events = await observer!.WaitForAsync(
                        rows => expectedMessages.All(expected => CountStatus(rows, expected) >= 1),
                        configuration.StartupTimeout,
                        token);
                    foreach (var expected in expectedMessages)
                    {
                        var count = CountStatus(events, expected);
                        if (count != 1)
                            throw new InvalidOperationException($"Status '{expected}' appeared {count} times; expected exactly once.");
                    }
                    return Observation("All three presentation-consumer startup statuses appeared exactly once; no UI reset listener is owned.");
                });

            await Step("G0-019", "Start the current futures feed",
                "The feed start request has one matching successful terminal event.",
                async token =>
                {
                    RequireObserver(observer);
                    RequireContract(esContract);
                    var events = await observer!.WaitForAsync(
                        rows => rows.Any(row => row.Family == "MarketDataFeed" && row.Verb == "StartedComplete"),
                        configuration.StartupTimeout,
                        token);
                    var request = events.SingleOrDefault(row => row.Family == "MarketDataFeed" && row.Verb == "Started")
                        ?? throw new InvalidOperationException("Feed Started event was not observed exactly once.");
                    var terminal = events.SingleOrDefault(row => row.Family == "MarketDataFeed" && row.Verb == "StartedComplete")
                        ?? throw new InvalidOperationException("Feed StartedComplete event was not observed exactly once.");
                    if (request.CommandId != terminal.CommandId || terminal.Success != true)
                        throw new InvalidOperationException("Feed start terminal correlation failed.");
                    return Observation($"Market-data feed started; commandId={request.CommandId}.");
                });

            await Step("G0-020", "Start the authoritative intraday analytics profile",
                "Exactly 24 RSI-13/ATR-14/ADX-14/MACD-9/12/26 Started identities are observed and no daily identity starts.",
                async token =>
                {
                    RequireObserver(observer);
                    RequireContract(esContract);
                    RequireValueDate(valueDate);
                    var expected = ExpectedSignalIdentities(esContract!.ContractId, valueDate!.Value);
                    var events = await observer!.WaitForAsync(
                        rows => SignalEvents(rows, "Started").Count >= expected.Count,
                        configuration.StartupTimeout,
                        token);
                    var actual = SignalEvents(events, "Started");
                    AssertExactSignalProfile(expected, actual, "Started");
                    return Observation("All 24 configured intraday signal identities started exactly once.");
                });

            await Step("G0-021", "Reach initialized shell state",
                "The actor-owned Analytics activity and presentation initialization statuses appear and shell actions are enabled.",
                async token =>
                {
                    RequireObserver(observer);
                    RequireAutomation(automation);
                    var events = await observer!.WaitForAsync(
                        rows => CountStatus(rows, "StartRealtimeAnalytics => Started") == 1
                            && CountStatus(rows, "presentation initialization complete") == 1,
                        configuration.StartupTimeout,
                        token);
                    var controls = await WaitForEnabledToolbarAsync(
                        automation!,
                        configuration.StartupTimeout,
                        token);
                    return Observation(
                        $"Presentation initialized after actor-owned Analytics startup; toolbar={string.Join(",", controls.Select(pair => $"{pair.Key}:{pair.Value}"))}.");
                });

            await SyncStep("G0-022", "Request normal main-window close",
                "UI Automation close is accepted without force-killing the desktop.",
                _ =>
                {
                    RequireAutomation(automation);
                    RequireDesktop(desktop);
                    automation!.RequestClose();
                    return Observation("Normal main-window close was requested.");
                });

            await Step("G0-023", "Observe analytics and transport shutdown",
                "The desktop exits within the threshold without stopping API-owned analytics.",
                async token =>
                {
                    RequireObserver(observer);
                    RequireContract(esContract);
                    RequireValueDate(valueDate);
                    RequireDesktop(desktop);
                    var exited = await desktop!.WaitForExitAsync(configuration.ShutdownTimeout, token);
                    if (!exited)
                        throw new TimeoutException($"Desktop did not exit within {configuration.ShutdownTimeout}.");
                    if (desktop.ForcedTermination)
                        throw new InvalidOperationException("Desktop required forced termination.");
                    if (SignalEvents(observer!.Events, "Stopped").Count != 0)
                        throw new InvalidOperationException("Closing the UI stopped API-owned Analytics actors.");
                    return Observation("Desktop exited normally and API-owned Analytics actors were not stopped.");
                });

            await Step("G0-024", "Verify feed remains API-owned",
                "Closing the UI does not submit a market-data feed stop command.",
                async token =>
                {
                    RequireObserver(observer);
                    await Task.Delay(TimeSpan.FromMilliseconds(250), token);
                    if (observer!.Events.Any(row => row.Family == "MarketDataFeed" && row.Verb is "Stopped" or "StoppedComplete"))
                        throw new InvalidOperationException("Closing the UI submitted a market-data feed stop operation.");
                    return Observation("No feed stop operation was observed after UI close.");
                });

            await Step("G0-025", "Verify bounded exit and cleanup",
                "No error-coded status, desktop process, network connection, listener, signal timer, or harness backend remains.",
                async token =>
                {
                    List<string> cleanupFailures = [];
                    if (desktop is not null && !desktop.Process.HasExited)
                    {
                        if (!await desktop.TerminateOwnedTreeAsync(TimeSpan.FromSeconds(5), token))
                            cleanupFailures.Add("Desktop process tree did not terminate.");
                        else
                            cleanupFailures.Add("Desktop required forced cleanup.");
                    }

                    if (desktop is not null)
                    {
                        var connections = await InfrastructureProbe.GetProcessTcpConnectionsAsync(desktop.Process.Id, token);
                        await WriteConnectionsAsync(evidence, "desktop-after-exit.json", connections, token);
                        if (connections.Count > 0)
                            cleanupFailures.Add($"Desktop PID still owns {connections.Count} TCP connection(s).");
                    }

                    if (queries is not null)
                    {
                        await queries.DisposeAsync();
                        queries = null;
                    }
                    if (observer is not null)
                    {
                        var errorStatuses = observer.Events
                            .Where(IsErrorCodedStatus)
                            .ToArray();
                        if (errorStatuses.Length != 0)
                        {
                            cleanupFailures.Add(
                                $"Observed {errorStatuses.Length} error-coded status message(s): "
                                + string.Join(" | ", errorStatuses
                                    .Take(3)
                                    .Select(row => $"{row.EntityId}: {row.Message}")));
                        }
                        await observer.WriteEvidenceAsync(evidence, token);
                        await observer.DisposeAsync();
                        observer = null;
                    }
                    if (api is not null && !api.Process.HasExited
                        && !await api.TerminateOwnedTreeAsync(TimeSpan.FromSeconds(10), token))
                        cleanupFailures.Add("Harness-owned API process tree did not terminate.");

                    if (api is not null)
                        await evidence.WriteTextAsync(Path.Combine("processes", "api-final.json"), api.Describe(), token);
                    if (desktop is not null)
                        await evidence.WriteTextAsync(Path.Combine("processes", "desktop-final.json"), desktop.Describe(), token);
                    if (cleanupFailures.Count > 0)
                        throw new InvalidOperationException(string.Join(Environment.NewLine, cleanupFailures));
                    cleanupSucceeded = true;
                    return Observation(
                        "No error-coded status was observed; desktop exited normally; NATS/query observers stopped; harness-owned API was removed; no desktop TCP connections remain.",
                        ["network/desktop-after-exit.json", "network/nats-events.json", "processes/api-final.json", "processes/desktop-final.json"]);
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
            $"G0 evidence was written to {evidence.RunDirectory}; "
            + string.Join("; ", run.Steps.Where(step => step.Status != G0StepStatus.Passed).Select(step => $"{step.Id}={step.Status}: {step.Actual}")));

        async Task<G0StepResult> Step(
            string id,
            string name,
            string expected,
            Func<CancellationToken, Task<G0StepObservation>> action)
        {
            var step = await recorder.RunAsync(id, name, expected, action, cancellationToken);
            if (step.Status == G0StepStatus.Failed && automation is not null)
            {
                try
                {
                    automation.CaptureScreenshot(Path.Combine(evidence.ScreenshotDirectory, $"{id}.png"));
                    automation.DumpAutomationTree(Path.Combine(evidence.AutomationTreeDirectory, $"{id}.txt"));
                }
                catch { /* A failure artifact must not hide the original step failure. */ }
            }
            await evidence.WriteResultAsync(run, CancellationToken.None);
            return step;
        }

        Task<G0StepResult> SyncStep(
            string id,
            string name,
            string expected,
            Func<CancellationToken, G0StepObservation> action)
            => Step(id, name, expected, token => Task.FromResult(action(token)));
    }

    static G0StepObservation Observation(string actual, IReadOnlyList<string>? evidence = null)
        => new(string.Empty, actual, G0StepStatus.Passed, evidence);

    static void RequirePassed(G0AuditRecorder recorder, string stepId, string reason)
    {
        var step = recorder.Result.Steps.SingleOrDefault(candidate => candidate.Id == stepId);
        if (step?.Status != G0StepStatus.Passed)
            throw new G0DependencyException($"{reason} Dependency {stepId} status={step?.Status.ToString() ?? "NotRun"}.");
    }

    static void RequireDesktop(OwnedProcess? desktop)
    {
        if (desktop is null || desktop.Process.HasExited)
            throw new G0DependencyException("A running harness-owned desktop process is required.", G0StepStatus.SkippedDependency);
    }

    static void RequireObserver(G0EventObserver? observer)
    {
        if (observer is null)
            throw new G0DependencyException("The typed NATS evidence observer is unavailable.", G0StepStatus.SkippedDependency);
    }

    static void RequireQueries(G0QuerySession? queries)
    {
        if (queries is null)
            throw new G0DependencyException("The NATS query session is unavailable.", G0StepStatus.SkippedDependency);
    }

    static void RequireAutomation(G0UiAutomationSession? automation)
    {
        if (automation is null)
            throw new G0DependencyException("The main-window UI Automation session is unavailable.", G0StepStatus.SkippedDependency);
    }

    static void RequireContract(FuturesContractV2ReadModel? contract)
    {
        if (contract is null)
            throw new G0DependencyException("A current ES contract is unavailable.", G0StepStatus.SkippedDependency);
    }

    static void RequireValueDate(DateOnly? valueDate)
    {
        if (!valueDate.HasValue)
            throw new G0DependencyException("The application value date is unavailable.", G0StepStatus.SkippedDependency);
    }

    static async Task<G0DesktopNatsReadiness> WaitForDesktopNatsReadinessAsync(
        int processId,
        G0EventObserver observer,
        int natsPort,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        IReadOnlyList<TcpConnectionEvidence> processConnections = [];
        IReadOnlyList<TcpConnectionEvidence> endpointConnections = [];
        while (!timeoutSource.IsCancellationRequested)
        {
            try
            {
                processConnections = await InfrastructureProbe.GetProcessTcpConnectionsAsync(
                    processId,
                    timeoutSource.Token);
                endpointConnections = await InfrastructureProbe.GetPortTcpConnectionsAsync(
                    natsPort,
                    timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (processConnections.Any(row => InfrastructureProbe.GetPort(row.RemoteEndpoint) == natsPort))
                return new G0DesktopNatsReadiness(processConnections, endpointConnections, "desktop PID socket");
            if (endpointConnections.Count > 0 && HasUiInitiatedNatsTraffic(observer.Events))
                return new G0DesktopNatsReadiness(processConnections, endpointConnections, "typed UI traffic through endpoint proxy");

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        throw new TimeoutException($"Desktop NATS readiness was not observed within {timeout}.");
    }

    static bool HasUiInitiatedNatsTraffic(IReadOnlyList<G0ObservedEvent> events)
        => events.Any(row => row.Verb == "Imported" && row.Family is "YieldCurve" or "EconomicCalendar");

    static Task WriteConnectionsAsync(
        G0EvidenceWriter evidence,
        string fileName,
        IReadOnlyList<TcpConnectionEvidence> connections,
        CancellationToken cancellationToken)
        => evidence.WriteTextAsync(
            Path.Combine("network", fileName),
            JsonSerializer.Serialize(connections, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

    static async Task<G0EconomicCalendarUiState> WaitForEconomicCalendarAsync(
        G0UiAutomationSession automation,
        bool rowsRequired,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        Exception? lastFailure = null;
        while (!timeoutSource.IsCancellationRequested)
        {
            try
            {
                var state = automation.ReadEconomicCalendarState();
                if (!string.IsNullOrWhiteSpace(state.CalendarDate)
                    && (!rowsRequired || state.AutomationDescendantCount > 0))
                    return state;
            }
            catch (Exception exception)
            {
                lastFailure = exception;
            }
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        throw new TimeoutException("Economic-calendar country/date/list state did not render.", lastFailure);
    }

    static async Task<IReadOnlyDictionary<string, bool>> WaitForEnabledToolbarAsync(
        G0UiAutomationSession automation,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        Exception? lastFailure = null;
        IReadOnlyDictionary<string, bool>? controls = null;
        while (!timeoutSource.IsCancellationRequested)
        {
            try
            {
                controls = automation.ReadToolbarEnabledState();
                if (controls.Values.All(static enabled => enabled))
                    return controls;
                lastFailure = new InvalidOperationException(
                    "One or more shell toolbar actions remained disabled: "
                    + string.Join(", ", controls.Select(pair => $"{pair.Key}={pair.Value}")));
            }
            catch (Exception exception)
            {
                lastFailure = exception;
            }
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        throw new TimeoutException("The initialized shell toolbar did not become readable and enabled.", lastFailure);
    }

    static async Task<(G0ObservedEvent Request, G0ObservedEvent Terminal)> WaitForSuccessfulImportAsync(
        G0EventObserver observer,
        string family,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var events = await observer.WaitForAsync(
            rows => rows.Any(row => row.Family == family && row.Success.HasValue),
            timeout,
            cancellationToken);
        var requests = events.Where(row => row.Family == family && row.Verb == "Imported").ToArray();
        var terminals = events.Where(row => row.Family == family && row.Success.HasValue).ToArray();
        if (requests.Length != 1 || terminals.Length != 1)
            throw new InvalidOperationException(
                $"{family} import observed requests={requests.Length}, terminalEvents={terminals.Length}; expected exactly one each.");
        if (requests[0].CommandId == Guid.Empty || requests[0].CommandId != terminals[0].CommandId)
            throw new InvalidOperationException($"{family} import terminal command ID does not match its request.");
        if (terminals[0].Success != true)
            throw new InvalidOperationException($"{family} import failed: {terminals[0].Message}");
        return (requests[0], terminals[0]);
    }

    static HashSet<string> ExpectedSignalIdentities(string contractId, DateOnly valueDate)
        => FuturesIntradaySignalActivationProfile.Create(contractId, valueDate)
            .SelectMany(activation => new[]
            {
                $"RSI:{activation.Rsi.Format()}",
                $"ATR:{activation.Atr.Format()}",
                $"ADX:{activation.Adx.Format()}",
                $"MACD:{activation.Macd.Format()}"
            })
            .ToHashSet(StringComparer.Ordinal);

    static IReadOnlyList<G0ObservedEvent> SignalEvents(IReadOnlyList<G0ObservedEvent> events, string verb)
        => events.Where(row => row.Verb == verb && row.Family is "RSI" or "ATR" or "ADX" or "MACD").ToArray();

    static void AssertExactSignalProfile(
        HashSet<string> expected,
        IReadOnlyList<G0ObservedEvent> actualEvents,
        string verb)
    {
        var actual = actualEvents.Select(row => $"{row.Family}:{row.EntityId}").ToArray();
        var actualSet = actual.ToHashSet(StringComparer.Ordinal);
        var missing = expected.Except(actualSet, StringComparer.Ordinal).ToArray();
        var unexpected = actualSet.Except(expected, StringComparer.Ordinal).ToArray();
        var duplicates = actual.GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => $"{group.Key} x{group.Count()}")
            .ToArray();
        if (actual.Length != 24 || missing.Length > 0 || unexpected.Length > 0 || duplicates.Length > 0)
            throw new InvalidOperationException(
                $"Signal {verb} profile mismatch. count={actual.Length}; missing=[{string.Join(",", missing)}]; "
                + $"unexpected=[{string.Join(",", unexpected)}]; duplicates=[{string.Join(",", duplicates)}].");
    }

    static int CountStatus(IReadOnlyList<G0ObservedEvent> events, string text)
        => events.Count(row => row.Family == "Status"
            && row.Message.Contains(text, StringComparison.OrdinalIgnoreCase));

    static bool IsErrorCodedStatus(G0ObservedEvent row)
    {
        if (row.Family != "Status") return false;
        var separator = row.Message.IndexOf(':');
        return separator > 0
            && row.Message.AsSpan(0, separator).IndexOfAnyExceptInRange('0', '9') < 0;
    }
}

public sealed record G0DesktopNatsReadiness(
    IReadOnlyList<TcpConnectionEvidence> ProcessConnections,
    IReadOnlyList<TcpConnectionEvidence> EndpointConnections,
    string EvidenceKind);
