using TomasAI.IFM.Application.Api.Server.Core.Actors.Registration;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.ServerManager;
using TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;
using TomasAI.IFM.Application.Api.Server.Core.Development.Provisioning;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Application.Api.Server.Core.Messaging.JetStream;
using TomasAI.IFM.Application.Api.Server.Core.Recovery.Shutdown;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Actors;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Schema;
using Serilog;
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
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting;

public sealed partial class ApiServerLifecycle
{
    static async Task RunServerAsync(WebApplication app, string[] args, Microsoft.Extensions.Logging.ILogger logger, IApiFatalRecoveryShutdown? fatalRecoveryShutdown)
    {
        DevelopmentExecutionProjectionRecovery.Map(app);
        using var redisDeadline = CancellationTokenSource.CreateLinkedTokenSource(app.Lifetime.ApplicationStopping);
        redisDeadline.CancelAfter(TimeSpan.FromSeconds(30));
        var redisReady = app.Services.GetRequiredService<TomasAI.IFM.Application.Api.Server.Core.HostedServices.RedisConnectionStartup>()
            .GetAsync(redisDeadline.Token);
        if (EventLogQualification.Active is { } qualification)
        {
            _ = await redisReady.ConfigureAwait(false);
            await app.Services.GetRequiredService<ApplicationSchemaInitializer>()
                .InitializeAsync(CancellationToken.None);
            await qualification.InitializeCandidateAsync(CancellationToken.None);
        }

        var preflightFailures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var configurationReady = false;
        await Task.WhenAll(
            RunPreflightAsync("Redis connection", async () => { _ = await redisReady.ConfigureAwait(false); }),
            RunPreflightAsync("Aggregate checkpoint schema", async () =>
            {
                using var schemaDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await app.Services.GetRequiredService<EventSourceSchemaDb>()
                    .EnsureAggregateCheckpointSchemaAsync(schemaDeadline.Token);
            }),
            RunPreflightAsync("Broker order projection", async () =>
            {
                _ = await redisReady.ConfigureAwait(false);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(app.Lifetime.ApplicationStopping);
                deadline.CancelAfter(TimeSpan.FromSeconds(30));
                var container = app.Services.GetRequiredService<SimpleInjector.Container>();
                var store = container.GetInstance<IBrokerOrderReadStore>();
                var rebuilder = new BrokerOrderReadModelRebuilder(app.Services.GetRequiredService<IPostgresEventTransaction>());
                var restored = await rebuilder.RebuildAsync(store, deadline.Token);
                logger.LogInformation("Restored {Count} broker-order definitions from committed snapshots.", restored);
            }),
            RunPreflightAsync("Recovery schema", async () =>
            {
                // Normal startup does not run full deployment schema initialization. Recovery readers
                // must have their additive operational table before any hosted services start.
                using (var recoverySchemaDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                    await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.TradeDb.Schema.TradeSchemaDb>()
                        .CreateAsync(["live_feed_recovery"], recoverySchemaDeadline.Token);
            }),
            RunPreflightAsync("Scheduler schema", async () =>
            {
                if (app.Services.GetService<TomasAI.IFM.Application.Storage.ScheduledTaskDb.Schema.ScheduledTaskSchemaDb>() is { } scheduledTaskSchema)
                {
                    using var scheduledTaskSchemaDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    await scheduledTaskSchema.CreateAllAsync().WaitAsync(scheduledTaskSchemaDeadline.Token);
                }
            }),
            RunPreflightAsync("Option cache schema", async () =>
            {
                // These additive cache tables must exist before background configuration readers start.
                using (var cacheSchemaDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                await app.Services.GetRequiredService<MarketDataSchemaDb>().CreateAsync(
                ["strategy_option_chain_parameter_version", "strategy_option_chain_parameter_current"], cacheSchemaDeadline.Token);
            }),
            RunPreflightAsync("Historical analytics diagnostics schema", async () =>
            {
                using var schemaDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await app.Services.GetRequiredService<MarketDataSchemaDb>().CreateAsync(
                    ["historical_data_loader", "historical_analytics_attempt_projection"], schemaDeadline.Token);
            }),
            RunPreflightAsync("Indicator seed coverage schema", async () =>
            {
                using var schemaDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await app.Services.GetRequiredService<MarketDataSchemaDb>().CreateAsync(
                    ["indicator_seed_coverage"], schemaDeadline.Token);
            }),
            RunPreflightAsync("Configuration schema and parameter migration", async () =>
            {
                await new TomasAI.IFM.Application.Storage.ConfigurationDb.Schema.ConfigurationSchemaDb(
                    app.Services.GetRequiredService<TomasAI.IFM.Shared.Storage.IDbConnectionSettings>(),
                    app.Services.GetRequiredService<ILogger<TomasAI.IFM.Framework.Storage.DbProvider>>()).CreateAllAsync();
                _ = await redisReady.ConfigureAwait(false);
                var migratedParameters = await app.Services.GetRequiredService<TomasAI.IFM.Domain.Reference.ParameterSets.Migration.ParameterCatalogMigrationService>()
                    .ExecuteAsync(app.Lifetime.ApplicationStopping);
                logger.LogInformation("Typed parameter catalog migration completed: {SourceFacts} source facts replayed", migratedParameters);
                configurationReady = true;
            })
        ).ConfigureAwait(false);
        if (configurationReady)
            await RunPreflightAsync("Development strategy defaults", async () =>
            {
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
            });
        else
            logger.LogWarning("API startup step Development strategy defaults blocked by configuration initialization.");

        if (!preflightFailures.IsEmpty)
        {
            logger.LogError("API startup blocked HTTP and actor startup because prerequisite checks failed.");
            throw new AggregateException("API startup prerequisites failed.", preflightFailures);
        }

        async Task RunPreflightAsync(string step, Func<Task> operation)
        {
            try
            {
                await operation().ConfigureAwait(false);
                logger.LogInformation("API startup step {Step} completed.", step);
            }
            catch (Exception error) when (!app.Lifetime.ApplicationStopping.IsCancellationRequested)
            {
                logger.LogError(error, "API startup step {Step} failed.", step);
                preflightFailures.Enqueue(new InvalidOperationException($"Startup step '{step}' failed: {error.Message}", error));
            }
        }
        app.EnableServerManagerStandardInputShutdown(args, logger);
        // Bind the HTTP endpoint and start hosted infrastructure before exposing
        // any NATS actor subscriptions. If Kestrel cannot bind (for example, a
        // duplicate API host owns the port), no actor can consume messages from
        // a service provider that is immediately torn down.
        await app.StartAsync();
        await new NatsJetStreamStartupPurge(
        app.Services.GetRequiredService<INatsJetStreamConsumerOptions>().Url,
        app.Services.GetRequiredService<NatsConnectionManager>(),
        app.Services.GetRequiredService<ILogger<NatsJetStreamStartupPurge>>())
        .PurgeAsync(
        static name => name == "EventStream"
        || FinancialJetStreamPolicy.ShouldPurgeProjectorStream(name),
        app.Lifetime.ApplicationStopping);
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
                var provisioner = app.Services.GetRequiredService<DevelopmentTradingPortfolioProvisioner>();
                await provisioner.EnsureAsync(provisioningDeadline.Token);
                if (args.Contains("--verify-development-provisioning", StringComparer.Ordinal))
                {
                    var seeder = app.Services.GetServices<IHostedService>().OfType<DevelopmentOptionChainParameterSeeder>().Single();
                    if (!await seeder.EnsureAsync(provisioningDeadline.Token))
                    throw new InvalidOperationException("Development option-chain defaults were not published.");
                    var expected = DevelopmentTradingPortfolioDefaults.OptionCacheProfiles();
                    var database = app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.IDbContextFactory>();
                    var projectionDeadline = DateTime.UtcNow.AddSeconds(15);
                    while (true)
                    {
                        var persisted = await database.MarketDataDb.ReadPublishedAsync("Development", provisioningDeadline.Token);
                        if (expected.All(profile => persisted.Any(row => row.ParameterSetId == profile.ParameterSetId && row.Hash() == profile.Hash()))) break;
                        if (DateTime.UtcNow >= projectionDeadline) throw new InvalidOperationException("Development option-chain default projection did not complete.");
                        await Task.Delay(100, provisioningDeadline.Token);
                    }
                    Log.Information("Verified three authoritative option-chain profiles in ScyllaDB.");
                    var repeated = await provisioner.EnsureAsync(provisioningDeadline.Token);
                    Log.Information("Development provisioning repeated successfully: {Report}",
                    System.Text.Json.JsonSerializer.Serialize(repeated));
                    app.Lifetime.StopApplication();
                }
            }
            await app.WaitForShutdownAsync();
        }
        finally
        {
            var shutdownComplete = true;
            if (actorsStarted)
            {
                var shutdown = await managedActorLifecycle.ShutdownActorsAsync(CancellationToken.None);
                if (!shutdown.Succeeded)
                {
                    shutdownComplete = false;
                    logger.LogError(
                    "Supervisor actor shutdown {OperationId} ended with {Outcome} at {Stage}: {FailureReason}",
                    shutdown.OperationId, shutdown.Outcome, shutdown.Stage, shutdown.FailureReason);
                }
                var supervisorShutdown = await supervisorBootstrap.StopSupervisorAsync(CancellationToken.None);
                if (!supervisorShutdown.Succeeded)
                {
                    shutdownComplete = false;
                    logger.LogError(
                    "Supervisor bootstrap shutdown {OperationId} ended with {Outcome} at {Stage}: {FailureReason}",
                    supervisorShutdown.OperationId, supervisorShutdown.Outcome,
                    supervisorShutdown.Stage, supervisorShutdown.FailureReason);
                }
            }
            if (fatalRecoveryShutdown?.IsRequested == true)
            {
                if (shutdownComplete) fatalRecoveryShutdown.MarkGracefulShutdownComplete();
                else fatalRecoveryShutdown.FailAndExit(
                new InvalidOperationException("Supervisor actor shutdown did not complete after fatal recovery."));
            }
        }
    }
}
