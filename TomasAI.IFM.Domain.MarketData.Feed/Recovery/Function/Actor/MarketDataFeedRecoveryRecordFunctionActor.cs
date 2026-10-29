using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function.State;
namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function.Actor;
/// <summary>Acknowledges only after the incident and this function's completion have committed.</summary>
public sealed class MarketDataFeedRecoveryRecordFunctionActor(IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor> context,
    IEventSourceFunctionStateRepository<MarketDataFeedRecoveryRecordState, RecordMarketDataFeedRecoveryCommand> repository,
    IActorService actors, ILogger<MarketDataFeedRecoveryRecordFunctionActor> logger)
    : BaseEventSourceFunctionActor<MarketDataFeedRecoveryRecordFunctionActor, RecordMarketDataFeedRecoveryCommand,
      MarketDataFeedRecoveryId, MarketDataFeedRecoveryId, MarketDataFeedRecoveryRecordState,
      MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>(context, repository, null, logger)
{
    public const string ActorName = RecordMarketDataFeedRecoveryCommand.Actor;
    protected override RecordMarketDataFeedRecoveryCommand ParseMessage(IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor> context, IActorMessage message)
    {
        var request=message.AsCommand<RecordMarketDataFeedRecoveryCommand>()! with {ReceivedAtUtc=DateTime.UtcNow};
        logger.LogDebug("Recovery audit record received {RecordId} {AttemptId} {RequestSubject} {ReceivedAtUtc} {RequestDeadlineUtc}",
            request.EntityId.Value,request.AttemptId,request.Subject.ToString(),request.ReceivedAtUtc,request.RequestDeadlineUtc);
        return request;
    }
    protected override FunctionExecutionPolicy ResolveExecutionPolicy(RecordMarketDataFeedRecoveryCommand request, FunctionFailureStage stage) =>
        new(TimeProvider.System, request.RequestDeadlineUtc, FunctionCompletionMode.EventThenProjection);
    protected override ValueTask ValidateAsync(IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor> context, ActorThreadId thread, RecordMarketDataFeedRecoveryCommand request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var record = RecoveryRecord.Parse(request.Payload, request.Digest);
        if (request.EntityId.Value != record.RecordId || request.AttemptId == Guid.Empty || request.CommandId == Guid.Empty ||
            request.RequestDeadlineUtc.Kind != DateTimeKind.Utc || request.AttemptStartedAtUtc.Kind != DateTimeKind.Utc ||
            request.RequestDeadlineUtc <= request.AttemptStartedAtUtc ||
            request.Subject != new ActorSubject(ActorType.Function, ActorName, "Record", request.EntityId.Format()))
            throw new InvalidDataException("Invalid audit delivery identity/deadline/route.");
        return ValueTask.CompletedTask;
    }
    protected override async ValueTask<FunctionResult<MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>> ExecuteFunctionAsync(
        IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor> context, MarketDataFeedRecoveryRecordState state,
        RecordMarketDataFeedRecoveryCommand request, CancellationToken token)
    {
        var record = RecoveryRecord.Parse(request.Payload, request.Digest);
        var operation = new MarketDataFeedRecoveryId(record.OperationId);
        var append = new AppendMarketDataFeedRecoveryCommand { CommandId = request.CommandId,
            EntityId = operation, Subject = new(ActorType.Command, AppendMarketDataFeedRecoveryCommand.Actor, "Record", operation.Format()),
            Payload = request.Payload, Digest = request.Digest, AttemptId = request.AttemptId,
            AttemptStartedAtUtc = request.AttemptStartedAtUtc, RequestDeadlineUtc = request.RequestDeadlineUtc, ReceivedAtUtc=request.ReceivedAtUtc };
        var saved = await actors.RequestAsync<AppendMarketDataFeedRecoveryCommand, MarketDataFeedRecoveryId>(append, token).ConfigureAwait(false);
        if (!saved.Success) throw new InvalidOperationException(saved.ErrorMessage);
        logger.LogDebug("Recovery fact storage acknowledged {RecordId} {OperationId} {Sequence} {Transition} {OccurredAtUtc} {ReceivedAtUtc} {CompletionScope} {MonitoringReady}",
            record.RecordId, record.OperationId, record.Sequence, record.Kind, record.OccurredAtUtc, DateTime.UtcNow, record.CompletionScope, record.MonitoringReady);
        return FunctionResult<MarketDataFeedRecoveryRecordStoredEvent, MarketDataFeedRecoveryRecordStorageFailedEvent>.Complete(new()
        {
            EntityId = request.EntityId, RecordId = record.RecordId, OperationId = record.OperationId, Digest = request.Digest,
            Sequence = record.Sequence, CommandId = request.CommandId, Id = record.RecordId, ReceivedOn = DateTime.UtcNow,
            Subject = new(ActorType.Event, "MarketDataFeedRecoveryEvent", "RecordStored", request.EntityId.Format())
        });
    }
    protected override MarketDataFeedRecoveryRecordStorageFailedEvent CreateConflictFailedEvent(RecordMarketDataFeedRecoveryCommand request) =>
        CreateFailedEvent(request, new InvalidDataException("Recovery RecordId content conflict."), FunctionFailureStage.Execution);
    protected override MarketDataFeedRecoveryRecordStorageFailedEvent CreateFailedEvent(RecordMarketDataFeedRecoveryCommand? request, Exception exception, FunctionFailureStage stage) => new()
    {
        EntityId = request?.EntityId ?? new(Guid.Empty), CommandId = request?.CommandId ?? Guid.Empty,
        ErrorDate = DateTime.UtcNow, ReceivedOn = DateTime.UtcNow, ErrorMessage = exception.Message, ErrorData = exception.ToString(),
        Subject = new(ActorType.Event, "MarketDataFeedRecoveryEvent", "RecordStorageFailed", request?.EntityId.Format() ?? "unknown")
    };
}
