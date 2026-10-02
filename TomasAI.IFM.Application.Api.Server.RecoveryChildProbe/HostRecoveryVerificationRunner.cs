using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

/// <summary>Runs fatal recovery through the real API entry point, only against a parent-owned test fixture.</summary>
internal static class HostRecoveryVerificationRunner
{
    /// <summary>Runs one isolated host scenario, optionally failing one production action boundary.</summary>
    /// <param name="mode">The child verification mode.</param>
    /// <param name="failedAction">The exact action to throw from in the action-failure matrix.</param>
    public static async Task RunAsync(string mode, string? failedAction = null)
    {
        if (mode == "host-action-failure"
            && (failedAction is null || !RecoveryActionFaultProbe.Sequence.Contains(failedAction)))
            throw new ArgumentException("The action-failure mode requires one of the 12 recovery actions.");
        RecoveryActionFaultProbe? faultProbe = null;
        RecoveryActionEvidenceLogger? evidenceLogger = null;
        var database = Environment.GetEnvironmentVariable("IFM_TEST_MARKET_DATA_CONNECTION") ?? "";
        var broker = Environment.GetEnvironmentVariable("IFM_CONFIGURED_TEST_NATS_URL") ?? "";
        if (!database.Contains("ifm_synthetic_", StringComparison.Ordinal)
            || !broker.StartsWith("nats://127.0.0.1:", StringComparison.Ordinal)
            || broker.EndsWith(":4222", StringComparison.Ordinal))
            throw new InvalidOperationException("Child recovery requires the isolated domain-actor fixture.");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TomasAI.IFM.sln")))
            root = root.Parent;
        if (root is null) throw new InvalidOperationException("Repository root not found.");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var workerPath = Path.Combine(root.FullName, "TomasAI.IFM.Application.MarketData.Worker",
            "bin", configuration, "net10.0", "TomasAI.IFM.Application.MarketData.Worker.dll");

        await using var owner = new KestrelWebApplicationFactory<ApiServerEntryPoint>();
        var factory = owner.WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("AppSettings:Databento:DeploymentProfile", "SyntheticCi")
            .UseSetting("AppSettings:Databento:DataSource", "Synthetic")
            .UseSetting("MarketDataRecovery:Stage3:Enabled", "true")
            .UseSetting("MarketDataRecovery:Stage3:WorkerAssemblyPath", workerPath)
            .UseSetting("MarketDataRecovery:HardRecovery:Pipeline:Enabled", "true")
            .UseSetting("MarketDataRecovery:HardRecovery:SoftGate:MaximumRounds", "1")
            .UseSetting("MarketDataRecovery:HardRecovery:Runtime:QualificationTimeout", "00:00:15")
            .UseSetting("MarketDataRecovery:HardRecovery:FatalShutdown:GracefulTimeout", "00:00:20")
            .UseSetting("MarketDataRecovery:HardRecovery:Pipeline:SystemConsoleTimeout", "00:00:00.200")
            .ConfigureTestServices(services =>
            {
                if (failedAction is null)
                {
                    // The existing infrastructure-outage scenarios retain all real probes.
                    services.AddSingleton<IRecoveryInfrastructureProbe, InjectedFailureProbe>();
                }
                else
                {
                    // Use the identical production composition, decorating only the selected boundary.
                    services.RemoveAll<IDatabentoRecoveryRequester>();
                    services.AddSingleton<IDatabentoRecoveryRequester>(provider =>
                    {
                        faultProbe = new RecoveryActionFaultProbe(
                            ApiDatabentoRecoveryComposition.CreateActions(provider),
                            provider.GetRequiredService<DatasetWorkerProcessRecoveryService>(),
                            provider.GetRequiredService<DatasetWorkerAdmissionRegistry>(), failedAction);
                        evidenceLogger = new(provider.GetRequiredService<ILogger<ApiDatabentoRecoveryPipeline>>());
                        return new ApiDatabentoRecoveryPipeline(faultProbe,
                            provider.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping,
                            provider.GetRequiredService<ApiDatabentoRecoveryPipelinePolicy>(), evidenceLogger);
                    });
                }
                services.RemoveAll<IStatusConsoleWriter>();
                services.AddSingleton<IStatusConsoleWriter>(provider => new ConsoleProbe(
                    ActivatorUtilities.CreateInstance<StatusConsoleWriter>(provider), mode));
                services.RemoveAll<IRecoveryFatalTelemetry>();
                services.AddSingleton<RecoveryFatalOpenTelemetry>();
                services.AddSingleton<IRecoveryFatalTelemetry>(provider =>
                    new TelemetryProbe(provider.GetRequiredService<RecoveryFatalOpenTelemetry>()));
            }));
        try
        {
            var services = factory.Services;
            using var client = factory.CreateClient();
            var health = await client.GetAsync("/api/actor-health");
            health.EnsureSuccessStatusCode();
            Console.WriteLine("REAL_KESTREL_ACTOR_HOST_READY=" + client.BaseAddress);
            var date = services.GetRequiredService<IFuturesMarketSessionAuthority>().Current.OperationalValueDate;
            var manifest = services.GetRequiredService<DatasetDesiredSubscriptionRegistry>().Set("GLBX.MDP3", date,
            [
                new DatabentoContractRegistration
                {
                    DomainContractId = "ES20261218", ProviderContractName = "ESZ6",
                    AssetTypeId = AssetTypeId.Futures, RootSymbol = "ES", Dataset = "GLBX.MDP3",
                    OnTheRun = true, Rollover = true
                }
            ]);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await services.GetRequiredService<ITickAggregationEventPublisher>().StartAsync(timeout.Token);
            var workers = services.GetRequiredService<DatasetWorkerProcessRecoveryService>();
            var original = await workers.StartOwnedAsync(
                services.GetRequiredService<DatabentoSupervisedWorkerOptions>().CreateStartRequest(manifest), timeout.Token);
            Console.WriteLine("OWNED_WORKER_PID=" + original.ProcessId);
            var requester = services.GetRequiredService<IDatabentoRecoveryRequester>();
            var result = await requester.HardResetRecoveryAsync(
                new(Guid.NewGuid(), date, original.GenerationId, "ChildVerification", mode), timeout.Token);
            if (result.Outcome != DatabentoRecoveryRequestOutcome.Unrecoverable
                || result.HardResult?.FailedStage != (failedAction ?? nameof(IApiDatabentoRecoveryActions.QualifyInfrastructureAsync))
                || !services.GetRequiredService<IApiFatalRecoveryShutdown>().IsRequested)
                throw new InvalidOperationException("Unexpected child recovery result: " + result);
            Console.WriteLine("TERMINAL_ACTION=" + result.HardResult.FailedStage);
            foreach (var worker in workers.Current) Console.WriteLine("OWNED_WORKER_PID=" + worker.ProcessId);
            if (faultProbe is not null && evidenceLogger is not null)
            {
                var subsequent = await requester.HardResetRecoveryAsync(
                    new(Guid.NewGuid(), date, original.GenerationId, "ChildVerification", "Must stay terminal"));
                Console.WriteLine("ACTION_FAILURE_EVIDENCE=" + JsonSerializer.Serialize(new
                {
                    Action = failedAction, result.CorrelationId, Outcome = result.Outcome.ToString(),
                    result.HardResult.FailedStage, result.HardResult.Detail,
                    ExceptionType = faultProbe.InjectedException?.GetType().Name,
                    ExceptionMessage = faultProbe.InjectedException?.Message,
                    Calls = faultProbe.Calls.ToArray(), Logs = evidenceLogger.Entries.ToArray(),
                    TerminalLatched = ReferenceEquals(result, subsequent),
                    faultProbe.CandidateCountAtFailure, faultProbe.CandidateAdmittedAtFailure
                }));
            }
        }
        finally { await factory.DisposeAsync(); }
        Console.WriteLine("REAL_API_HOST_DISPOSED");
        // The production fatal handler, not this probe, owns the nonzero exit code.
    }

    sealed class InjectedFailureProbe(DatasetWorkerProcessRecoveryService workers) : IRecoveryInfrastructureProbe
    {
        public string Name => "Isolated child injected infrastructure outage";
        public RecoveryInfrastructureKind Kind => RecoveryInfrastructureKind.PostgreSql;
        public Task<RecoveryProbeResult> CheckAsync(CancellationToken cancellationToken)
        {
            foreach (var worker in workers.Current) Console.WriteLine("OWNED_WORKER_PID=" + worker.ProcessId);
            return Task.FromResult(new RecoveryProbeResult(Name, Kind, false, "Deliberate child-only terminal fault"));
        }
    }

    sealed class ConsoleProbe(IStatusConsoleWriter inner, string mode) : IStatusConsoleWriter
    {
        public Task WriteConsoleAsync(LogSourceType source, string message) => inner.WriteConsoleAsync(source, message);
        public async Task WriteConsoleAsync(LogSourceType source, int code, string message, string dataType = "", string data = "")
        {
            if (code == 10042)
            {
                Console.WriteLine("SYSTEM_CONSOLE_ATTEMPT=" + message);
                if (mode == "host-console-throw") throw new IOException("Injected console failure");
                if (mode == "host-console-hang")
                    await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            }
            await inner.WriteConsoleAsync(source, code, message, dataType, data);
            if (code == 10042) Console.WriteLine("SYSTEM_CONSOLE_SENT");
        }
    }

    sealed class TelemetryProbe(RecoveryFatalOpenTelemetry inner) : IRecoveryFatalTelemetry
    {
        public async Task EmitAsync(FatalRecoveryReport report, CancellationToken token)
        {
            await inner.EmitAsync(report, token);
            Console.WriteLine("REAL_OTEL_LOCAL_SUBMISSION=" + report.Request.CorrelationId);
        }
    }
}
