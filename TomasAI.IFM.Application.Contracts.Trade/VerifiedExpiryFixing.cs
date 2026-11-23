using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Expiry valuation from the exchange's published ES fixing, never a tradable bid/ask or execution.</summary>
[MessagePackObject]
public sealed record VerifiedExpiryFixing
{
    [Key(0)] public OptionPricingConvention Convention { get; init; } = null!;
    [Key(1)] public string ReferencePayloadSha256 { get; init; } = "";
    [Key(2)] public uint UnderlyingInstrumentId { get; init; }
    [Key(3)] public uint UnderlyingPublisherId { get; init; }
    [Key(4)] public decimal FixingPrice { get; init; }
    [Key(5)] public DateOnly ReferenceDate { get; init; }
    [Key(6)] public long PublishedNanoseconds { get; init; }
    [Key(7)] public long ReceivedNanoseconds { get; init; }
    [Key(8)] public uint Sequence { get; init; }
    [Key(9)] public DateTime CapturedAtUtc { get; init; }
    [IgnoreMember] public decimal Price => Convention.Right == PricingOptionRight.Call
        ? Math.Max(0, FixingPrice - Convention.Strike!.Value) : Math.Max(0, Convention.Strike!.Value - FixingPrice);

    public bool Covers(StoredPositionClosingQuote quote, DateTime boundary)
    {
        var c=Convention;
        if(c is null || c.SchemaVersion<3 || c.Root!="ES" || c.Currency!="USD" || c.Dataset!="GLBX.MDP3"
            || c.Exchange is not ("XCME" or "CME") || c.ExerciseStyle!=OptionExerciseStyle.European
            || c.SettlementStyle!=OptionSettlementStyle.DeliveryOfFuture || c.Strike is not >0
            || c.Right is not (PricingOptionRight.Call or PricingOptionRight.Put) || c.Multiplier!=50
            || c.RawSymbol is not {Length:>=4} || !c.RawSymbol.StartsWith("EW",StringComparison.Ordinal) || c.RawSymbol[2] is <'1' or >'5'
            || c.ExpirationUtc.Offset!=TimeSpan.Zero || c.LastTradingUtc!=c.ExpirationUtc
            || c.ExpirationUtc.DayOfWeek!=DayOfWeek.Friday || c.EffectiveFromUtc>c.ExpirationUtc
            || string.IsNullOrEmpty(c.ContractId) || c.ContractId!=quote.ContractId || c.InstrumentId==0 || c.InstrumentId!=quote.InstrumentId || c.PublisherId==0
            || ReferencePayloadSha256 is not {Length:64} || !ReferencePayloadSha256.All(char.IsAsciiHexDigit)
            || UnderlyingInstrumentId==0 || UnderlyingPublisherId!=c.PublisherId || string.IsNullOrEmpty(c.UnderlyingContractId)
            || ReferenceDate!=DateOnly.FromDateTime(c.ExpirationUtc.UtcDateTime) || quote.ValueDate<ReferenceDate
            || boundary.Kind!=DateTimeKind.Utc || boundary<c.ExpirationUtc.UtcDateTime || FixingPrice<=0
            || Sequence==0 || ReceivedNanoseconds<PublishedNanoseconds
            || quote.SourceSequence!=Sequence || quote.EventTimestampNanoseconds!=PublishedNanoseconds
            || quote.ReceiveTimestampNanoseconds!=ReceivedNanoseconds || quote.Book is not null
            || quote.HasBid || quote.Bid!=0 || quote.Ask!=0 || quote.BidSize!=0 || quote.AskSize!=0
            || CapturedAtUtc.Kind!=DateTimeKind.Utc) return false;
        var expiry=checked((c.ExpirationUtc.UtcTicks-DateTime.UnixEpoch.Ticks)*100);
        return PublishedNanoseconds>=expiry && PublishedNanoseconds<=expiry+5L*TimeSpan.TicksPerMinute*100
            && ReceivedNanoseconds<=checked((CapturedAtUtc.Ticks-DateTime.UnixEpoch.Ticks)*100);
    }

    /// <summary>Checks the original exchange fixing record, including date, exact instrument and statistic type.</summary>
    public static void Verify(StoredPositionClosingQuote quote, byte[] original)
    {
        var p=quote.ExpiryFixing ?? throw new InvalidDataException("EOD.EXPIRY.PROOF_MISSING");
        if(!p.Covers(quote,DateTime.SpecifyKind(quote.ValueDate.ToDateTime(TimeOnly.MaxValue),DateTimeKind.Utc))
            || original.Length is 0 or >65536) throw new InvalidDataException("EOD.EXPIRY.INVALID_PROOF");
        using var reader=new StringReader(Encoding.UTF8.GetString(original));
        var names=(reader.ReadLine()??"").Split(',').Select((name,index)=>(name:name.Trim('"','\uFEFF'),index)).ToDictionary(x=>x.name,x=>x.index,StringComparer.Ordinal);
        var fields=(reader.ReadLine()??"").Split(',');
        if(fields.Length!=names.Count || reader.ReadLine() is not null) throw new InvalidDataException("EOD.EXPIRY.INVALID_ARCHIVE");
        long N(string name)=>long.Parse(fields[names[name]].Trim('"'),NumberStyles.Integer,CultureInfo.InvariantCulture);
        var reference=checked((p.ReferenceDate.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc).Ticks-DateTime.UnixEpoch.Ticks)*100);
        if(N("rtype")!=24 || N("stat_type")!=10 || N("update_action")!=1 || N("stat_flags")!=0
            || N("publisher_id")!=p.UnderlyingPublisherId || N("instrument_id")!=p.UnderlyingInstrumentId
            || N("ts_ref")!=reference || N("ts_event")!=p.PublishedNanoseconds || N("ts_recv")!=p.ReceivedNanoseconds
            || N("sequence")!=p.Sequence || N("price")/1_000_000_000m!=p.FixingPrice
            || Convert.ToHexString(SHA256.HashData(original))!=quote.SourceArchiveSha256)
            throw new InvalidDataException("EOD.EXPIRY.ARCHIVE_MISMATCH");
    }

    /// <summary>Valuation recovery cannot hide a residual futures delivery or a partly expired strategy.</summary>
    public static void RequireOffsetDelivery(StrategyPositionSnapshot position, IReadOnlyList<StoredPositionClosingQuote> quotes, DateTime boundary)
    {
        if(!quotes.Any(q=>q.ExpiryFixing is not null))return;
        var active=position.Legs.Where(l=>l.SignedQuantity!=0).ToArray();
        if(active.Length==0 || quotes.Count!=active.Length || quotes.Select(q=>q.ContractId).Distinct(StringComparer.Ordinal).Count()!=quotes.Count
            || active.Any(l=>quotes.All(q=>q.ContractId!=l.ContractId)) || quotes.Any(q=>q.ExpiryFixing?.Covers(q,boundary)!=true))
            throw new InvalidOperationException("EOD.EXPIRY.PARTIAL_STRATEGY");
        var map=quotes.ToDictionary(q=>q.ContractId,StringComparer.Ordinal);
        var groups=active.GroupBy(l=> (map[l.ContractId].ExpiryFixing!.Convention.UnderlyingContractId,
            map[l.ContractId].ExpiryFixing!.UnderlyingInstrumentId,map[l.ContractId].ExpiryFixing!.UnderlyingPublisherId,
            map[l.ContractId].ExpiryFixing!.Convention.ExpirationUtc));
        foreach(var group in groups)
        {
            if(group.Select(l=>map[l.ContractId].ExpiryFixing!.FixingPrice).Distinct().Count()!=1
                || group.Sum(l=>map[l.ContractId].MarkPrice==0 ? 0L : (long)l.SignedQuantity*
                    (map[l.ContractId].ExpiryFixing!.Convention.Right==PricingOptionRight.Call?1:-1))!=0)
                throw new InvalidOperationException("EOD.EXPIRY.UNOFFSET_DELIVERY");
        }
    }
}
