using System.Globalization;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
namespace TomasAI.IFM.Application.MarketData.Rust.Publication;
/// <summary>Raw convention wire projection only. No source qualification,
/// financial calculation, provider lease or context preparation occurs here.</summary>
public static class NativeReferenceProjection
{
    /// <summary>Copies complete immutable reference configuration. No rates,
    /// calendars, source admission or financial eligibility are prepared here.</summary>
    public static object References(OptionPricingCalendar calendar,TomasAI.IFM.Application.MarketData.Pricing.TreasuryPublicationPolicy publication,
        TomasAI.IFM.Shared.MarketData.Contracts.TreasuryRateConversionPolicy conversion,int maximumLead=0)
    {
        static long Ticks(DateTimeOffset at)=>at.UtcTicks-DateTime.UnixEpoch.Ticks;
        static long Offset(DateTimeOffset at)=>(long)at.Offset.TotalMinutes;
        return new{calendar=new{version=calendar.Version,timezone=calendar.TimeZoneId,from=calendar.CoverageFrom.DayNumber,until=calendar.CoverageUntil.DayNumber,
            rollover=calendar.ValueDateRollover.Ticks,dates=calendar.TradingDates.Select(date=>date.DayNumber)},
            publication=new{version=publication.Version,timezone=publication.TimeZoneId,from=Ticks(publication.CoverageFromUtc),until=Ticks(publication.CoverageUntilUtc),
                from_offset=Offset(publication.CoverageFromUtc),until_offset=Offset(publication.CoverageUntilUtc),entries=publication.Publications.Select(row=>new{date=row.ValueDate.DayNumber,available=Ticks(row.AvailableByUtc),offset=Offset(row.AvailableByUtc)})},
            conversion=new{source=conversion.Source,series=conversion.SourceSeriesId,convention=(int)conversion.Convention,version=conversion.Version,evidence=conversion.EvidenceId},maximum_lead=maximumLead};
    }
    /// <summary>Copies the published policy fields only. Native code validates,
    /// hashes, plans coverage and selects candidates; no C# policy decision runs here.</summary>
    public static object[] Strategy(TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet p)
    {
        static string Decimal(decimal value)=>value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        static object[] Range(TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.OptionDeltaRange range)=>[Decimal(range.Minimum),Decimal(range.Target),Decimal(range.Maximum)];
        object[] Bias(TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.OptionStrategyBiasParameters b)=>[
            (int)b.MarketBias,b.Enabled,b.MinimumDte,b.MaximumDte,b.PreferredDte,Range(b.PutDelta),Range(b.CallDelta),b.PutWingWidths.Select(Decimal).ToArray(),b.CallWingWidths.Select(Decimal).ToArray(),
            Decimal(b.TargetNetDelta),Decimal(b.NetDeltaTolerance),b.MaximumQuoteAgeMilliseconds,b.MaximumQuoteSkewMilliseconds,Decimal(b.MinimumNetCredit),b.MaximumNetDebit is {} debit?Decimal(debit):null!,
            b.MinimumQuoteSize,Decimal(b.MaximumLegSpreadPoints),b.MaximumCandidatesPerSide];
        object[] parameters=[p.ParameterSetId.ToString(),p.Version,p.Name,p.StrategyDefinitionId.ToString(),p.StrategyDefinitionVersion,p.InstrumentRoot,p.Environment,p.Enabled,
            p.BiasRows.Select(Bias).ToArray(),p.CacheRangeMultiplier,p.MaximumContracts,p.MaximumExpirations,p.SchemaVersion];
        return parameters;
    }
    /// <summary>Copies exact normalized provider candidates. Immutable version,
    /// strike and expiry are data only; qualification remains native.</summary>
    public static object?[][] Candidates(IEnumerable<TomasAI.IFM.Application.MarketData.Pricing.OptionDefinitionCandidate> candidates)
        =>candidates.Select(candidate=>new object?[]{candidate.ContractId,candidate.MappingVersion,candidate.DefinitionDigest,candidate.Definition.Dataset,
            candidate.Definition.Instrument.PublisherId,candidate.Definition.Instrument.InstrumentId,candidate.Definition.RawSymbol,candidate.Definition.Underlying,
            (int)candidate.Definition.Right,candidate.Definition.StrikePrice.ToString(CultureInfo.InvariantCulture),candidate.Definition.ExpirationTimestampNanoseconds}).ToArray();
    /// <summary>Copies existing convention fields into native import representation.</summary>
    public static object?[] Convention(OptionPricingConvention c)
    {
        static object[] Time(DateTimeOffset at)=>[at.UtcTicks-DateTime.UnixEpoch.Ticks,(long)at.Offset.TotalMinutes];
        static string Amount(decimal value)=>value.ToString(CultureInfo.InvariantCulture);
        return [c.SchemaVersion,c.ContractId,c.Dataset,c.PublisherId,c.InstrumentId,c.RawSymbol,c.Root,c.Exchange,c.Currency,c.UnderlyingContractId,
            (int)c.ExerciseStyle,(int)c.SettlementStyle,Time(c.ExpirationUtc),Time(c.LastTradingUtc),(int)c.DayCount,c.CalendarVersion,
            Amount(c.Multiplier),Amount(c.TickSize),c.TickRuleVersion,c.DefinitionDigest,c.MappingVersion,c.EvidenceId,Time(c.EffectiveFromUtc),Time(c.EffectiveUntilUtc),
            (int)c.PremiumTickRule,(int)c.PremiumStyle,(int)c.UnderlyingKind,c.Strike is {} strike?Amount(strike):null,(int)c.Right];
    }
    /// <summary>Copies source fields needed for native coverage and underlying
    /// acquisition. Full review/financial metadata is fetched only after selection.</summary>
    public static object?[] CoverageOption(TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel contract)
    {
        var source=Option(contract);
        return new[]{0,2,8,9,11,12,13,14,15,16,19,21,23,24,29,33,34,35,36,37,40,42,43,44}.Select(field=>source[field]).ToArray();
    }
    /// <summary>Appends the untouched legacy option-type text for exact UI
    /// ordering. The background 24-field projection remains unchanged.</summary>
    public static object?[] BrowserOption(TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel contract)
        =>CoverageOption(contract).Append((object?)contract.OptionType).ToArray();
    /// <summary>Copies every public read-model field without qualification,
    /// price conversion, date inference or preparation. Rust validates the input.</summary>
    public static object?[] Option(TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return [
            c.ContractId,
            c.Description,
            c.Symbol,
            c.LocalSymbol,
            c.SecurityType,
            c.Currency,
            c.Exchange,
            c.Multiplier,
            c.ContractMonth.DayNumber,
            unchecked((ulong)BitConverter.DoubleToInt64Bits(c.StrikePrice)).ToString("x16",CultureInfo.InvariantCulture),
            c.OptionType,
            c.StrikePriceDecimal?.ToString(CultureInfo.InvariantCulture),
            c.SchemaVersion,
            (int)c.ReviewState,
            c.Dataset,
            c.PublisherId,
            c.InstrumentId,
            c.RawSymbol,
            c.DefinitionTimestampUtc is {} time18?new object[]{time18.UtcTicks-DateTime.UnixEpoch.Ticks,(long)time18.Offset.TotalMinutes}:null,
            c.DefinitionDigest,
            c.RawDefinitionReference,
            c.ExpirationUtc is {} time21?new object[]{time21.UtcTicks-DateTime.UnixEpoch.Ticks,(long)time21.Offset.TotalMinutes}:null,
            c.LastTradingUtc is {} time22?new object[]{time22.UtcTicks-DateTime.UnixEpoch.Ticks,(long)time22.Offset.TotalMinutes}:null,
            c.ExchangeTimeZoneId,
            (int)c.SettlementStyle,
            c.MultiplierValue?.ToString(CultureInfo.InvariantCulture),
            c.PriceScale?.ToString(CultureInfo.InvariantCulture),
            c.TickSize?.ToString(CultureInfo.InvariantCulture),
            c.CalendarVersion,
            c.MappingVersion,
            c.EvidenceId,
            c.EffectiveFromUtc is {} time31?new object[]{time31.UtcTicks-DateTime.UnixEpoch.Ticks,(long)time31.Offset.TotalMinutes}:null,
            c.EffectiveUntilUtc is {} time32?new object[]{time32.UtcTicks-DateTime.UnixEpoch.Ticks,(long)time32.Offset.TotalMinutes}:null,
            c.UnderlyingContractId,
            (int)c.UnderlyingAssetType,
            (int)c.OptionRight,
            (int)c.ExerciseStyle,
            (int)c.PremiumStyle,
            c.ExerciseCutoffUtc is {} time38?new object[]{time38.UtcTicks-DateTime.UnixEpoch.Ticks,(long)time38.Offset.TotalMinutes}:null,
            c.ExerciseResultContractId,
            (int)c.PremiumTickRule,
            c.TickRuleVersion,
            (int)c.DayCount,
            c.UnderlyingInstrumentId,
            c.UnderlyingPublisherId];
    }
}
