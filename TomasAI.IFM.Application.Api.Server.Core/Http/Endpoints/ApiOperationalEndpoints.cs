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
        app.MapGet("/api/market-data/operations-health",
        (MarketDataOperationsHealthService health, LivePipelineMonitor monitor) => Results.Ok(LivePipelineEndpoints.OperationsSnapshot(health, monitor)))
        .CacheOutput(ApiOutputCachePolicies.OperationalSnapshot);
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
        })
        .CacheOutput(ApiOutputCachePolicies.OperationalSnapshot);
        app.MapLivePipelineHealth();

    }
}
