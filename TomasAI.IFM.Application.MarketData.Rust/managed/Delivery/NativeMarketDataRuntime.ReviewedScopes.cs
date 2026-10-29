using System.Diagnostics;
using System.Globalization;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>Copied reviewed configuration. This record owns no feed, lease,
/// context preparation task, renewal timer or provider lifetime.</summary>
public sealed record NativeReviewedOption(OptionPricingConvention Contract, uint UnderlyingInstrument,
    decimal Strike, bool IsCall, bool RiskRequired);

/// <summary>Complete immutable reference inputs for native scope construction.
/// Rust fetches the curve, qualifies the context and retains rebuild authority.</summary>
public sealed record NativeReviewedScopeReferences(OptionPricingCalendar Calendar,
    TreasuryPublicationPolicy Publication, TreasuryRateConversionPolicy Conversion,
    NativeReviewedOption[] Options, OptionPricingRefreshPolicy RefreshPolicy,
    int MaximumSourceClockLeadMilliseconds, bool Production);

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Imports reviewed data and physical intent. All reference HTTP,
    /// preparation, context hashing, manifests, refresh and incident supervision
    /// happen inside Rust. The adapter only copies command arguments.</summary>
    public Task StartReviewedOptionScopeAsync(string scope, string physicalDigest, Guid generation,
        string key, string host, ushort port, string[] numericDnsServers,
        NativeSubscription[] subscriptions, NativeRegistration[] registrations,
        NativeReviewedScopeReferences references, TimeSpan remainingBudget,
        OptionChainStatisticsSnapshot[]? statistics = null, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();
        if(remainingBudget<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(references.RefreshPolicy);
        // Configuration and approval are copied; native admission owns their
        // validation along with the complete pricing/reference policy.
        var reviewed = ReviewedScopePayload(references, registrations, statistics ?? []);
        return SubmitScope(7, generation, new
        {
            scope, digest = physicalDigest,
            request = ScopeRequest(key, host, port, numericDnsServers, subscriptions, registrations, true),
            reviewed
        }, Remaining(remainingBudget, started), cancellationToken);
    }

    static object ReviewedScopePayload(NativeReviewedScopeReferences references,
        NativeRegistration[] registrations, OptionChainStatisticsSnapshot[] statistics)
    {
        static long Ticks(DateTimeOffset at) => at.UtcTicks - DateTimeOffset.UnixEpoch.Ticks;
        static long Offset(DateTimeOffset at) => (long)at.Offset.TotalMinutes;
        static object[] Time(DateTimeOffset at) => [Ticks(at), Offset(at)];
        static string Amount(decimal value) => value.ToString(CultureInfo.InvariantCulture);
        static object?[] Contract(OptionPricingConvention c) =>
        [
            c.SchemaVersion,c.ContractId,c.Dataset,c.PublisherId,c.InstrumentId,c.RawSymbol,c.Root,c.Exchange,c.Currency,c.UnderlyingContractId,
            (int)c.ExerciseStyle,(int)c.SettlementStyle,Time(c.ExpirationUtc),Time(c.LastTradingUtc),(int)c.DayCount,c.CalendarVersion,
            Amount(c.Multiplier),Amount(c.TickSize),c.TickRuleVersion,c.DefinitionDigest,c.MappingVersion,c.EvidenceId,
            Time(c.EffectiveFromUtc),Time(c.EffectiveUntilUtc),(int)c.PremiumTickRule,(int)c.PremiumStyle,(int)c.UnderlyingKind,
            c.Strike is { } strike ? Amount(strike) : null,(int)c.Right
        ];
        var calendar = references.Calendar;
        var publication = references.Publication;
        var conversion = references.Conversion;
        var policy = references.RefreshPolicy;
        static int? DateNumber(DateOnly? date) => date is { } d ? d.Year * 10000 + d.Month * 100 + d.Day : null;
        return new
        {
            calendar = new { version = calendar.Version, timezone = calendar.TimeZoneId,
                from = calendar.CoverageFrom.DayNumber, until = calendar.CoverageUntil.DayNumber,
                rollover = calendar.ValueDateRollover.Ticks, dates = calendar.TradingDates.Select(d => d.DayNumber) },
            publication = new { version = publication.Version, timezone = publication.TimeZoneId,
                from = Ticks(publication.CoverageFromUtc), until = Ticks(publication.CoverageUntilUtc),
                from_offset = Offset(publication.CoverageFromUtc), until_offset = Offset(publication.CoverageUntilUtc),
                entries = publication.Publications.Select(p => new { date = p.ValueDate.DayNumber,
                    available = Ticks(p.AvailableByUtc), offset = Offset(p.AvailableByUtc) }) },
            conversion = new { source = conversion.Source, series = conversion.SourceSeriesId,
                convention = (int)conversion.Convention, version = conversion.Version, evidence = conversion.EvidenceId },
            maximum_lead = references.MaximumSourceClockLeadMilliseconds,
            production = references.Production, production_approved = policy.ProductionApproved,
            review_evidence = policy.ReviewEvidenceId,
            policy_version = policy.Version, selection_ms = policy.PriceDeltaMilliseconds,
            risk_ms = policy.FullRiskMilliseconds, iv_ms = policy.ImpliedVolatilityMilliseconds,
            batch = policy.MaximumContractsPerPass, maximum_contracts = policy.MaximumContracts,
            options = references.Options.Select(o => new { instrument = o.Contract.InstrumentId,
                underlying = o.UnderlyingInstrument, strike = DecimalWords(o.Strike), call = o.IsCall,
                required = o.RiskRequired, contract = Contract(o.Contract) }),
            statistics = statistics.SelectMany(s => registrations.Where(r => r.Option && r.Contract == s.ContractId)
                .Select(r => new { instrument = r.Instrument, volume = s.Volume, volume_date = DateNumber(s.VolumeValueDate),
                    open_interest = s.OpenInterest, open_interest_date = DateNumber(s.OpenInterestValueDate) }))
        };
    }
}
