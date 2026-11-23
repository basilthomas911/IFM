using System.Security.Cryptography;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public sealed partial class TradeDbContext : IPositionEndOfDayRecoveryStore
{
    public Task ScheduleAsync(StrategyPositionId id, DateOnly date, DateTime boundaryUtc, CancellationToken token)
        => this.NativeTransaction().ExecuteOnceAsync((db,ct)=>db.ExecuteAsync("""
            INSERT INTO trade.position_eod_recovery(position_id,value_date,position_identity,boundary_utc,next_attempt_at,updated_at)
            VALUES($1,$2,$3,$4,$4,now())
            ON CONFLICT(position_id,value_date) DO UPDATE SET
                state=CASE WHEN position_eod_recovery.state='Complete' THEN 'Pending' ELSE position_eod_recovery.state END,
                next_attempt_at=CASE WHEN position_eod_recovery.state='Complete' THEN EXCLUDED.next_attempt_at ELSE position_eod_recovery.next_attempt_at END;
            """,[id.PositionId,date,MessagePackBinarySerializer.Shared.Serialize(id),boundaryUtc],ct),token);

    public async Task<int> CountOutstandingAsync(DateTimeOffset now, CancellationToken token)
        => Convert.ToInt32(await this.NativeTransaction().ExecuteOnceAsync((db,ct)=>db.ScalarAsync("""
            SELECT count(*) FROM trade.position_eod_recovery j JOIN trade.strategy_position_current p ON p.positionid=j.position_id
            WHERE j.state<>'Complete' AND j.boundary_utc<=$1 AND p.isopen;
            """,[now],ct),token).ConfigureAwait(false),System.Globalization.CultureInfo.InvariantCulture);

    public async Task<PositionEndOfDayJob?> ClaimAsync(DateTimeOffset now, TimeSpan leaseDuration, CancellationToken token)
    {
        if(leaseDuration<=TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        var rows=await this.NativeTransaction().ExecuteOnceAsync((db,ct)=>db.QueryAsync("""
            WITH candidate AS (
                SELECT queued.position_id,queued.value_date FROM trade.position_eod_recovery queued
                WHERE next_attempt_at<=$1 AND boundary_utc<=$1
                  AND (state='Pending' OR (state='Working' AND lease_until<=$1))
                  AND NOT EXISTS (SELECT 1 FROM trade.position_eod_recovery earlier WHERE earlier.position_id=queued.position_id
                    AND earlier.value_date<queued.value_date AND earlier.state<>'Complete')
                ORDER BY next_attempt_at,position_id,value_date FOR UPDATE SKIP LOCKED LIMIT 1
            )
            UPDATE trade.position_eod_recovery j SET state='Working',lease_id=$2,lease_until=$3,
                attempts=j.attempts+1,updated_at=$1 FROM candidate c
            WHERE j.position_id=c.position_id AND j.value_date=c.value_date
            RETURNING j.position_identity,j.value_date,j.boundary_utc,j.lease_id,j.attempts;
            """,[now,Guid.NewGuid(),now+leaseDuration],row=>new PositionEndOfDayJob(
                MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionId>(row.GetFieldValue<byte[]>(0)),
                row.GetFieldValue<DateOnly>(1),row.GetDateTime(2),row.GetGuid(3),row.GetInt32(4)),ct),token).ConfigureAwait(false);
        return rows.SingleOrDefault();
    }

    public Task FinishAsync(PositionEndOfDayJob job, DateTimeOffset now, string? error, CancellationToken token)
    {
        var delay=TimeSpan.FromSeconds(Math.Min(900,5*Math.Pow(2,Math.Min(job.Attempts,8))));
        return this.NativeTransaction().ExecuteOnceAsync(async(db,ct)=>{
            var changed=await db.ScalarAsync("""
                UPDATE trade.position_eod_recovery SET state=$1,last_error=$2,next_attempt_at=$3,
                    lease_id=NULL,lease_until=NULL,updated_at=$4
                WHERE position_id=$5 AND value_date=$6 AND lease_id=$7 AND state='Working' RETURNING position_id;
                """,[error is null?"Complete":"Pending",error is null?string.Empty:error[..Math.Min(error.Length,1500)],
                    now+delay,now,job.PositionId.PositionId,job.ValueDate,job.LeaseId],ct).ConfigureAwait(false);
            if(changed is null)throw new InvalidOperationException("EOD.RECOVERY.LEASE_LOST");return true;
        },token);
    }

    public async Task<PositionEndOfDaySource?> ReadSourceAsync(StrategyPositionId id, DateOnly date, CancellationToken token)
    {
        var boundary=FuturesTradingValueDate.GetSessionEndUtc(date).UtcDateTime;
        var snapshots=await this.NativeTransaction().ExecuteOnceAsync((db,ct)=>db.QueryAsync("""
            SELECT payload FROM trade.strategy_position_history WHERE positionid=$1 AND asofutc BETWEEN $2 AND $3 ORDER BY asofutc DESC LIMIT 256;
            """,[id.PositionId,boundary.AddDays(-2),boundary],row=>MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(row.GetFieldValue<byte[]>(0)),ct),token).ConfigureAwait(false);
        var mark=snapshots.FirstOrDefault(s=>s.ValueDate==date && s.Phase is StrategyPositionPhase.EndOfDay or StrategyPositionPhase.MarkToMarket);
        mark ??= await GetStrategyPositionAsync(id,token).ConfigureAwait(false);
        if(mark is null || mark.ValueDate!=date)return null;
        var actor=mark.StrategyKind switch { TradeStrategyKind.IronCondor=>"FuturesIronCondorTradePositionCommand",
            TradeStrategyKind.VerticalSpread=>"FuturesVerticalSpreadTradePositionCommand",TradeStrategyKind.FuturesOutright=>"FuturesTradePositionCommand",_=>throw new InvalidOperationException("Unsupported position strategy") };
        var rows=await this.NativeTransaction().ExecuteOnceAsync((db,ct)=>db.QueryAsync("""
            SELECT e.eventversion,n.eventtypename,e.streamversion,e.eventpayload FROM event_log e
            JOIN event_stream_id s USING(eventstreamid) JOIN event_name_id n USING(eventnameid)
            WHERE s.eventstream=$1 AND e.streamversion BETWEEN $2 AND $3 ORDER BY e.streamversion;
            """,[$"Command.{actor}.{id.Format()}",mark.PositionSequence,mark.PositionSequence+64],row=>new EventStreamReadModel {
                EventVersion=row.GetInt64(0),EventTypeName=row.GetString(1),StreamVersion=row.GetInt64(2),EventData=row.GetFieldValue<byte[]>(3) },ct),token).ConfigureAwait(false);
        foreach(var row in rows)
            if(row.ToDomainEvent() is PositionChangedEvent fact && fact.EntityId==id && fact.PositionSnapshot.PositionSequence==mark.PositionSequence
                && fact.PositionSnapshot.ValueDate==date)
                return new(row.EventVersion,fact.PositionSnapshot);
        return null;
    }

    public async Task<StrategyPositionSnapshot?> ReadPreviousSealAsync(StrategyPositionId id, DateOnly date, CancellationToken token)
    {
        var boundary=FuturesTradingValueDate.GetSessionEndUtc(date.AddDays(-1)).UtcDateTime;
        var rows=await this.NativeTransaction().ExecuteOnceAsync((db,ct)=>db.QueryAsync("""
            SELECT payload FROM trade.strategy_position_history WHERE positionid=$1 AND phase='EndOfDay' AND asofutc<=$2
            ORDER BY asofutc DESC,positionsequence DESC LIMIT 1;
            """,[id.PositionId,boundary],row=>MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(row.GetFieldValue<byte[]>(0)),ct),token).ConfigureAwait(false);
        return rows.SingleOrDefault();
    }

    public async Task<PositionClosingInstrument?> ResolveInstrumentAsync(string contractId, DateOnly date, CancellationToken token)
    {
        var rows=await this.NativeTransaction().ExecuteOnceAsync((db,ct)=>db.QueryAsync("""
            SELECT payload FROM securities.securities_reference_version WHERE contract_id=$1 AND published IS TRUE ORDER BY version DESC LIMIT 64;
            """,[contractId],row=>MessagePackBinarySerializer.Shared.Deserialize<ReferenceContractVersion>(row.GetFieldValue<byte[]>(0)),ct),token).ConfigureAwait(false);
        var boundary=FuturesTradingValueDate.GetSessionEndUtc(date);
        var candidates=rows.Select(r=>r.Option is { } option ?
            (id:option.ContractId,instrument:option.InstrumentId,dataset:option.Dataset,asOf:option.DefinitionTimestampUtc,isOption:true,raw:option.RawSymbol,publisher:option.PublisherId) :
            (id:r.Future?.ContractId,instrument:r.Future?.InstrumentId,dataset:r.Future?.Dataset,asOf:r.Future?.DefinitionTimestampUtc,isOption:false,raw:r.Future?.RawSymbol,publisher:r.Future?.PublisherId))
            .Where(r=>r.id==contractId && r.instrument is >0 && !string.IsNullOrWhiteSpace(r.dataset) && r.asOf is not null && r.asOf<=boundary)
            .OrderByDescending(r=>r.asOf).ToArray();
        if(candidates.Length==0)return null;
        var latest=candidates[0];
        if(candidates.Where(c=>c.asOf==latest.asOf).Any(c=>c.instrument!=latest.instrument || c.dataset!=latest.dataset))
            throw new InvalidDataException("EOD.RECOVERY.AMBIGUOUS_INSTRUMENT");
        var selected=rows.First(r=>(r.Option?.InstrumentId??r.Future?.InstrumentId)==latest.instrument
            && (r.Option?.DefinitionTimestampUtc??r.Future?.DefinitionTimestampUtc)==latest.asOf);
        return new(contractId,latest.instrument!.Value,latest.dataset!,latest.isOption,string.IsNullOrWhiteSpace(latest.raw)?null:latest.raw,latest.publisher ?? 0)
        {
            ExpiryConvention=selected.Convention,
            ReferencePayloadSha256=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(selected))),
            UnderlyingInstrumentId=selected.Option?.UnderlyingInstrumentId??0,
            UnderlyingPublisherId=selected.Option?.UnderlyingPublisherId??0
        };
    }

    public async Task<StoredPositionClosingQuote?> ReadQuoteAsync(string contractId, uint instrumentId, DateOnly date, CancellationToken token)
    {
        var rows=await this.NativeTransaction().ExecuteOnceAsync((db,ct)=>db.QueryAsync("""
            SELECT e.payload FROM market_data.position_eod_quote_evidence e JOIN market_data.position_eod_quote_original o
                ON o.sha256=e.source_archive_sha256 WHERE e.contract_id=$1 AND e.value_date=$2
            ORDER BY e.observed_at DESC LIMIT 16;
            """,[contractId,date],row=>MessagePackBinarySerializer.Shared.Deserialize<StoredPositionClosingQuote>(row.GetFieldValue<byte[]>(0)),ct),token).ConfigureAwait(false);
        return rows.Where(q=>q.InstrumentId==instrumentId).OrderByDescending(q=>q.ExpiryFixing is not null)
            .ThenByDescending(q=>q.Book?.VerifiedThroughUtc ?? q.ObservedAtUtc).FirstOrDefault();
    }

    public Task SaveQuoteAsync(AcquiredPositionClosingQuote acquired, CancellationToken token)
    {
        var quote=acquired.Quote;var bytes=MessagePackBinarySerializer.Shared.Serialize(quote);
        var expected=Convert.ToHexString(SHA256.HashData(MessagePackBinarySerializer.Shared.Serialize(quote with {EvidenceId=string.Empty})));
        if(acquired.OriginalRecord.Length is 0 or >ClosingBookArchive.MaximumBytes || expected!=quote.EvidenceId
            || Convert.ToHexString(SHA256.HashData(acquired.OriginalRecord))!=quote.SourceArchiveSha256)
            throw new InvalidDataException("EOD.RECOVERY.INVALID_ORIGINAL_RECORD");
        if (quote.Book is not null) ClosingBookArchive.Verify(quote,acquired.OriginalRecord,true);
        if (quote.ExpiryFixing is not null) VerifiedExpiryFixing.Verify(quote,acquired.OriginalRecord);
        return this.NativeTransaction().ExecuteOnceAsync(async(db,ct)=>{
            await db.ExecuteAsync("INSERT INTO market_data.position_eod_quote_original VALUES($1,$2) ON CONFLICT DO NOTHING;",[quote.SourceArchiveSha256,acquired.OriginalRecord],ct).ConfigureAwait(false);
            await db.ExecuteAsync("""
                INSERT INTO market_data.position_eod_quote_evidence(evidence_id,payload,source_archive_sha256,contract_id,value_date,observed_at)
                VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT DO NOTHING;
                """,[quote.EvidenceId,bytes,quote.SourceArchiveSha256,quote.ContractId,quote.ValueDate,quote.ObservedAtUtc],ct).ConfigureAwait(false);
            var saved=await db.ScalarAsync("SELECT payload FROM market_data.position_eod_quote_evidence WHERE evidence_id=$1;",[quote.EvidenceId],ct).ConfigureAwait(false);
            var original=await db.ScalarAsync("SELECT original_record FROM market_data.position_eod_quote_original WHERE sha256=$1;",[quote.SourceArchiveSha256],ct).ConfigureAwait(false);
            if(saved is not byte[] stored || !stored.AsSpan().SequenceEqual(bytes) || original is not byte[] record || !record.AsSpan().SequenceEqual(acquired.OriginalRecord))
                throw new InvalidDataException("EOD.RECOVERY.EVIDENCE_CONFLICT");return true;
        },token);
    }
}
