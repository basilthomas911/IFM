

using System.Runtime.CompilerServices;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Reads financial source bytes directly from the event log, independent of all receipt and projection tables.</summary>
public sealed class FinancialEventLogReader(IPostgresEventTransaction transactions)
{
    static readonly string[] Names=[nameof(LedgerConfigurationCompletedEvent),nameof(LedgerPostingCompletedEvent),nameof(LedgerPostingBatchCompletedEvent),
        nameof(CapacityReservationCompletedEvent),nameof(CapacityConsumptionCompletedEvent),nameof(CapacityLifecycleCompletedEvent),nameof(EmulatorOrderSubmittedEvent),
        nameof(FinancialAuthoritySnapshotEvent),nameof(AccountingExportSnapshotEvent),nameof(BrokerAccountingIntentCapturedEvent),nameof(PortfolioOrderCompositionCompletedEvent),nameof(PortfolioCloseOrderCompositionCompletedEvent)];

    /// <summary>Loads the latest persisted full-database baseline, independent of projection receipts. Apply it only to the full financial target.</summary>
    public Task<FinancialProjectionBaselineEvent?> ReadLatestBaselineAsync(long inclusiveCut,CancellationToken token=default)
        => transactions.ExecuteAsync(async(db,ct)=>
        {
            var identity = EventWriteIdentityRegistry.Default.GetIdentity(typeof(FinancialProjectionBaselineEvent));
            var name=await (db.ScalarAsync("SELECT eventnameid FROM event_name_id WHERE eventname=$1 AND eventtypename=$2;",
                [identity.EventName, identity.EventTypeName],ct)).ConfigureAwait(false);
            if(name==null) return null;
            var rows=await (db.QueryAsync("""
                SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,e.commandid,e.eventtimestamp::text,e.streamversion
                FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
                WHERE e.eventversion<=$1 AND e.eventnameid=$2 ORDER BY e.eventversion DESC LIMIT 1;
                """,[inclusiveCut,Convert.ToInt32(name,System.Globalization.CultureInfo.InvariantCulture)],r=>new EventLogReadModel(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt64(3),
                    r.GetFieldValue<byte[]>(4),r.GetGuid(5),r.GetString(6),r.GetInt64(7)),ct)).ConfigureAwait(false);
            return rows.Count==0?null:(FinancialProjectionBaselineEvent)rows[0].ToDomainEvent();
        },token);

    /// <summary>Reads bounded pages from an explicitly quiesced source cut. The caller must stop source writes before capturing the cut.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4456", Justification = "Validation occurs on enumeration immediately before database access; callers consume this streaming contract asynchronously.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async IAsyncEnumerable<IEvent> ReadAsync(long inclusiveCut,IReadOnlySet<int> portfolioIds,bool sourceWritesQuiesced,
        [EnumeratorCancellation] CancellationToken token=default,long afterEventId=0)
    {
        ArgumentNullException.ThrowIfNull(portfolioIds);
        if(!sourceWritesQuiesced || inclusiveCut<=0 || portfolioIds.Count==0 || portfolioIds.Any(x=>x<=0))
            throw new ArgumentException("An explicit quiesced source cut and positive selected Portfolio IDs are required.",nameof(inclusiveCut));
        if(afterEventId<0 || afterEventId>inclusiveCut) throw new ArgumentOutOfRangeException(nameof(afterEventId));
        long cursor=afterEventId;
        while(true)
        {
            var rows=await (transactions.ExecuteAsync((db,ct)=>db.QueryAsync("""
                SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.EventPayload,e.commandid,e.eventtimestamp::text,e.streamversion
                FROM event_name_id n CROSS JOIN LATERAL (
                    SELECT * FROM event_log stored WHERE stored.eventnameid=n.eventnameid
                      AND stored.eventversion>$1 AND stored.eventversion<=$2
                    ORDER BY stored.eventversion LIMIT 1000
                ) e WHERE n.eventname=ANY($3)
                ORDER BY e.eventversion LIMIT 1000;
                """,[cursor,inclusiveCut,Names],r=>new EventLogReadModel(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt64(3),
                    r.GetFieldValue<byte[]>(4),r.GetGuid(5),r.GetString(6),r.GetInt64(7)),ct),token)).ConfigureAwait(false);
            if(rows.Count==0) yield break;
            foreach(var row in rows)
            {
                var source=row.ToDomainEvent();cursor=source.EventId;
                var portfolio=source switch
                {
                    IFinancialCompletedEvent value=>value.PortfolioId,
                    FinancialAuthoritySnapshotEvent value=>value.PortfolioId,
                    AccountingExportSnapshotEvent value=>value.PortfolioId,
                    BrokerAccountingIntentCapturedEvent value=>value.PortfolioId,
                    _=>throw new InvalidDataException("Unexpected financial source type.")
                };
                if(portfolioIds.Contains(portfolio)) yield return source;
            }
        }
    }
}
