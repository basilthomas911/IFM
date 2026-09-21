using System.Globalization;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Reference qualification is separate from legacy structural validation. No convention is inferred.</summary>
public static class FuturesReferenceQualification
{
    public static IReadOnlyList<string> Errors(FuturesContractV3ReadModel c)
    {
        var errors = Common(c.SchemaVersion, c.ReviewState, c.Dataset, c.PublisherId, c.InstrumentId, c.RawSymbol,
            c.DefinitionTimestampUtc, c.DefinitionDigest, c.RawDefinitionReference, c.ExpirationUtc, c.LastTradingUtc,
            c.ExchangeTimeZoneId, c.Multiplier, c.MultiplierValue, c.PriceScale, c.TickSize, c.CalendarVersion,
            c.MappingVersion, c.EvidenceId, c.EffectiveFromUtc, c.EffectiveUntilUtc);
        if (c.SecurityType != "FUT") errors.Add("SecurityType");
        if (c.SettlementStyle is not (ReferenceSettlementStyle.Cash or ReferenceSettlementStyle.Physical)) errors.Add("SettlementStyle");
        if (!c.IsValid) errors.Add("LegacyIdentity");
        return errors;
    }

    public static IReadOnlyList<string> Errors(FuturesOptionContractReadModel c)
    {
        var errors = Common(c.SchemaVersion, c.ReviewState, c.Dataset, c.PublisherId, c.InstrumentId, c.RawSymbol,
            c.DefinitionTimestampUtc, c.DefinitionDigest, c.RawDefinitionReference, c.ExpirationUtc, c.LastTradingUtc,
            c.ExchangeTimeZoneId, c.Multiplier, c.MultiplierValue, c.PriceScale, c.TickSize, c.CalendarVersion,
            c.MappingVersion, c.EvidenceId, c.EffectiveFromUtc, c.EffectiveUntilUtc);
        if (c.SecurityType != "FOP") errors.Add("SecurityType");
        if (c.UnderlyingAssetType != ReferenceAssetType.Futures || string.IsNullOrWhiteSpace(c.UnderlyingContractId)) errors.Add("Underlying");
        if (c.UnderlyingInstrumentId is null or 0 || c.UnderlyingPublisherId is null or 0) errors.Add("UnderlyingProviderIdentity");
        if (c.ExerciseStyle is not (ReferenceExerciseStyle.European or ReferenceExerciseStyle.American)) errors.Add("ExerciseStyle");
        if (c.SettlementStyle is not (ReferenceSettlementStyle.Cash or ReferenceSettlementStyle.DeliveryOfFuture)) errors.Add("SettlementStyle");
        if (c.PremiumStyle is not (ReferencePremiumStyle.PremiumPaid or ReferencePremiumStyle.FuturesStyleVariation)) errors.Add("PremiumStyle");
        if (c.OptionRight is not (ReferenceOptionRight.Call or ReferenceOptionRight.Put)
            || c.OptionType != (c.OptionRight == ReferenceOptionRight.Call ? "Call" : "Put")) errors.Add("OptionRight");
        if (c.DayCount is not (ReferenceDayCount.Actual365Fixed or ReferenceDayCount.Actual360)) errors.Add("DayCount");
        if (c.PremiumTickRule is not (ReferencePremiumTickRule.Fixed or ReferencePremiumTickRule.CmeEsGlobex358A)
            || string.IsNullOrWhiteSpace(c.TickRuleVersion)) errors.Add("PremiumTickRule");
        if (c.StrikePriceDecimal is null) errors.Add("StrikePriceDecimal");
        try { _ = c.GetExactStrikePrice(); }
        catch (InvalidDataException) { errors.Add("StrikeConflict"); }
        if (c.ExerciseCutoffUtc is { } cutoff && (cutoff.Offset != TimeSpan.Zero || cutoff > c.ExpirationUtc)) errors.Add("ExerciseCutoffUtc");
        if (!c.IsValid) errors.Add("LegacyIdentity");
        return errors;
    }

    static List<string> Common(int schema, ReferenceReviewState review, string? dataset, ushort? publisher,
        uint? instrument, string? raw, DateTimeOffset? defined, string? digest, string? rawReference,
        DateTimeOffset? expiration, DateTimeOffset? lastTrading, string? zone, string legacyMultiplier,
        decimal? multiplier, decimal? scale, decimal? tick, string? calendar, string? version,
        string? evidence, DateTimeOffset? from, DateTimeOffset? until)
    {
        List<string> errors = [];
        if (schema != 1 || review != ReferenceReviewState.Reviewed) errors.Add("ReviewRequired");
        if (string.IsNullOrWhiteSpace(dataset) || publisher is null or 0 || instrument is null or 0
            || string.IsNullOrWhiteSpace(raw)) errors.Add("ProviderIdentity");
        if (!Utc(defined) || digest is not { Length: 64 } || !digest.All(Uri.IsHexDigit)
            || string.IsNullOrWhiteSpace(rawReference)) errors.Add("DefinitionEvidence");
        if (!Utc(expiration) || !Utc(lastTrading) || lastTrading > expiration) errors.Add("ExactTimes");
        if (!Utc(from) || !Utc(until) || from >= until) errors.Add("EffectiveInterval");
        if (multiplier is null or <= 0 || !decimal.TryParse(legacyMultiplier, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var oldMultiplier) || oldMultiplier != multiplier) errors.Add("Multiplier");
        if (scale is null or <= 0 || tick is null or <= 0) errors.Add("PriceScale/TickSize");
        if (string.IsNullOrWhiteSpace(calendar) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(evidence))
            errors.Add("ConventionEvidence");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(zone ?? ""); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { errors.Add("ExchangeTimeZoneId"); }
        return errors;
    }

    static bool Utc(DateTimeOffset? value) => value is { Offset: var offset } && offset == TimeSpan.Zero
        && value != DateTimeOffset.MinValue && value != DateTimeOffset.MaxValue;
}
