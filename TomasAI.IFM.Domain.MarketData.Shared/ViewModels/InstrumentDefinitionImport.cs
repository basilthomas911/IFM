using System.Globalization;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Deterministic draft conversion. Convention review is separate from provider selection.</summary>
public static class InstrumentDefinitionImport
{
    static void Validate(InstrumentDefinitionSelection source, DateTimeOffset at, string timeZone)
    {
        if (source.SnapshotId == Guid.Empty || source.InstrumentId == 0 || source.PublisherId == 0
            || source.Deleted || source.ExpirationUtc is null || source.ExpirationUtc <= at || source.ActivationUtc > at
            || string.IsNullOrWhiteSpace(source.RawSymbol) || source.DefinitionDigest.Length != 64
            || string.IsNullOrWhiteSpace(source.RawDefinitionReference))
            throw new ArgumentException("A current, complete provider definition is required.");
        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
    }
    static DateOnly ExpiryDate(InstrumentDefinitionSelection source, string timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(source.ExpirationUtc!.Value,
            TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);

    public static FuturesContractV3ReadModel Future(InstrumentDefinitionSelection source, string timeZone, DateTimeOffset at)
    {
        Validate(source, at, timeZone);
        if (source.InstrumentClass != "F" || string.IsNullOrWhiteSpace(source.Root)
            || source.Root.Any(c => !char.IsAsciiLetterOrDigit(c))) throw new ArgumentException("A futures definition is required.");
        var date = ExpiryDate(source, timeZone);
        return new(FormattableString.Invariant($"{source.Root}{date:yyyyMMdd}"), source.RawSymbol, source.Root,
            source.RawSymbol, "FUT", source.Currency, source.Exchange,
            source.Multiplier?.ToString(CultureInfo.InvariantCulture) ?? "", date, false)
        {
            SchemaVersion = 1, ReviewState = ReferenceReviewState.Draft, Dataset = source.Dataset,
            PublisherId = source.PublisherId, InstrumentId = source.InstrumentId, RawSymbol = source.RawSymbol,
            DefinitionTimestampUtc = source.DefinitionTimestampUtc, DefinitionDigest = source.DefinitionDigest,
            RawDefinitionReference = source.RawDefinitionReference, ExpirationUtc = source.ExpirationUtc,
            ExchangeTimeZoneId = timeZone, MultiplierValue = source.Multiplier, PriceScale = 1m, TickSize = source.TickSize
        };
    }

    public static FuturesOptionContractReadModel Option(InstrumentDefinitionSelection source, FuturesContractV3ReadModel underlying,
        string timeZone, DateTimeOffset at)
    {
        Validate(source, at, timeZone);
        if (source.InstrumentClass is not ("C" or "P") || source.Strike is not > 0)
            throw new ArgumentException("A call or put with an exact strike is required.");
        if (underlying.SecurityType != "FUT" || underlying.SchemaVersion != 1
            || underlying.InstrumentId != source.UnderlyingInstrumentId || source.UnderlyingInstrumentId == 0
            || underlying.Dataset != source.Dataset || underlying.PublisherId != source.PublisherId
            || underlying.ExpirationUtc is null || underlying.ExpirationUtc < source.ExpirationUtc)
            throw new ArgumentException("Import/select the exact provider underlying future before saving the option.");
        var date = ExpiryDate(source, timeZone);
        var call = source.InstrumentClass == "C";
        var strike = source.Strike.Value;
        return new(FuturesOptionContractId.Create(underlying.Symbol, date, call ? OptionType.Call : OptionType.Put, strike),
            source.RawSymbol, underlying.Symbol, source.RawSymbol, "FOP", source.Currency, source.Exchange,
            source.Multiplier?.ToString(CultureInfo.InvariantCulture) ?? "", date, (double)strike, call ? "Call" : "Put")
        {
            SchemaVersion = 1, ReviewState = ReferenceReviewState.Draft, StrikePriceDecimal = strike,
            Dataset = source.Dataset, PublisherId = source.PublisherId, InstrumentId = source.InstrumentId,
            RawSymbol = source.RawSymbol, DefinitionTimestampUtc = source.DefinitionTimestampUtc,
            DefinitionDigest = source.DefinitionDigest, RawDefinitionReference = source.RawDefinitionReference,
            ExpirationUtc = source.ExpirationUtc, ExchangeTimeZoneId = timeZone, MultiplierValue = source.Multiplier,
            PriceScale = 1m, TickSize = source.TickSize, UnderlyingContractId = underlying.ContractId,
            UnderlyingAssetType = ReferenceAssetType.Futures,
            UnderlyingInstrumentId = source.UnderlyingInstrumentId, UnderlyingPublisherId = underlying.PublisherId,
            OptionRight = call ? ReferenceOptionRight.Call : ReferenceOptionRight.Put
        };
    }
}
