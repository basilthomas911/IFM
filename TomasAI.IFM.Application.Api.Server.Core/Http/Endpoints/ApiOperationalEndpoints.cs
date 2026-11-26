using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Api.Server.Core.Http.OutputCaching;
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
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;

public static class ApiOperationalEndpoints
{
    public static void MapOperationalEndpoints(WebApplication app)
    {
        app.MapSessionCaptureEndpoints();
        // Copied native readiness evidence: this diagnostic never acquires leases or initiates recovery.
        app.MapGet("/api/market-data/options-health", async (Observability.HealthChecks.BackendDiagnosticReads diagnostics, CancellationToken token) =>
        {
            var snapshot = await diagnostics.SnapshotAsync(token).ConfigureAwait(false);
            return Results.Ok(new { datasets = snapshot.Status.Datasets.Where(d => d.Role == "Options"), workload = snapshot.NativeOptionWorkload });
        });
        app.MapGet("/api/market-data/recovery-audit-health",async (Observability.HealthChecks.BackendDiagnosticReads diagnostics,CancellationToken token) =>
            Results.Ok((await diagnostics.StatusAsync(token).ConfigureAwait(false)).RecoveryAudit));
        app.MapGet("/api/market-data/recovery-log", async (IDbContextFactory databases, DateOnly valueDate,
            string? dataset, Guid? operationId, int? limit, CancellationToken token) =>
        {
            if (valueDate==default || limit is <1 or >4096 || dataset?.Length>64)
                return Results.BadRequest(new { Error="A ValueDate, optional dataset/operation and limit 1..4096 are required." });
            var records=await databases.MarketDataServiceDb.DbReader.ReadRecoveryLogAsync(
                valueDate,dataset,operationId,limit??256,token).ConfigureAwait(false);
            return Results.Ok(records.Select(record=>System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(record)));
        });
        app.MapGet("/api/market-data/operations-health",
        async (MarketDataOperationsHealthService health, LivePipelineMonitor monitor, HttpContext context) =>
        {
            // A view/Refresh captures current passive counters; it never starts or resets a feed.
            if (context.RequestServices.GetService<MarketDataBackendHealthObserver>() is { } observer)
                await observer.ObserveOnceAsync(context.RequestAborted).ConfigureAwait(false);
            // Freshness uses the API operational clock, never the last market-session transition.
            context.Response.Headers["X-IFM-Server-Utc"] = (context.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            return Results.Ok(LivePipelineEndpoints.OperationsSnapshot(health, monitor));
        });
        app.MapGet("/api/actor-health",
        (IActorSupervisor supervisor, ISupervisorActorMetricsState metrics,
        ISupervisorIncidentStore incidents, ISupervisorOperationStore operations,
        ISupervisorHistoryStore history, ISupervisorActorMetricsPollingService poller,
        ISupervisorHealthManager healthManager, SupervisorHealthActionOptions actionOptions,
        IConfiguration configuration,
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
                History = history.Read(from, to),
                SupervisorAuthorityState = healthManager.AuthorityState,
                ManualMutationEnabled = configuration.GetSection("Supervisor:AllowedOperators").Get<string[]>() is { Length: > 0 },
                actionOptions.AutomaticMutationEnabled
            });
        });
        // The live views share a one-second capture without caching serialized bodies.
        app.MapGet("/api/actor-health/realtime-mailboxes", (
            TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes.DurableRealtimeMailboxHub hub, int? offset, int? limit) => Results.Ok(new
            {
                hub.Enabled, hub.IsRunning, hub.CurrentFailure, hub.MailboxCount,
                hub.PendingSourceCount, hub.PendingSourceBytes, hub.ReceiptBytes,
                hub.BrokerStoredBytes, hub.BrokerStoredMessages,
                Streams = hub.StreamSnapshots, Mailboxes = hub.GetMailboxSnapshots(offset ?? 0, limit ?? 40)
            }));
        app.MapGet("/api/actor-health/summary", (Observability.HealthChecks.ActorHealthSnapshotReader reader) =>
            Results.Ok(ActorHealthReadViews.Summarize(reader.Capture())));
        app.MapGet("/api/actor-health/actors", (Observability.HealthChecks.ActorHealthSnapshotReader reader, int? offset,
            int? limit, bool? includeMailboxes) =>
        {
            try { return Results.Ok(ActorHealthReadViews.Actors(reader.Capture(), offset ?? 0, limit ?? 64, includeMailboxes ?? false)); }
            catch (ArgumentOutOfRangeException error) { return Results.BadRequest(new { Error = error.Message }); }
        });
        app.MapGet("/api/actor-health/diagnostics", (Observability.HealthChecks.ActorHealthSnapshotReader reader,
            IActorSupervisor supervisor, ISupervisorActorMetricsState metrics, ISupervisorIncidentStore incidents,
            ISupervisorOperationStore operations, ISupervisorActorMetricsPollingService poller,
            ISupervisorHealthManager healthManager, SupervisorHealthActionOptions actionOptions,
            IConfiguration configuration, DateTime? fromUtc) =>
        {
            var runtime = reader.Capture();
            var from = (fromUtc ?? runtime.ObservedUtc.AddHours(-1)).ToUniversalTime();
            if (from > runtime.ObservedUtc) return Results.BadRequest(new { Error = "fromUtc must be before the observation time." });
            return Results.Ok(new
            {
                runtime.ObservedUtc,
                Failures = fromUtc is null ? runtime.Failures : supervisor.RuntimeContext.ReadFailures(from, runtime.ObservedUtc),
                runtime.Projectors,
                Workers = runtime.Workers ?? [],
                Collection = metrics.Current,
                Polling = poller.CaptureStatus(),
                Incidents = incidents.ActiveIncidents,
                Operations = operations.RecentOperations,
                SupervisorAuthorityState = healthManager.AuthorityState,
                ManualMutationEnabled = configuration.GetSection("Supervisor:AllowedOperators").Get<string[]>() is { Length: > 0 },
                actionOptions.AutomaticMutationEnabled
            });
        });
        app.MapGet("/api/actor-health/actors/{actorType}/{name}", (Observability.HealthChecks.ActorHealthSnapshotReader reader, ActorType actorType, string name) =>
        {
            var actor = reader.Capture().Actors.FirstOrDefault(actor => actor.ActorId.ActorType == actorType && actor.ActorId.Name == name);
            return actor is null ? Results.NotFound() : Results.Ok(actor);
        });
        app.MapGet("/api/actor-health/projectors", (Observability.HealthChecks.ActorHealthSnapshotReader reader) => Results.Ok(reader.Capture().Projectors));
        app.MapGet("/api/actor-health/workers", (Observability.HealthChecks.ActorHealthSnapshotReader reader) => Results.Ok(reader.Capture().Workers ?? []));
        app.MapGet("/api/actor-health/history", (ISupervisorHistoryStore history, DateTime fromUtc, DateTime toUtc, int? offset, int? limit) =>
        {
            if (fromUtc > toUtc)
                return Results.BadRequest(new { Error = "History requires an ordered range." });
            try { ActorHealthReadViews.ValidatePage(offset ?? 0, limit ?? 64); }
            catch (ArgumentOutOfRangeException error) { return Results.BadRequest(new { Error = error.Message }); }
            var rows = history.Read(fromUtc.ToUniversalTime(), toUtc.ToUniversalTime(), 10080);
            return Results.Ok(new { Offset = offset ?? 0, Total = rows.Count, Rows = rows.Skip(offset ?? 0).Take(limit ?? 64).ToArray() });
        });
        app.MapLivePipelineHealth();
        app.MapMarketClose();

    }
}
