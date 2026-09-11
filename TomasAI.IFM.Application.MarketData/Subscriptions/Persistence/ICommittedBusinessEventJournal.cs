using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>Reads committed source events and records delivery receipts; receipt absence is replayable after a crash.</summary>
public interface ICommittedBusinessEventJournal
{
    Task<IReadOnlyList<EventLogReadModel>> ReadPendingAsync(IReadOnlyList<string> eventNames, CancellationToken cancellationToken);
    Task<EventLogReadModel?> ReadPriorAsync(long streamId, long throughEventId, IReadOnlyList<string> eventNames, CancellationToken cancellationToken);
    Task AcknowledgeAsync(long eventId, CancellationToken cancellationToken);
    Task RejectAsync(long eventId, string reasonCode, string detail, CancellationToken cancellationToken);
    Task<IReadOnlyList<EventLogReadModel>> ReadPendingHandoffsAsync(CancellationToken cancellationToken);
    Task CompleteHandoffAsync(long eventId, CancellationToken cancellationToken);
}
