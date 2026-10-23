using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.SystemAdmin.MarketClose;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.MarketData.Sessions;

/// <summary>Adapts domain close operations to existing actor commands and their durable receipts.</summary>
public sealed class MarketCloseOperations : IMarketCloseOperations
{
    const string HostId = "api-market-close";
    const string Operator = "ApiMarketClose";
    readonly IDbContextFactory databases;
    readonly IHostEnvironment environment;
    readonly IMarketDataFeedCommandApi feeds;
    readonly IStrategyPositionCommandApi positions;
    readonly ScheduledTaskCommandApi receipts;
    readonly ScheduledTaskQueryApi queries;
    readonly ScheduledTaskEventCompletion completion;
    readonly INatsJetStreamEndOfDayMaintenance maintenance;
    readonly ILogger<MarketCloseOperations> logger;
    readonly TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi? market;

    public MarketCloseOperations(IDbContextFactory databases, IHostEnvironment environment,
        IMarketDataFeedCommandApi feeds, IStrategyPositionCommandApi positions, IActorProducer producer,
        IConfiguration configuration, ILoggerFactory loggers, NatsConnectionManager connections,
        TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi? market = null)
    {
        this.databases = databases; this.environment = environment; this.feeds = feeds; this.positions = positions;
        this.market = market;
        logger = loggers.CreateLogger<MarketCloseOperations>();
        receipts = new(producer); queries = new(producer);
        var url = configuration["Nats:Url"] ?? "nats://localhost:4222";
        completion = new(new NatsEventListenerOptions { Url = url }, loggers.CreateLogger<ScheduledTaskEventCompletion>());
        var options = new NatsJetStreamEndOfDayMaintenanceOptions();
        configuration.GetSection("JetStreamEndOfDayPurge").Bind(options);
        options.Url = url;
        maintenance = new NatsJetStreamEndOfDayMaintenance(options, connections, loggers.CreateLogger<NatsJetStreamEndOfDayMaintenance>());
    }

    public async Task<DateOnly?> GetCompletedDateAsync(CancellationToken token)
        => await databases.ScheduledTaskDb.GetCompletedEndOfDayAsync(environment.EnvironmentName, token).ConfigureAwait(false);

    public async Task<DateTime?> GetPreviousCloseBoundaryAsync(DateOnly valueDate, CancellationToken token)
    {
        var runs = await databases.ScheduledTaskDb.GetRunsAsync(environment.EnvironmentName, HostId,
            ScheduledTaskIdentities.Catalog(environment.EnvironmentName, HostId), 500, token).ConfigureAwait(false);
        return runs.Where(run => run.ValueDate == valueDate)
            .OrderBy(run => run.IntendedFireTimeUtc).Select(run => (DateTime?)run.IntendedFireTimeUtc.UtcDateTime).FirstOrDefault();
    }

    public async Task<Guid> BeginAsync(DateOnly valueDate, DateTime closeBoundaryUtc, CancellationToken token)
    {
        var id = Guid.NewGuid();
        Require(await receipts.RequestScheduledTaskRunAsync(new()
        {
            CommandId = Guid.NewGuid(), OperationCommandId = id, EntityId = new(id),
            ScheduleId = ScheduledTaskIdentities.Catalog(environment.EnvironmentName, HostId), DefinitionRevision = 1,
            IntendedFireTimeUtc = new DateTimeOffset(closeBoundaryUtc), Manual = true, TaskKey = ScheduledTaskKeys.FuturesMarketClose,
            HostId = HostId, Environment = environment.EnvironmentName, Operator = Operator
        }, token).ConfigureAwait(false));
        // These receipts record backend execution. No schedule admission command or policy is invoked.
        Require(await receipts.RecordScheduledTaskRunAdmissionAsync(new()
        { CommandId = Guid.NewGuid(), OperationCommandId = id, EntityId = new(id), Accepted = true, Detail = "API-owned close execution; no scheduler admission policy.", Operator = Operator }, token).ConfigureAwait(false));
        Require(await receipts.RecordScheduledTaskRunStartedAsync(new()
        { CommandId = Guid.NewGuid(), OperationCommandId = id, EntityId = new(id), ProcessId = Environment.ProcessId, StartedAtUtc = DateTimeOffset.UtcNow, Operator = Operator }, token).ConfigureAwait(false));
        return id;
    }

    public async Task StopFeedsAsync(DateOnly valueDate, CancellationToken token)
    {
        var stopped = await completion.ExecuteAsync<MarketDataFeedStoppedCompleteEvent, MarketDataFeedStoppedFailEvent>(
            MarketDataFeedStoppedCompleteEvent.Actor, MarketDataFeedStoppedCompleteEvent.Verb, MarketDataFeedStoppedFailEvent.Verb,
            () => feeds.StopMarketDataFeedAsync(valueDate), token).ConfigureAwait(false);
        if (stopped.ValueDate != valueDate) throw new InvalidOperationException("Feed stop completed for another value date.");
        if (market is not null) await market.CloseTradeFeedRecoveryAsync(valueDate, token).ConfigureAwait(false);
    }

    public async Task RecordStageAsync(Guid runId, DateOnly valueDate, string stage, string detail, CancellationToken token)
        => Require(await receipts.RecordScheduledTaskRunStageAsync(new()
        { CommandId = Guid.NewGuid(), OperationCommandId = runId, EntityId = new(runId), ValueDate = valueDate, Stage = stage, Detail = detail, Operator = Operator }, token).ConfigureAwait(false));

    public async Task<ScheduledMarketPositionPage> LoadPositionsAsync(DateOnly valueDate, byte[]? cursor, CancellationToken token)
    {
        var page = await queries.GetScheduledMarketPositionsAsync(new() { ValueDate = valueDate, PageSize = 100, PagingState = cursor }, token).ConfigureAwait(false);
        if (!page.Success || page.Value is null) throw new InvalidOperationException("Open positions could not be loaded: " + page.ErrorMessage);
        return page.Value;
    }

    public async Task FinalizePositionAsync(StrategyPositionSnapshot position, DateOnly valueDate, DateTime boundary, CancellationToken token)
    {
        var result = await positions.EndOfDayAsync(position.Id, position.StrategyKind, valueDate, boundary, token).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException($"EOD source commitment failed for {position.Id.Format()}: {result.ErrorMessage}");
    }

    public async Task CompleteAsync(Guid runId, CancellationToken token)
        => Require(await receipts.CompleteScheduledTaskRunAsync(new()
        { CommandId = Guid.NewGuid(), OperationCommandId = runId, EntityId = new(runId), FinishedAtUtc = DateTimeOffset.UtcNow,
            ExitCode = 0, Stage = "MarketClosed", Detail = "Backend EOD and transport maintenance completed.", Operator = Operator }, token).ConfigureAwait(false));

    public async Task FailAsync(Guid runId, Exception error, CancellationToken token)
        => Require(await receipts.FailScheduledTaskRunAsync(new()
        { CommandId = Guid.NewGuid(), OperationCommandId = runId, EntityId = new(runId), FinishedAtUtc = DateTimeOffset.UtcNow,
            Stage = "MarketCloseFailed", Detail = error.Message, Operator = Operator }, token).ConfigureAwait(false));

    public async Task PurgeCompletedMessagesAsync(CancellationToken token)
    {
        var result = await maintenance.PurgeCompletedMessagesAsync(token).ConfigureAwait(false);
        logger.LogInformation("MarketClose TransportMaintenanceCompleted PurgedMessages={Messages} Streams={Streams}", result.PurgedMessages, result.Streams.Count);
    }

    static void Require(ServiceResult<GuidResult> result)
    {
        if (!result.Success) throw new InvalidOperationException("Market Close receipt failed: " + result.ErrorMessage);
    }
}
