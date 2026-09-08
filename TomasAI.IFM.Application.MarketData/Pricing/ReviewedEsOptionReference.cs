using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Reviewed September 2026 Tuesday/Thursday ES profile. Unknown series or dates fail closed.</summary>
public static class ReviewedEsOptionReference
{
    public const string Version = "CME-ES-TueThu-202609/v2";
    public const string Evidence = "https://www.cmegroup.com/articles/faqs/e-mini-s-p-500-tuesday-and-thursday-options-frequently-asked-questions.html";
    // Labor Day trading belongs to September 8's business trade date. September 7 has
    // no separate settlement/value date and must not add a day to the pricing tenor.
    public static OptionPricingCalendar Calendar { get; } = new("CME-ES-202609/v2", "America/New_York",
        new(2026, 9, 1), new(2026, 9, 30), new(18, 0), Enumerable.Range(0, 30)
            .Select(i => new DateOnly(2026, 9, 1).AddDays(i))
            .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && d != new DateOnly(2026, 9, 7)).ToImmutableArray());

    public static (OptionDefinitionCandidate Candidate, OptionPricingConvention Convention) Create(ContractDetail option, ContractDetail future)
    {
        if (option.Dataset != "GLBX.MDP3" || future.Dataset != option.Dataset || future.ContractKind != ContractKind.Future
            || future.Ticker != "ES" || option.Underlying != future.RawSymbol || option.UnderlyingInstrumentId != future.Instrument.InstrumentId
            || option.Instrument.PublisherId != future.Instrument.PublisherId || future.Instrument.PublisherId != 1
            || option.Currency != "USD" || future.Currency != "USD" || option.Exchange != "XCME" || future.Exchange != "XCME"
            || option.SecurityType != "OOF" || option.UnitOfMeasure != "IPNT"
            || !Regex.IsMatch(option.Ticker, "^E[1-5][BD]$", RegexOptions.CultureInvariant)
            || !option.RawSymbol.StartsWith(option.Ticker, StringComparison.Ordinal)
            || option.ContractKind is not (ContractKind.CallOption or ContractKind.PutOption)
            || option.Cfi != (option.ContractKind == ContractKind.CallOption ? "OCEFPS" : "OPEFPS")
            || option.ContractMultiplier is not (null or 50) || future.ContractMultiplier is not (null or 50)
            || option.StrikePrice is not > 0 || option.StrikePrice % 1_000_000_000 != 0
            || option.MaturityDate is not { } expiry || expiry < Calendar.CoverageFrom || expiry > Calendar.CoverageUntil
            || expiry.DayOfWeek != (option.Ticker.EndsWith('B') ? DayOfWeek.Tuesday : DayOfWeek.Thursday)
            || option.MaturityWeek != (expiry.Day - 1) / 7 + 1
            || future.MaturityDate is not { } futureDate || futureDate < expiry || futureDate > expiry.AddMonths(3)
            || option.ExpirationTimestampNanoseconds is not { } ns || ns % 100 != 0
            || future.ExpirationTimestampNanoseconds is not { } futureNs || ns >= futureNs)
            throw new InvalidDataException("ES option definition is outside the reviewed profile or conflicts with its exact underlying.");
        var expires = DateTimeOffset.UnixEpoch.AddTicks(checked((long)(ns / 100)));
        var expected = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(expiry.ToDateTime(new TimeOnly(16, 0)),
            TimeZoneInfo.FindSystemTimeZoneById(Calendar.TimeZoneId)));
        if (expires != expected) throw new InvalidDataException("Provider expiry differs from the reviewed series termination time.");
        var strike = option.StrikePrice.Value / 1_000_000_000m;
        var right = option.ContractKind == ContractKind.CallOption ? OptionRightSelection.Call : OptionRightSelection.Put;
        var id = string.Create(CultureInfo.InvariantCulture, $"ES{expiry:yyyyMMdd}{(right == OptionRightSelection.Call ? 'C' : 'P')}{strike:0000}");
        var underlyingId = $"ES{futureDate:yyyyMMdd}";
        var digest = PricingSemanticHash.Compute(option);
        var convention = new OptionPricingConvention
        {
            SchemaVersion = 2, ContractId = id, Dataset = option.Dataset, PublisherId = option.Instrument.PublisherId,
            InstrumentId = option.Instrument.InstrumentId, RawSymbol = option.RawSymbol, Root = "ES", Exchange = option.Exchange,
            Currency = option.Currency, UnderlyingContractId = underlyingId, ExerciseStyle = OptionExerciseStyle.European,
            SettlementStyle = OptionSettlementStyle.DeliveryOfFuture, ExpirationUtc = expires, LastTradingUtc = expires,
            // Explicit application model convention, not a field purportedly supplied by DataBento/CME.
            DayCount = PricingDayCount.Actual365Fixed, CalendarVersion = Calendar.Version, Multiplier = 50,
            TickSize = .05m, PremiumTickRule = OptionPremiumTickRule.CmeEsGlobex358A, TickRuleVersion = OptionPremiumTicks.CmeEsGlobexVersion,
            DefinitionDigest = digest, MappingVersion = Version, EvidenceId = Evidence,
            EffectiveFromUtc = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), EffectiveUntilUtc = expires
        };
        return (new(id, Version, digest, new()
        {
            Dataset = option.Dataset, RawSymbol = option.RawSymbol, Ticker = option.Ticker, Underlying = underlyingId,
            Instrument = option.Instrument, Right = right, StrikePrice = strike, MaturityDate = expiry,
            ExpirationTimestampNanoseconds = ns, ActivationTimestampNanoseconds = option.ActivationTimestampNanoseconds,
            MinimumPriceIncrement = option.MinimumPriceIncrement, ContractMultiplier = option.ContractMultiplier
        }), convention);
    }
}
