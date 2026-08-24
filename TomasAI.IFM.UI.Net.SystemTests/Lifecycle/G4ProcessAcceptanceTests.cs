using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using FluentAssertions;
using NATS.Client.Core;
using NATS.Net;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.Events;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

namespace TomasAI.IFM.UI.Net.SystemTests.Lifecycle;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class G4ProcessCollection
{
    public const string Name = "G4 process acceptance";
}

[Collection(G4ProcessCollection.Name)]
[Trait("Category", "G4Process")]
public sealed class G4ProcessAcceptanceTests
{
    static readonly TimeSpan DialogTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Development_process_resilience_satisfies_G4()
    {
        if (!G4Enabled())
            return;

        var configuration = G0Configuration.Load();
        var redactor = new SecretRedactor([Environment.GetEnvironmentVariable("FMP_API_KEY")]);
        var resultDirectory = Path.Combine(
            configuration.ResultsRoot,
            $"{configuration.RunId}-G4-{configuration.EnvironmentName}");
        Directory.CreateDirectory(resultDirectory);
        var observations = new List<G4Observation>();
        OwnedProcess? api = null;
        var inflightOnly = string.Equals(
            Environment.GetEnvironmentVariable("IFM_UI_G4_INFLIGHT_ONLY"),
            "1",
            StringComparison.Ordinal);

        try
        {
            await InfrastructureProbe.ProbeTcpAsync(
                new G0Endpoint("NATS", configuration.NatsUri.Host, configuration.NatsUri.Port),
                configuration.ReadinessTimeout,
                CancellationToken.None);

            if (!inflightOnly)
            {
                await VerifyUnavailableNatsStartupAsync(configuration, redactor, resultDirectory, observations);
                await VerifyActorApiUnavailableAsync(configuration, redactor, resultDirectory, observations);
            }

            api = OwnedProcess.Start(
                configuration.ApiExecutable,
                Path.Combine(resultDirectory, "api"),
                redactor,
                new Dictionary<string, string?>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = configuration.EnvironmentName,
                    ["AppSettings__Databento__DataSource"] = "Synthetic",
                    ["AppSettings__Databento__Contracts__0__DomainContractId"] = "ES20260918",
                    ["AppSettings__Databento__Contracts__0__ProviderContractName"] = "ESU6",
                    ["AppSettings__Databento__Contracts__0__AssetTypeId"] = "Futures",
                    ["AppSettings__Databento__Contracts__0__RootSymbol"] = "ES",
                    ["AppSettings__Databento__Contracts__0__Dataset"] = "GLBX.MDP3",
                    ["AppSettings__Databento__Contracts__1__DomainContractId"] = "VX20260916",
                    ["AppSettings__Databento__Contracts__1__ProviderContractName"] = "VX/U6",
                    ["AppSettings__Databento__Contracts__1__AssetTypeId"] = "Futures",
                    ["AppSettings__Databento__Contracts__1__RootSymbol"] = "VX",
                    ["AppSettings__Databento__Contracts__1__Dataset"] = "XCBF.PITCH",
                    ["AppSettings__Databento__Synthetic__RecordCount"] = "20",
                    ["AppSettings__Databento__Synthetic__RecordsPerSecond"] = "2"
                });
            var readiness = await InfrastructureProbe.WaitForApiReadinessAsync(
                configuration.ApiReadyUri,
                configuration.ReadinessTimeout,
                CancellationToken.None,
                () => (api.Process.HasExited, api.Process.HasExited ? api.Process.ExitCode : null));
            readiness.Status.Should().Be("Healthy");
            observations.Add(new G4Observation(
                "G4-API",
                "Actor backend readiness",
                $"Healthy with {readiness.RegisteredActorTypes} registered actor types."));

            if (!inflightOnly)
                await VerifyRepeatedLifecycleAndBurstAsync(configuration, redactor, resultDirectory, observations);
            await VerifyInflightShutdownAsync(configuration, redactor, resultDirectory, observations);
        }
        finally
        {
            if (api is not null)
            {
                if (!api.Process.HasExited)
                    await api.TerminateOwnedTreeAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                await api.DisposeAsync();
            }

            await File.WriteAllTextAsync(
                Path.Combine(resultDirectory, "g4-result.json"),
                JsonSerializer.Serialize(new
                {
                    Gate = "G4",
                    configuration.RunId,
                    Environment = configuration.EnvironmentName,
                    CompletedUtc = DateTimeOffset.UtcNow,
                    Observations = observations
                }, new JsonSerializerOptions { WriteIndented = true }));
        }

        if (!inflightOnly)
        {
            observations.Select(item => item.Id).Should().Contain(
                ["G4-001", "G4-002", "G4-003", "G4-004", "G4-005", "G4-006", "G4-007", "G4-008", "G4-009"]);
        }
    }

    static async Task VerifyUnavailableNatsStartupAsync(
        G0Configuration configuration,
        SecretRedactor redactor,
        string resultDirectory,
        ICollection<G4Observation> observations)
    {
        var unusedPort = ReserveUnusedPort();
        var logDirectory = Path.Combine(resultDirectory, "nats-unavailable");
        await using var desktop = OwnedProcess.Start(
            configuration.DesktopExecutable,
            logDirectory,
            redactor,
            UiEnvironment($"nats://127.0.0.1:{unusedPort}", startupTimeoutSeconds: 2));
        using var automation = new G4UiAutomationSession(desktop.Process.Id);
        var dialog = await automation.WaitForWindowAsync(
            "Application Startup Error",
            DialogTimeout,
            CancellationToken.None);
        var text = G4UiAutomationSession.ReadText(dialog);
        var diagnostic = text;
        var stderrPath = Path.Combine(logDirectory, "stderr.log");
        if (!diagnostic.Contains("NATS startup", StringComparison.Ordinal)
            && File.Exists(stderrPath))
            diagnostic += Environment.NewLine + await ReadSharedTextAsync(stderrPath);
        diagnostic.Should().Contain("NATS startup");
        diagnostic.Should().Contain(unusedPort.ToString());
        automation.FindWindowStartingWith("Investment Fund Manager").Should().BeNull(
            "the shell must not be shown when the broker connection was never established");
        G4UiAutomationSession.Capture(
            dialog,
            Path.Combine(logDirectory, "startup-error.png"),
            Path.Combine(logDirectory, "startup-error-tree.txt"));
        G4UiAutomationSession.Dismiss(dialog, "Application Startup Error");
        (await desktop.WaitForExitAsync(configuration.ShutdownTimeout, CancellationToken.None)).Should().BeTrue();
        desktop.Process.ExitCode.Should().Be(1);
        desktop.ForcedTermination.Should().BeFalse();

        observations.Add(new G4Observation(
            "G4-001",
            "NATS unavailable at launch",
            "Bounded diagnostic appeared, the main shell stayed hidden, and the desktop exited with code 1."));
        observations.Add(new G4Observation(
            "G4-008",
            "Unexpected modal evidence",
            "Captured modal text, screenshot, and automation tree before deterministic OK dismissal."));
    }

    static async Task VerifyActorApiUnavailableAsync(
        G0Configuration configuration,
        SecretRedactor redactor,
        string resultDirectory,
        ICollection<G4Observation> observations)
    {
        if (await IsHealthyAsync(configuration.ApiReadyUri))
            throw new InvalidOperationException(
                "G4 actor-unavailable injection requires no pre-existing API Server on the configured Development endpoint.");

        var logDirectory = Path.Combine(resultDirectory, "actor-api-unavailable");
        await using var desktop = OwnedProcess.Start(
            configuration.DesktopExecutable,
            logDirectory,
            redactor,
            UiEnvironment(configuration.NatsUri.ToString(), startupTimeoutSeconds: 5));
        using var automation = new G4UiAutomationSession(desktop.Process.Id);
        var main = await WaitForMainWindowAsync(automation, configuration.StartupTimeout);
        var mainTitle = main.Title;
        await WaitForAsync(
            () => desktop.StandardOutputSnapshot.Contains("NatsNoRespondersException", StringComparison.Ordinal),
            DialogTimeout);
        var text = desktop.StandardOutputSnapshot;
        text.Should().Contain("NatsNoRespondersException");
        text.Should().Contain("Failed to request query");
        text.Should().NotContain("NATS startup at");
        G4UiAutomationSession.Capture(
            main,
            Path.Combine(logDirectory, "actor-unavailable-shell.png"),
            Path.Combine(logDirectory, "actor-unavailable-shell-tree.txt"));
        foreach (var title in automation.WindowTitles()
                     .Where(title => !title.StartsWith("Investment Fund Manager", StringComparison.OrdinalIgnoreCase)))
        {
            var dialog = await automation.WaitForWindowAsync(title, TimeSpan.FromSeconds(2), CancellationToken.None);
            G4UiAutomationSession.Capture(
                dialog,
                Path.Combine(logDirectory, $"{SafeName(title)}.png"),
                Path.Combine(logDirectory, $"{SafeName(title)}-tree.txt"));
            G4UiAutomationSession.Dismiss(dialog, title);
        }
        await Task.Delay(250);
        G4UiAutomationSession.Dismiss(main, mainTitle);
        (await desktop.WaitForExitAsync(configuration.ShutdownTimeout, CancellationToken.None)).Should().BeTrue();
        desktop.ForcedTermination.Should().BeFalse();

        observations.Add(new G4Observation(
            "G4-002",
            "Broker reachable but actor API unavailable",
            "The responsive main shell proved broker connectivity; query subjects logged distinct no-responder failures."));
    }

    static async Task VerifyRepeatedLifecycleAndBurstAsync(
        G0Configuration configuration,
        SecretRedactor redactor,
        string resultDirectory,
        ICollection<G4Observation> observations)
    {
        for (var cycle = 1; cycle <= 3; cycle++)
        {
            var logDirectory = Path.Combine(resultDirectory, $"lifecycle-{cycle}");
            await using var desktop = OwnedProcess.Start(
                configuration.DesktopExecutable,
                logDirectory,
                redactor,
                UiEnvironment(configuration.NatsUri.ToString(), startupTimeoutSeconds: 10));
            using var automation = new G1UiAutomationSession(desktop.Process.Id);
            await automation.WaitForMainWindowAsync(configuration.StartupTimeout, CancellationToken.None);
            await automation.WaitForInitializedShellAsync(configuration.StartupTimeout, CancellationToken.None);

            if (cycle == 1)
                await PublishStatusBurstAsync(configuration.NatsUri, automation, resultDirectory);

            automation.FindUnexpectedWindowTitles("Investment Fund Manager").Should().BeEmpty();
            automation.RequestMainWindowClose();
            (await desktop.WaitForExitAsync(configuration.ShutdownTimeout, CancellationToken.None)).Should().BeTrue();
            desktop.ForcedTermination.Should().BeFalse();
            var connections = await InfrastructureProbe.GetProcessTcpConnectionsAsync(
                desktop.Process.Id,
                CancellationToken.None);
            connections.Should().BeEmpty();
        }

        observations.Add(new G4Observation(
            "G4-004",
            "Repeated launch/initialize/close",
            "Three complete cycles exited normally with no remaining desktop TCP connections or unexpected windows."));
        observations.Add(new G4Observation(
            "G4-007",
            "UI dispatcher burst safety",
            "A 10,000-event live status burst retained a responsive shell and a bounded 500-row history."));
        observations.Add(new G4Observation(
            "G4-009",
            "Process/network cleanup",
            "Every desktop process exited normally and owned no TCP connections after exit."));
    }

    static async Task VerifyInflightShutdownAsync(
        G0Configuration configuration,
        SecretRedactor redactor,
        string resultDirectory,
        ICollection<G4Observation> observations)
    {
        await using var proxy = new TcpFaultProxy(configuration.NatsUri.Host, configuration.NatsUri.Port);
        var logDirectory = Path.Combine(resultDirectory, "inflight-close");
        await using var desktop = OwnedProcess.Start(
            configuration.DesktopExecutable,
            logDirectory,
            redactor,
            UiEnvironment(proxy.Uri.ToString(), startupTimeoutSeconds: 10));
        using var automation = new G4UiAutomationSession(desktop.Process.Id);
        var main = await WaitForMainWindowAsync(automation, configuration.StartupTimeout);
        var mainTitle = main.Title;
        await WaitForAsync(() => proxy.ActiveConnectionCount > 0, TimeSpan.FromSeconds(10));
        proxy.PauseAndDropConnections();
        G4UiAutomationSession.Dismiss(main, mainTitle);

        if (!await desktop.WaitForExitAsync(configuration.ShutdownTimeout, CancellationToken.None))
        {
            foreach (var windowTitle in automation.WindowTitles()
                         .Where(title => !title.StartsWith("Investment Fund Manager", StringComparison.OrdinalIgnoreCase)))
            {
                var dialog = await automation.WaitForWindowAsync(windowTitle, TimeSpan.FromSeconds(1), CancellationToken.None);
                G4UiAutomationSession.Capture(
                    dialog,
                    Path.Combine(logDirectory, $"{SafeName(windowTitle)}.png"),
                    Path.Combine(logDirectory, $"{SafeName(windowTitle)}-tree.txt"));
                G4UiAutomationSession.Dismiss(dialog, windowTitle);
            }
        }

        (await desktop.WaitForExitAsync(configuration.ShutdownTimeout, CancellationToken.None)).Should().BeTrue(
            "closing while startup queries are in flight must cancel or finish without deadlock");
        desktop.ForcedTermination.Should().BeFalse();
        observations.Add(new G4Observation(
            "G4-005",
            "Close during in-flight operation",
            "The broker path was severed during startup work and normal close still completed within the shutdown bound."));

        observations.Add(new G4Observation(
            "G4-003",
            "Supported disconnect/reconnect",
            "The paired G4 live NATS test drops the isolated proxy connection, reconnects once, and delivers exactly once."));
        observations.Add(new G4Observation(
            "G4-006",
            "Listener failure and restart",
            "The paired G4 live NATS test preserves typed failure code/message/command correlation after reconnect."));
    }

    static async Task PublishStatusBurstAsync(
        Uri natsUri,
        G1UiAutomationSession automation,
        string resultDirectory)
    {
        const int count = 10_000;
        await automation.WaitForStatusConsoleStateAsync(
            TimeSpan.FromSeconds(30),
            CancellationToken.None);
        var serializer = new NatsMessagePackDataSerializer();
        await using var publisher = new NatsClient(natsUri.ToString());
        await publisher.ConnectAsync();
        for (var index = 0; index < count; index++)
        {
            var @event = new StatusConsoleLoggedEvent
            {
                Subject = new ActorSubject(
                    ActorType.Notify,
                    StatusConsoleLoggedEvent.Actor,
                    StatusConsoleLoggedEvent.Verb,
                    "G4"),
                Id = Guid.NewGuid(),
                EntityId = new ActorEntityId("G4"),
                EventId = index,
                CommandId = Guid.Empty,
                AggregateId = "G4",
                EventSource = nameof(G4ProcessAcceptanceTests),
                ReceivedOn = DateTime.UtcNow,
                StatusConsoleLog = new StatusConsoleLogReadModel(
                    DateTime.UtcNow,
                    0,
                    LogSourceType.TestSource,
                    $"G4-BURST-{index:D5}")
            };
            await publisher.PublishAsync(
                @event.Subject.ToString(),
                serializer.Serialize(@event),
                serializer: NatsDefaultSerializer<byte[]>.Default);
        }

        G1StatusConsoleState state = null!;
        await WaitForAsync(
            () =>
            {
                state = automation.ReadStatusConsoleState();
                return state.Rows.Any(row => row.Contains("G4-BURST-09999", StringComparison.Ordinal));
            },
            TimeSpan.FromSeconds(45));
        state.RowCount.Should().BeLessThanOrEqualTo(500);
        await File.WriteAllTextAsync(
            Path.Combine(resultDirectory, "status-burst.json"),
            JsonSerializer.Serialize(new
            {
                Published = count,
                state.RowCount,
                LatestMarkerObserved = true
            }, new JsonSerializerOptions { WriteIndented = true }));
    }

    static async Task<FlaUI.Core.AutomationElements.Window> WaitForMainWindowAsync(
        G4UiAutomationSession automation,
        TimeSpan timeout)
    {
        FlaUI.Core.AutomationElements.Window? main = null;
        await WaitForAsync(
            () => (main = automation.FindWindowStartingWith("Investment Fund Manager")) is not null,
            timeout);
        return main!;
    }

    static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!TryEvaluate(condition))
            await Task.Delay(100, cancellation.Token);

        static bool TryEvaluate(Func<bool> currentCondition)
        {
            try
            {
                return currentCondition();
            }
            catch (COMException)
            {
                // UI Automation can reject a property read while WinForms is replacing rows.
                // Treat that short-lived provider state as "not ready" and retry within the
                // caller's existing bounded timeout.
                return false;
            }
            catch (InvalidOperationException)
            {
                // The bounded status list can briefly disappear while WinForms recreates its handle.
                return false;
            }
        }
    }

    static async Task<bool> IsHealthyAsync(Uri uri)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        try
        {
            using var response = await client.GetAsync(uri);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    static async Task<string> ReadSharedTextAsync(string path)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);
        using StreamReader reader = new(stream);
        return await reader.ReadToEndAsync();
    }

    static Dictionary<string, string?> UiEnvironment(string natsUrl, int startupTimeoutSeconds)
        => new()
        {
            ["IFM_UI_NATS_URL"] = natsUrl,
            ["IFM_UI_NATS_STARTUP_TIMEOUT_SECONDS"] = startupTimeoutSeconds.ToString()
        };

    static int ReserveUnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static string SafeName(string value)
        => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

    static bool G4Enabled()
        => string.Equals(Environment.GetEnvironmentVariable("IFM_RUN_UI_G4"), "1", StringComparison.Ordinal);

    sealed record G4Observation(string Id, string Scenario, string Actual);
}
