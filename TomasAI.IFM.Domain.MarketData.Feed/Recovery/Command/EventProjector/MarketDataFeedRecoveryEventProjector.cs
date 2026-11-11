using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.EventProjector;
/// <summary>Durable process/replay descriptors rebuild immutable audit rows after database outages.</summary>
public sealed class MarketDataFeedRecoveryEventProjector : ConventionalEventProjector<MarketDataFeedRecoveryCommandActor>
{
    public MarketDataFeedRecoveryEventProjector(IDurableReplayQueue queue, IEventSourceActorDbContext events,
        IDbContextFactory databases, ILogger<MarketDataFeedRecoveryEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions=null)
        : base(queue,events,logger,AuditOptions(reliabilityOptions))
    {
        ProjectionDescriptors = [
            DescribeNotification<MarketDataFeedRecoveryStartedEvent, MarketDataFeedRecoveryId>((fact,execution)=>ProjectAsync(fact,execution.CancellationToken)).WithReadModelRebuild((source,token)=>RebuildAsync((MarketDataFeedRecoveryFact)source, token)),
            DescribeNotification<MarketDataFeedDatasetResetStartedEvent, MarketDataFeedRecoveryId>((fact,execution)=>ProjectAsync(fact,execution.CancellationToken)).WithReadModelRebuild((source,token)=>RebuildAsync((MarketDataFeedRecoveryFact)source, token)),
            DescribeNotification<MarketDataFeedDatasetResetCompletedEvent, MarketDataFeedRecoveryId>((fact,execution)=>ProjectAsync(fact,execution.CancellationToken)).WithReadModelRebuild((source,token)=>RebuildAsync((MarketDataFeedRecoveryFact)source, token)),
            DescribeNotification<MarketDataFeedConnectionRestoredEvent, MarketDataFeedRecoveryId>((fact,execution)=>ProjectAsync(fact,execution.CancellationToken)).WithReadModelRebuild((source,token)=>RebuildAsync((MarketDataFeedRecoveryFact)source, token)),
            DescribeNotification<MarketDataFeedRecoveryCompletedEvent, MarketDataFeedRecoveryId>((fact,execution)=>ProjectAsync(fact,execution.CancellationToken)).WithReadModelRebuild((source,token)=>RebuildAsync((MarketDataFeedRecoveryFact)source, token)),
            DescribeNotification<MarketDataFeedRecoveryFailedEvent, MarketDataFeedRecoveryId>((fact,execution)=>ProjectAsync(fact,execution.CancellationToken)).WithReadModelRebuild((source,token)=>RebuildAsync((MarketDataFeedRecoveryFact)source, token)),
            DescribeNotification<MarketDataFeedMonitoringRestoredEvent, MarketDataFeedRecoveryId>((fact,execution)=>ProjectAsync(fact,execution.CancellationToken)).WithReadModelRebuild((source,token)=>RebuildAsync((MarketDataFeedRecoveryFact)source, token)),
            DescribeNotification<MarketDataFeedRecoveryInterruptedEvent, MarketDataFeedRecoveryId>((fact,execution)=>ProjectAsync(fact,execution.CancellationToken)).WithReadModelRebuild((source,token)=>RebuildAsync((MarketDataFeedRecoveryFact)source, token))
        ];
        Task RebuildAsync(MarketDataFeedRecoveryFact source, CancellationToken token)
            => databases.MarketDataServiceDb.DbWriter.AppendRecoveryLogAsync(
                ToProjectionRow(source, RecoveryRecord.Parse(source.Payload, source.Digest)), token);

        async Task ProjectAsync(MarketDataFeedRecoveryFact source,CancellationToken token)
        {
            var record = RecoveryRecord.Parse(source.Payload, source.Digest);
            try
            {
                await databases.MarketDataServiceDb.DbWriter.AppendRecoveryLogAsync(ToProjectionRow(source, record), token).ConfigureAwait(false);
                logger.LogInformation("Recovery projection completed {RecordId} {OperationId} {Transition} {OccurredAtUtc}",record.RecordId,record.OperationId,record.Kind,record.OccurredAtUtc);
            }
            catch (Exception exception)
            {
                logger.LogError(exception,"Recovery projection failed {RecordId} {OperationId}",record.RecordId,record.OperationId);
                throw;
            }
        }
    }
    static RecoveryLogRow ToProjectionRow(MarketDataFeedRecoveryFact source, RecoveryRecord record)
        => new(record.RecordId, record.OperationId, record.Sequence, record.ValueDate, record.Role, record.Dataset,
            record.Kind, record.OccurredAtUtc, source.ReceivedOn, source.Digest, source.Payload, source.EventId);

    // Audit facts require durable replay even in deployments where unrelated
    // read-model projectors are deliberately transient. Do not change their options.
    static EventProjectorReliabilityOptions AuditOptions(EventProjectorReliabilityOptions? options) =>
        (options ?? new()) with { BoundedRecoveryEnabled=true,FencedExecutionEnabled=true,DurableProjectorAllowlist=null };
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors { get; }
}
