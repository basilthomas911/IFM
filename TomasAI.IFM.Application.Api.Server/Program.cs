using Serilog;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;
using Microsoft.AspNetCore.OutputCaching;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

try
{
    var bootstrapTradeStrategyFamiliesOnly = args.Contains(
        "--bootstrap-trade-strategy-families-only",
        StringComparer.OrdinalIgnoreCase);
    var migrateStrategyCatalogOnly = args.Contains("--migrate-strategy-catalog-only", StringComparer.OrdinalIgnoreCase);
    var initializeSchemaOnly = args.Contains("--initialize-schema-only", StringComparer.OrdinalIgnoreCase);
    var refreshInstrumentDefinitionsOnly = args.Contains("--refresh-instrument-definitions-only", StringComparer.OrdinalIgnoreCase);
    var verifyStartupOnly = args.Contains("--verify-startup-only", StringComparer.OrdinalIgnoreCase);
    var builder = WebApplication.CreateBuilder(args);
    if (args.Contains("--publish-oct1-option-pricing-reference-only", StringComparer.OrdinalIgnoreCase) && !verifyStartupOnly)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await Oct1OptionPricingReferenceMaintenance.RunAsync(builder.Configuration, deadline.Token);
        return;
    }
    if (args.Contains("--publish-option-pricing-reference-only", StringComparer.OrdinalIgnoreCase) && !verifyStartupOnly)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await OptionPricingReferenceMaintenance.RunAsync(builder.Configuration, deadline.Token);
        return;
    }
    if (refreshInstrumentDefinitionsOnly && !verifyStartupOnly)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        await InstrumentDefinitionMaintenance.RunAsync(builder.Configuration, cancellation.Token);
        return;
    }
    builder.ConfigureApiServer(out var logger);
    EventLogQualification.Configure(builder, args);
    builder.Services.RegisterServices(builder.Configuration, logger);
    var app = builder.Build();
    var deploymentIdentity = app.Services.GetRequiredService<DeploymentIdentityMonitor>()
        .EnsureStartupValid();
    Log.Information("Deployment identity verified: {BuildId}", deploymentIdentity.BuildId);
    app.ConfigureRequestPipeline(logger);
    app.MapGet("/api/market-data/operations-health",
        (MarketDataOperationsHealthService health, LivePipelineMonitor monitor) => Results.Ok(LivePipelineEndpoints.OperationsSnapshot(health, monitor)))
        .CacheOutput(ApiOutputCachePolicies.OperationalSnapshot);
    app.MapGet("/api/actor-health",
        (IActorSupervisor supervisor, ISupervisorActorMetricsState metrics,
            ISupervisorIncidentStore incidents, ISupervisorOperationStore operations,
            ISupervisorHistoryStore history, ISupervisorActorMetricsPollingService poller,
            DateTime? fromUtc, DateTime? toUtc) =>
        {
            if (fromUtc > toUtc) return Results.BadRequest(new { Error = "fromUtc must be before toUtc." });
            var runtime = supervisor.RuntimeContext.CaptureSnapshot(fromUtc, toUtc);
            var from = (fromUtc ?? runtime.ObservedUtc.AddHours(-1)).ToUniversalTime();
            var to = (toUtc ?? runtime.ObservedUtc).ToUniversalTime();
            return Results.Ok(new
            {
                runtime.ObservedUtc,
                runtime.OverallStatus,
                runtime.ActorCount,
                runtime.RunningActorCount,
                runtime.ProcessingMailboxCount,
                runtime.QueuedMessageCount,
                runtime.Actors,
                runtime.Failures,
                runtime.Projectors,
                Workers = runtime.Workers ?? [],
                Collection = metrics.Current,
                Polling = poller.CaptureStatus(),
                Incidents = incidents.ActiveIncidents,
                Operations = operations.RecentOperations,
                History = history.Read(from, to)
            });
        })
        .CacheOutput(ApiOutputCachePolicies.OperationalSnapshot);
    app.MapLivePipelineHealth();
    if (verifyStartupOnly)
    {
        Console.WriteLine("IFM startup verification completed; no schemas, actors, feeds or HTTP listeners started.");
        // Run the real composition root/container checks, then exit before any schema,
        // seed, HTTP listener, hosted service, actor or feed startup. This takes precedence
        // over bootstrap mode so a verification request cannot accidentally write data.
        Log.Information("IFM startup verification completed; no schemas, actors, feeds or HTTP listeners started.");
        await app.DisposeAsync();
    }
    else if (initializeSchemaOnly)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        await app.Services.GetRequiredService<ApplicationSchemaInitializer>()
            .InitializeAsync(deadline.Token);
        Log.Information("Canonical schemas and catalogs initialized; no HTTP listener or actor runtime started.");
        await app.DisposeAsync();
    }
    else if (args.Contains("--backfill-risk-history-only", StringComparer.OrdinalIgnoreCase))
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.TradeDb.Schema.TradeSchemaDb>().CreateAllAsync();
        var cursorText = args.SingleOrDefault(x => x.StartsWith("--risk-after=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1];
        long cursor = cursorText is null ? 0 : long.Parse(cursorText, System.Globalization.CultureInfo.InvariantCulture);
        if (cursor < 0) throw new ArgumentException("Risk cursor must be nonnegative.");
        var recovery = app.Services.GetRequiredService<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.RiskObservationRecoveryService>();
        do { cursor = await recovery.ProjectPageAsync(cursor, false, deadline.Token); Console.WriteLine($"Risk history next cursor: {cursor}"); } while (cursor != 0);
        await app.DisposeAsync();
    }
    else if (migrateStrategyCatalogOnly)
    {
        await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.ConfigurationDb.Schema.ConfigurationSchemaDb>().CreateAllAsync();
        var migration = await app.Services.GetRequiredService<TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogMigration>().EnsureAsync(importLegacy: true);
        Console.WriteLine($"Verified catalog migration: {migration.StarterDefinitions} starter definitions, {migration.ImportedDeployments} legacy deployment imports, {migration.DeploymentsRequiringProductResolution} imports needing product resolution. No automatic publication or Fund permission changes.");
        await app.DisposeAsync();
    }
    else if (bootstrapTradeStrategyFamiliesOnly)
    {
        // Deliberately avoid HTTP binding and actor startup. This narrow process mode
        // lets deployment/startup qualification race independent initializers against
        // the same ReferenceDb and PostgreSQL sequence infrastructure.
        await app.Services.GetRequiredService<TradeStrategyFamilyBootstrapper>().EnsureV1Async();
        Log.Information("TradeStrategyFamily bootstrap-only process completed.");
    }
    else
    {
        if (EventLogQualification.Active is { } qualification)
        {
            await app.Services.GetRequiredService<ApplicationSchemaInitializer>()
                .InitializeAsync(CancellationToken.None);
            await qualification.InitializeCandidateAsync(CancellationToken.None);
        }
        var workflowOptions = app.Services.GetRequiredService<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor.IntrinsicTimeStrategyWorkflowOptions>();
        if (app.Environment.IsDevelopment()
            && workflowOptions.ProvisionDevelopmentMarketConditionAssessmentDefaults)
        {
            var regimeDefaults = await app.Services
                .GetRequiredService<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development.RegimeDiscoveryDefaultProvisioner>()
                .EnsureAsync(DateTime.UtcNow, "IFM Development startup");
            Log.Information(
                "Development Regime Discovery defaults ready: {ExistingProfiles} existing, {PublishedProfiles} published",
                regimeDefaults.ExistingProfiles,
                regimeDefaults.PublishedProfiles);
            var defaults = await app.Services
                .GetRequiredService<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development.MarketConditionAssessmentDefaultProvisioner>()
                .EnsureAsync(workflowOptions.MarketConditionAssessmentProfileId, DateTime.UtcNow, "IFM Development startup");
            Log.Information(
                "Development Market Condition Assessment defaults ready for {MarketProfileId}: {ExistingProfiles} existing, {PublishedProfiles} published, {ReplacedProfiles} replaced",
                workflowOptions.MarketConditionAssessmentProfileId,
                defaults.ExistingProfiles,
                defaults.PublishedProfiles,
                defaults.ReplacedProfiles);
        }
        app.EnableServerManagerStandardInputShutdown(args, logger);
        // Bind the HTTP endpoint and start hosted infrastructure before exposing
        // any NATS actor subscriptions. If Kestrel cannot bind (for example, a
        // duplicate API host owns the port), no actor can consume messages from
        // a service provider that is immediately torn down.
        await app.StartAsync();
        var managedActorLifecycle = app.Services.GetRequiredService<
            TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi.ISupervisorManagedActorLifecycle>();
        var supervisorBootstrap = app.Services.GetRequiredService<
            TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi.ISupervisorBootstrap>();
        var actorStartupSignal = app.Services.GetRequiredService<ActorRuntimeStartupSignal>();
        var actorsStarted = false;
        try
        {
            try
            {
                var orchestration = app.Services.GetRequiredService<StartupOrchestrationOptions>();
                using var actorStartupDeadline = CancellationTokenSource.CreateLinkedTokenSource(
                    app.Lifetime.ApplicationStopping);
                actorStartupDeadline.CancelAfter(orchestration.ActorStartupTimeout);
                await app.MapEventModelActorsAsync(logger, actorStartupDeadline.Token);
                actorStartupSignal.Complete();
            }
            catch (Exception exception)
            {
                actorStartupSignal.Fail(exception);
                throw;
            }
            actorsStarted = true;
            var developmentPortfolio = app.Services.GetRequiredService<DevelopmentTradingPortfolioOptions>();
            if (app.Environment.IsDevelopment()
                && developmentPortfolio.Enabled
                && string.IsNullOrWhiteSpace(app.Configuration["IFM_TEST_ACTOR_DOMAIN"]))
            {
                using var provisioningDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                await app.Services.GetRequiredService<DevelopmentTradingPortfolioProvisioner>()
                    .EnsureAsync(provisioningDeadline.Token);
            }
            await app.WaitForShutdownAsync();
        }
        finally
        {
            if (actorsStarted)
            {
                var shutdown = await managedActorLifecycle.ShutdownActorsAsync(CancellationToken.None);
                if (!shutdown.Succeeded)
                    logger.LogError(
                        "Supervisor actor shutdown {OperationId} ended with {Outcome} at {Stage}: {FailureReason}",
                        shutdown.OperationId, shutdown.Outcome, shutdown.Stage, shutdown.FailureReason);
                var supervisorShutdown = await supervisorBootstrap.StopSupervisorAsync(CancellationToken.None);
                if (!supervisorShutdown.Succeeded)
                    logger.LogError(
                        "Supervisor bootstrap shutdown {OperationId} ended with {Outcome} at {Stage}: {FailureReason}",
                        supervisorShutdown.OperationId, supervisorShutdown.Outcome,
                        supervisorShutdown.Stage, supervisorShutdown.FailureReason);
            }
        }
    }
}
catch (Exception ex)
{
    Environment.ExitCode = 1;
    if (args.Contains("--publish-option-pricing-reference-only", StringComparer.OrdinalIgnoreCase)
        || args.Contains("--publish-oct1-option-pricing-reference-only", StringComparer.OrdinalIgnoreCase))
    {
        var detail = ex.Message;
        var key = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (!string.IsNullOrEmpty(key)) detail = detail.Replace(key, "[redacted]", StringComparison.Ordinal);
        Console.Error.WriteLine("Option pricing reference publication failed: " + detail[..Math.Min(detail.Length, 2048)]);
    }
    if (args.Contains("--refresh-instrument-definitions-only", StringComparer.OrdinalIgnoreCase))
        Console.Error.WriteLine("Instrument definition refresh failed: " + ex.Message);
    Log.Fatal(ex, "IFM WebApiServer: startup failed");
    if (args.Contains("--verify-startup-only", StringComparer.OrdinalIgnoreCase))
        Console.Error.WriteLine("IFM startup verification failed: " + ex.Message);
    if (!args.Contains("--verify-startup-only", StringComparer.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IFM_TEST_NATS_URL")))
        throw;

}
finally
{
    Log.CloseAndFlush();
}
