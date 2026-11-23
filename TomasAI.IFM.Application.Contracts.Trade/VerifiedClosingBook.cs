using System.Globalization;
using System.Security.Cryptography;
using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Coverage of an immutable, complete MBP-1 response; never a replacement quote timestamp.</summary>
[MessagePackObject]
public sealed record VerifiedClosingBook
{
    [Key(0)] public int Version { get; init; } = 1;
    [Key(1)] public string Dataset { get; init; } = "";
    [Key(2)] public DateTime RangeStartUtc { get; init; }
    [Key(3)] public DateTime RangeEndUtc { get; init; }
    [Key(4)] public DateTime VerifiedThroughUtc { get; init; }
    [Key(5)] public DateTime CapturedAtUtc { get; init; }
    [Key(6)] public long ExpectedRecords { get; init; }
    [Key(7)] public long Records { get; init; }
    [Key(8)] public uint PublisherId { get; init; }

    public bool Covers(DateTime boundary) => Version == 1 && Dataset.Length > 0 && PublisherId > 0
        && RangeStartUtc.Kind == DateTimeKind.Utc && RangeEndUtc.Kind == DateTimeKind.Utc
        && VerifiedThroughUtc.Kind == DateTimeKind.Utc && CapturedAtUtc.Kind == DateTimeKind.Utc
        && VerifiedThroughUtc == boundary && RangeStartUtc < boundary && RangeEndUtc >= boundary.AddSeconds(15)
        && RangeStartUtc.Ticks % TimeSpan.TicksPerMinute == 0 && RangeStartUtc.Minute % 10 == 0
        && RangeEndUtc.Ticks % TimeSpan.TicksPerMinute == 0 && RangeEndUtc.Minute % 10 == 0
        && CapturedAtUtc >= RangeEndUtc && ExpectedRecords > 0 && ExpectedRecords == Records;
}

/// <summary>Replays authoritative full L1 states, including clears; refuses incomplete archives and gap flags.</summary>
public static class ClosingBookArchive
{
    public const int MaximumBytes = 64 * 1024 * 1024;
    public static StoredPositionClosingQuote? Reconstruct(string contract, uint instrument, DateOnly date,
        DateTime boundary, VerifiedClosingBook proof, byte[] original, bool option)
    {
        if (original.Length is 0 or > MaximumBytes || proof.Version != 1 || proof.ExpectedRecords <= 0
            || proof.RangeStartUtc.Kind != DateTimeKind.Utc || proof.RangeEndUtc.Kind != DateTimeKind.Utc
            || boundary.Kind != DateTimeKind.Utc || proof.VerifiedThroughUtc != boundary
            || proof.RangeStartUtc >= boundary || proof.RangeEndUtc < boundary.AddSeconds(15)
            || proof.CapturedAtUtc.Kind != DateTimeKind.Utc || proof.CapturedAtUtc < proof.RangeEndUtc)
            throw Invalid("COVERAGE");
        using var stream = new MemoryStream(original, false);
        using var reader = new StreamReader(stream);
        var header = reader.ReadLine() ?? throw Invalid("HEADER");
        var columns = header.Split(',').Select((name,index)=>(name:name.Trim('"','\uFEFF'),index))
            .ToDictionary(x=>x.name,x=>x.index,StringComparer.Ordinal);
        foreach(var name in new[]{"action","rtype","publisher_id","instrument_id","ts_event","ts_recv","sequence","flags","bid_px_00","ask_px_00","bid_sz_00","ask_sz_00"})
            if(!columns.ContainsKey(name)) throw Invalid("HEADER");
        var start = Ns(proof.RangeStartUtc); var end = Ns(proof.RangeEndUtc); var close = Ns(boundary);
        long count=0,lastReceive=0; uint publisher=0; StoredPositionClosingQuote? latest=null;
        while(reader.ReadLine() is {} line)
        {
            if(line.Length is 0 or >16384) throw Invalid("ROW");
            var fields=line.Split(','); if(fields.Length!=columns.Count)throw Invalid("ROW");
            long N(string name)=>long.Parse(fields[columns[name]].Trim('"'),NumberStyles.Integer,CultureInfo.InvariantCulture);
            var receive=N("ts_recv");var at=N("ts_event");var flags=N("flags");var pub=checked((uint)N("publisher_id"));
            if(N("rtype")!=1 || N("instrument_id")!=instrument || pub==0 || publisher!=0 && publisher!=pub
                || receive<start || receive>=end || receive<lastReceive || at<0 || at>receive
                // Ranges filter by receive time. A normal in-flight full L1 update may have
                // an exchange timestamp just before the range start. Older anchors still
                // require an explicit snapshot; preserve the existing 15-second late allowance.
                || (flags&4)!=0 || (flags&128)==0 || (flags&32)==0 && at<start && receive-at>15L*TimeSpan.TicksPerSecond*100)
                throw Invalid("GAP_OR_INVALID_EVENT");
            publisher=pub;lastReceive=receive;count++;
            if(at>close) continue; // Late receipts are included; post-close exchange events never alter the close.
            if(fields[columns["action"]].Trim('"')=="R"){latest=null;continue;}
            var bid=N("bid_px_00");var ask=N("ask_px_00");var bs=checked((uint)N("bid_sz_00"));var az=checked((uint)N("ask_sz_00"));
            var hasBid=bid>0 && bid!=long.MaxValue && bs>0;
            // Each record is a complete L1 state. An empty/crossed final state supersedes every earlier valid quote.
            latest=ask>0 && ask!=long.MaxValue && az>0 && (hasBid ? bid<=ask : option)
                ? new(){ContractId=contract,InstrumentId=instrument,ValueDate=date,EventTimestampNanoseconds=at,
                    ReceiveTimestampNanoseconds=receive,SourceSequence=checked((uint)N("sequence")),
                    HasBid=hasBid,Bid=hasBid?bid/1_000_000_000m:0,Ask=ask/1_000_000_000m,BidSize=hasBid?bs:0,AskSize=az}
                : null;
        }
        if(count!=proof.ExpectedRecords || publisher==0 || proof.PublisherId!=0 && publisher!=proof.PublisherId)
            throw Invalid("TRUNCATED_OR_WRONG_PUBLISHER");
        if(latest is null || latest.SourceSequence==0)return null;
        var verified=proof with{Records=count,PublisherId=publisher};
        if(!verified.Covers(boundary))throw Invalid("COVERAGE");
        return latest with{Book=verified,SourceArchiveSha256=Convert.ToHexString(SHA256.HashData(original))};
    }

    public static void Verify(StoredPositionClosingQuote quote, byte[] original, bool option)
    {
        var proof=quote.Book ?? throw Invalid("MISSING_PROOF");
        var replay=Reconstruct(quote.ContractId,quote.InstrumentId,quote.ValueDate,proof.VerifiedThroughUtc,proof,original,option);
        if(replay is null || replay != (quote with{EvidenceId=""}))throw Invalid("QUOTE_MISMATCH");
    }

    public static bool Qualifies(StrategyPositionSnapshot seal, DateTime boundary, TimeSpan maxAge)
    {
        if(seal.LastMarketObservationUtc==default || seal.LastMarketObservationUtc>boundary)return false;
        var legs=seal.Legs.Where(x=>x.SignedQuantity!=0).ToArray();
        if(seal.EndOfDayClosingQuotes?.Any(q=>q.ExpiryFixing is not null)==true)
        {
            try { VerifiedExpiryFixing.RequireOffsetDelivery(seal,seal.EndOfDayClosingQuotes,boundary); }
            catch(InvalidOperationException){return false;}
            return legs.Length>0 && legs.All(leg=>seal.EndOfDayClosingQuotes.Any(q=>q.ContractId==leg.ContractId
                && q.ValueDate==seal.ValueDate && q.MarkPrice==leg.CurrentPrice && q.ExpiryFixing?.Covers(q,boundary)==true
                && q.EvidenceId.Length==64 && q.SourceArchiveSha256.Length==64));
        }
        return legs.Length>0 && legs.All(leg=>leg.LastPriceAtUtc!=default && leg.LastPriceAtUtc<=boundary &&
            (boundary-leg.LastPriceAtUtc<=maxAge || seal.EndOfDayClosingQuotes?.Any(q=>q.ContractId==leg.ContractId
                && q.ValueDate==seal.ValueDate && q.ObservedAtUtc==leg.LastPriceAtUtc && q.MarkPrice==leg.CurrentPrice
                && q.SourceSequence==leg.LastSourceSequence && q.Book?.Covers(boundary)==true
                && q.EvidenceId.Length==64 && q.SourceArchiveSha256.Length==64)==true));
    }
    static long Ns(DateTime at)=>checked((at.Ticks-DateTime.UnixEpoch.Ticks)*100);
    static InvalidDataException Invalid(string code)=>new("EOD.BOOK."+code);
}
