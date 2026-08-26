using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;

/// <summary>
/// Reconstructs the authoritative Market Outlook working state exclusively from committed domain events.
/// </summary>
public sealed class MarketOutlookSnapshotCommandState
    : BaseEventSourceActorState<MarketOutlookSnapshotCommandState>,
      IEventSourceActorState<MarketOutlookSnapshotCommandState>
{
    MarketOutlookWorkingStateReadModel _workingState = new();

    /// <summary>Gets or sets the actor thread associated with this aggregate instance.</summary>
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>Applies a supported Market Outlook domain event to the aggregate.</summary>
    protected override bool Apply(IEvent domainEvent)
        => domainEvent switch
        {
            MarketOutlookComponentObservedEvent observed => ApplyCheckpoint(
                observed.EntityId,
                observed.WorkingState),
            MarketOutlookSnapshotPublishedEvent published => ApplyCheckpoint(
                published.EntityId,
                published.WorkingState with
                {
                    PublishedSnapshot = published.MarketOutlook,
                    Status = MarketOutlookStateStatus.Published
                }),
            _ => false
        };

    /// <summary>Gets the current immutable working-state checkpoint.</summary>
    internal MarketOutlookWorkingStateReadModel WorkingState => _workingState;

    /// <summary>Determines whether the aggregate has already incorporated a source event.</summary>
    internal bool HasProcessed(Guid sourceEventId)
        => sourceEventId != Guid.Empty
            && _workingState.SourceWatermarks.Any(
                watermark => watermark.SourceEventId == sourceEventId);

    bool ApplyCheckpoint(
        MarketOutlookEntityId eventEntityId,
        MarketOutlookWorkingStateReadModel checkpoint)
    {
        if (eventEntityId is null
            || checkpoint is null
            || eventEntityId != checkpoint.EntityId
            || checkpoint.Revision < _workingState.Revision)
            return false;

        _workingState = checkpoint with
        {
            SourceWatermarks = [.. checkpoint.SourceWatermarks]
        };
        return true;
    }
}
