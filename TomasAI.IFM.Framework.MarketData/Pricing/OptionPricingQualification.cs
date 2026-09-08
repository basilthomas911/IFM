using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Framework.MarketData.Pricing;

/// <summary>Validates explicit contract conventions; never infers style from root, symbol or horizon.</summary>
public static class OptionPricingQualification
{
    public static OptionPricingFailure? Validate(OptionPricingConvention c, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(c);
        OptionPricingFailure Fail(string code, string input) => new(code, input, c.ContractId, "Contract pricing qualification failed.");
        if (c.SchemaVersion != 1 || at.Offset != TimeSpan.Zero || c.InstrumentId == 0 || c.PublisherId == 0
            || string.IsNullOrWhiteSpace(c.ContractId) || string.IsNullOrWhiteSpace(c.UnderlyingContractId)
            || string.IsNullOrWhiteSpace(c.RawSymbol) || string.IsNullOrWhiteSpace(c.Exchange)
            || string.IsNullOrWhiteSpace(c.MappingVersion) || string.IsNullOrWhiteSpace(c.EvidenceId)
            || c.DefinitionDigest is not { Length: 64 } || !c.DefinitionDigest.All(Uri.IsHexDigit)
            || c.ExpirationUtc.Offset != TimeSpan.Zero || c.LastTradingUtc.Offset != TimeSpan.Zero
            || c.EffectiveFromUtc.Offset != TimeSpan.Zero || c.EffectiveUntilUtc.Offset != TimeSpan.Zero
            || at < c.EffectiveFromUtc || at >= c.EffectiveUntilUtc
            || c.LastTradingUtc > c.ExpirationUtc || c.Multiplier <= 0 || c.TickSize <= 0
            || string.IsNullOrWhiteSpace(c.TickRuleVersion) || string.IsNullOrWhiteSpace(c.CalendarVersion))
            return Fail("ContractMetadataUnavailable", "Definition/Mapping");
        if (c.ExerciseStyle == OptionExerciseStyle.Unknown)
            return Fail("ContractMetadataUnavailable", "ExerciseStyle");
        if (c.Root != "ES" || c.Dataset != "GLBX.MDP3" || c.Currency != "USD"
            || c.ExerciseStyle != OptionExerciseStyle.European
            || c.SettlementStyle != OptionSettlementStyle.DeliveryOfFuture)
            return Fail("PricingModelUnsupported", "Product/Exercise/Settlement");
        if (c.DayCount is not (PricingDayCount.Actual365Fixed or PricingDayCount.Actual360))
            return Fail("DayCountUnsupported", "DayCount");
        if (at >= c.ExpirationUtc || at >= c.LastTradingUtc)
            return Fail("ExpiredContract", "Expiration/LastTrading");
        return null;
    }

    /// <summary>Uses UTC elapsed time with the explicitly reviewed product denominator.</summary>
    public static double YearFraction(OptionPricingConvention c, DateTimeOffset valuation)
    {
        var failure = Validate(c, valuation);
        if (failure is not null) throw new ArgumentException(failure.Code, nameof(c));
        return (c.ExpirationUtc - valuation).TotalDays / (c.DayCount == PricingDayCount.Actual365Fixed ? 365d : 360d);
    }

    /// <summary>Counts exchange trading dates in (valuation value date, expiry value date].</summary>
    public static int CountTradingDays(OptionPricingCalendar calendar, OptionPricingConvention contract, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (calendar.TradingDates.IsDefault || calendar.TradingDates.Length > 2000
            || calendar.TimeZoneId is not ("America/New_York" or "Eastern Standard Time")
            || calendar.Version != contract.CalendarVersion || string.IsNullOrWhiteSpace(calendar.Version)
            || calendar.CoverageFrom > calendar.CoverageUntil
            || calendar.TradingDates.Any(x => x < calendar.CoverageFrom || x > calendar.CoverageUntil)
            || !calendar.TradingDates.SequenceEqual(calendar.TradingDates.Distinct().Order()))
            throw new ArgumentException("CalendarCoverageUnavailable", nameof(calendar));
        var zone = TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId);
        DateOnly ValueDate(DateTimeOffset instant)
        {
            var local = TimeZoneInfo.ConvertTime(instant, zone);
            var date = DateOnly.FromDateTime(local.DateTime);
            return TimeOnly.FromDateTime(local.DateTime) >= calendar.ValueDateRollover ? date.AddDays(1) : date;
        }
        var start = ValueDate(at);
        var end = ValueDate(contract.ExpirationUtc);
        if (at >= contract.ExpirationUtc || start < calendar.CoverageFrom || end > calendar.CoverageUntil
            || start > end || !calendar.TradingDates.Contains(end))
            throw new ArgumentException("CalendarCoverageUnavailable", nameof(calendar));
        return calendar.TradingDates.Count(x => x > start && x <= end);
    }
}
