using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Receipt-based recovery includes late commits; advancing a global event-ID cursor could lose them.</summary>
public sealed class FinancialHistoryJournal(IPostgresEventTransaction transactions)
{
    static readonly string[] EventNames=[nameof(LedgerPostingCompletedEvent),nameof(LedgerPostingBatchCompletedEvent),
        nameof(CapacityReservationCompletedEvent),nameof(CapacityConsumptionCompletedEvent),nameof(CapacityLifecycleCompletedEvent),nameof(LedgerConfigurationCompletedEvent),nameof(EmulatorOrderSubmittedEvent)];

    public Task<IReadOnlyList<IFinancialCompletedEvent>> PendingAsync(CancellationToken token=default,int? portfolioId=null)=>transactions.ExecuteAsync(async(db,ct)=>
    {
        var rows=await db.QueryAsync(PortfolioDbSql.Financial.FinancialHistoryJournal.Select01,[EventNames,portfolioId],r=>new EventLogReadModel(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt64(3),r.GetFieldValue<byte[]>(4),r.GetGuid(5),r.GetString(6),r.GetInt64(7)),ct);
        return (IReadOnlyList<IFinancialCompletedEvent>)rows.Select(row=>row.ToDomainEvent() as IFinancialCompletedEvent
            ??throw new InvalidDataException($"Unsupported financial history event {row.EventVersion}.")).ToArray();
    },token);

    public Task AcknowledgeAsync(long eventId,CancellationToken token=default)=>transactions.ExecuteAsync(async(db,ct)=>
    {
        await db.ExecuteAsync(PortfolioDbSql.Financial.FinancialHistoryJournal.Insert01,[eventId,DateTime.UtcNow],ct);
        return true;
    },token);
}
