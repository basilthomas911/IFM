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
        var rows=await db.QueryAsync("""
            SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,e.commandid,e.eventtimestamp::text,e.streamversion
            FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
            JOIN portfolio_financial.financial_operation_receipt o ON o.event_version=e.eventversion
            WHERE n.eventname=ANY($1) AND ($2::int IS NULL OR o.portfolio_id=$2)
              AND NOT EXISTS(SELECT 1 FROM portfolio_financial.financial_history_receipt r WHERE r.event_version=e.eventversion)
            ORDER BY e.eventversion LIMIT 32;
            """,[EventNames,portfolioId],r=>new EventLogReadModel(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt64(3),r.GetFieldValue<byte[]>(4),r.GetGuid(5),r.GetString(6),r.GetInt64(7)),ct);
        return (IReadOnlyList<IFinancialCompletedEvent>)rows.Select(row=>row.ToDomainEvent() as IFinancialCompletedEvent
            ??throw new InvalidDataException($"Unsupported financial history event {row.EventVersion}.")).ToArray();
    },token);

    public Task AcknowledgeAsync(long eventId,CancellationToken token=default)=>transactions.ExecuteAsync(async(db,ct)=>
    {
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.financial_history_receipt(event_version,projected_at_utc)
            VALUES($1,$2) ON CONFLICT DO NOTHING;
            """,[eventId,DateTime.UtcNow],ct);
        return true;
    },token);
}
