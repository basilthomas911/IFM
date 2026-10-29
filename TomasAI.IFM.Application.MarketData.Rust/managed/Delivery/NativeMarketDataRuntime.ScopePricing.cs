using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Interop;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.OptionPricing;
using TomasAI.IFM.Framework.Serialization;
namespace TomasAI.IFM.Application.MarketData.Rust.Delivery;

/// <summary>One frozen application-qualified context. QualifiedUntilUtc bounds
/// all reference/calendar assumptions, including earlier session/tenor cutovers.
/// Context identity changes whenever any immutable assumption changes.</summary>
public sealed record PreparedOptionPricingContext(Guid Identity, OptionPricingContext Context, uint UnderlyingInstrument,
    decimal Strike, bool IsCall, bool RiskRequired, DateTimeOffset QualifiedUntilUtc);

/// <summary>Whole-batch compare-and-swap input for a reference refresh.</summary>
public sealed record PreparedOptionContextRefresh(Guid ExpectedIdentity, PreparedOptionPricingContext Next);

/// <summary>One context-fenced member of the complete business/display risk
/// demand union. Changing demand never changes provider subscriptions.</summary>
public sealed record PreparedOptionRiskDemand(uint Instrument, Guid ContextIdentity, bool Required);

public sealed partial class NativeMarketDataRuntime
{
    /// <summary>Atomically replaces the complete risk demand union under the
    /// existing immutable pricing digest and contexts. Completion acknowledges
    /// control admission; callers still require fresh risk evidence for readiness.</summary>
    public Task SetOptionScopeRiskRequirementsAsync(string scope,Guid generation,string expectedPricingDigest,
        PreparedOptionRiskDemand[] demands,TimeSpan remainingBudget,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(demands);ArgumentException.ThrowIfNullOrWhiteSpace(expectedPricingDigest);
        if(generation==Guid.Empty||expectedPricingDigest.Length!=64||!expectedPricingDigest.All(Uri.IsHexDigit)
            ||demands.Length is <1 or >2048||demands.Any(x=>x is null||x.Instrument==0||x.ContextIdentity==Guid.Empty)
            ||demands.Select(x=>x.Instrument).Distinct().Count()!=demands.Length)throw new ArgumentException("Complete qualified risk demand is required.",nameof(demands));
        return SubmitScope(6,generation,new{scope,pricing=new{expected_digest=expectedPricingDigest,
            options=demands.Select(x=>new{instrument=x.Instrument,identity=IdentityWords(x.ContextIdentity),required=x.Required})}},remainingBudget,cancellationToken);
    }
    static ulong[] IdentityWords(Guid identity) { var value = GuidBits(identity); return [value.Low, value.High]; }
    static uint[] DecimalWords(decimal value) => decimal.GetBits(value).Select(word => unchecked((uint)word)).ToArray();
    static Dictionary<string, object> PreparedContext(PreparedOptionPricingContext entry, Guid generation, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(entry); ArgumentNullException.ThrowIfNull(entry.Context);
        var context = entry.Context; var contract = context.Contract;
        var invalid = Black76PricingModel.ValidateContext(context, at);
        if (invalid is not null) throw new InvalidOperationException($"Option context qualification failed: {invalid.Code}; {invalid.Input}.");
        if (entry.Identity == Guid.Empty || entry.UnderlyingInstrument == 0 || context.GenerationId != generation || entry.Strike <= 0
            || entry.QualifiedUntilUtc.Offset != TimeSpan.Zero || entry.QualifiedUntilUtc <= at || entry.QualifiedUntilUtc > context.ValidUntilUtc
            || entry.QualifiedUntilUtc > contract.EffectiveUntilUtc || entry.QualifiedUntilUtc > contract.ExpirationUtc
            || entry.QualifiedUntilUtc > contract.LastTradingUtc
            || contract.SchemaVersion == 3 && (contract.Strike != entry.Strike || (contract.Right == PricingOptionRight.Call) != entry.IsCall))
            throw new ArgumentException("Prepared option context identity, definition, generation or qualified window is invalid.", nameof(entry));
        var request = Black76PricingModel.CreateRequest(contract, 1, entry.Strike, entry.IsCall, 1, context.Rate.AnnualContinuousRate);
        var manifest=MessagePackBinarySerializer.Shared.Serialize(new WorkerOptionDefinition(context,entry.Strike,entry.IsCall))!;
        if(manifest.Length is 0 or >4*1024*1024)throw new ArgumentException("Trade context manifest exceeds its binary capacity.",nameof(entry));
        return new()
        {
            ["instrument"] = contract.InstrumentId, ["identity"] = IdentityWords(entry.Identity), ["generation"] = IdentityWords(generation),
            ["manifest"] = Convert.ToBase64String(manifest),
            ["underlying"] = entry.UnderlyingInstrument, ["required"] = entry.RiskRequired,
            ["expiration"] = contract.ExpirationUtc.UtcTicks - DateTimeOffset.UnixEpoch.Ticks,
            ["valid_until"] = entry.QualifiedUntilUtc.UtcTicks - DateTimeOffset.UnixEpoch.Ticks,
            ["days_in_year"] = contract.DayCount == PricingDayCount.Actual365Fixed ? 365 : 360,
            ["flags"] = 3u | (contract.SchemaVersion == 3 ? 4u : 0) | (entry.IsCall ? 8u : 0) | (contract.Right == PricingOptionRight.Call ? 16u : 0),
            ["maximum_age"] = context.MaximumQuoteAgeMilliseconds, ["maximum_skew"] = context.MaximumQuoteSkewMilliseconds,
            ["maximum_lead"] = context.MaximumSourceClockLeadMilliseconds,
            ["strike"] = DecimalWords(entry.Strike), ["definition_strike"] = DecimalWords(contract.Strike ?? 0),
            ["strike_double"] = request.Strike, ["rate"] = request.Rate,
            ["american"] = request.Exercise == ExerciseKind.American, ["futures_style"] = request.Premium == PremiumKind.FuturesStyle
        };
    }

    /// <summary>Starts a provider scope with qualified pricing installed before
    /// the first quote. Validation is application policy; all transport, source
    /// ordering, scheduling, IV/Greek work and cached calculations run in Rust.
    /// Caller supplies API time and the original remaining operation budget.</summary>
    public Task StartPricedOptionScopeAsync(string scope, string physicalDigest, Guid generation, string key, string host, ushort port,
        string[] numericDnsServers, NativeSubscription[] subscriptions, NativeRegistration[] registrations, bool requireObservations,
        string pricingDigest, PreparedOptionPricingContext[] contexts, OptionPricingRefreshPolicy policy, bool production,
        DateTimeOffset qualifiedAt, TimeSpan remainingBudget, OptionChainStatisticsSnapshot[]? statistics=null,CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp(); cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(policy); policy.Validate(); if (production) policy.RequireProductionApproval();
        if(StrictUtf8.GetByteCount(policy.Version)>1024)throw new ArgumentException("Refresh policy identity exceeds native UTF-8 capacity.",nameof(policy));
        ArgumentNullException.ThrowIfNull(contexts); ArgumentNullException.ThrowIfNull(registrations); ArgumentNullException.ThrowIfNull(subscriptions);
        if (remainingBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if (generation == Guid.Empty || contexts.Length is < 1 or > 2048 || contexts.Length > policy.MaximumContracts || qualifiedAt.Offset != TimeSpan.Zero
            || registrations.Length is < 1 or > 2112 || registrations.Any(r=>r.Publisher==0)
            || subscriptions.Length is < 1 or > 64 || string.IsNullOrWhiteSpace(scope) || scope.Length > 128)
            throw new ArgumentException("Invalid qualified options scope.");
        var prepared = new List<Dictionary<string, object>>(contexts.Length);
        foreach (var entry in contexts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value=PreparedContext(entry,generation,qualifiedAt);
            if (!registrations.Any(r => r.Option && r.Instrument == entry.Context.Contract.InstrumentId && r.Publisher == entry.Context.Contract.PublisherId && r.Contract == entry.Context.Contract.ContractId)
                || !registrations.Any(r => !r.Option && r.Instrument == entry.UnderlyingInstrument && r.Contract == entry.Context.Contract.UnderlyingContractId))
                throw new ArgumentException("Qualified pricing does not match the physical canonical registrations.", nameof(contexts));
            prepared.Add(value);
        }
        if(statistics?.Length>2048)throw new ArgumentOutOfRangeException(nameof(statistics));
        static int? DateNumber(DateOnly? date)=>date is{}d?d.Year*10000+d.Month*100+d.Day:null;
        var saved=(statistics??[]).SelectMany(value=>registrations.Where(r=>r.Option&&r.Contract==value.ContractId).Select(r=>new{
            instrument=r.Instrument,publisher=r.Publisher,volume=value.Volume,volume_date=DateNumber(value.VolumeValueDate),open_interest=value.OpenInterest,
            open_interest_date=DateNumber(value.OpenInterestValueDate)})).ToArray();
        var payload = new { scope, digest = physicalDigest, request = ScopeRequest(key, host, port, numericDnsServers, subscriptions, registrations, requireObservations),
            pricing = new { digest = pricingDigest, trade_manifests=true, options = prepared, selection_ms = policy.PriceDeltaMilliseconds,
                risk_ms = policy.FullRiskMilliseconds, iv_ms = policy.ImpliedVolatilityMilliseconds, batch = policy.MaximumContractsPerPass,policy_version=policy.Version,statistics=saved } };
        return SubmitScope(0, generation, payload, Remaining(remainingBudget, started), cancellationToken);
    }
    /// <summary>Atomically refreshes every immutable scope context. Conflicting
    /// identities or physical redefinitions leave the prior batch intact, with no
    /// reconnect. Current quotes are retained; prior IV/risk is invalidated.</summary>
    public Task RefreshOptionScopePricingAsync(string scope, Guid generation, string expectedDigest, string nextDigest,
        PreparedOptionContextRefresh[] contexts, DateTimeOffset qualifiedAt, TimeSpan remainingBudget, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp(); cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(contexts);
        if (remainingBudget < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(remainingBudget));
        if (contexts.Length is < 1 or > 2048 || generation == Guid.Empty || qualifiedAt.Offset != TimeSpan.Zero) throw new ArgumentException("Invalid context refresh.");
        var prepared = new List<Dictionary<string, object>>(contexts.Length);
        foreach (var entry in contexts)
        {
            cancellationToken.ThrowIfCancellationRequested(); var value = PreparedContext(entry.Next, generation, qualifiedAt);
            value["expected"] = IdentityWords(entry.ExpectedIdentity); prepared.Add(value);
        }
        return SubmitScope(5, generation, new { scope, pricing = new { expected_digest = expectedDigest, digest = nextDigest, trade_manifests=true, options = prepared } },
            Remaining(remainingBudget, started), cancellationToken);
    }
    static TimeSpan Remaining(TimeSpan budget, long started) => TimeSpan.FromTicks(Math.Max(0,
        Math.Min(budget.Ticks, TimeSpan.FromMinutes(2).Ticks) - Stopwatch.GetElapsedTime(started).Ticks));
    static object ScopeRequest(string key, string host, ushort port, string[] dns, NativeSubscription[] subscriptions, NativeRegistration[] registrations, bool observations) => new
    {
        key, host, port, dns, evidence = observations ? "observations" : "control",
        subscriptions = subscriptions.Select(s => new { symbols = s.Symbols, schema = s.Schema, symbology = s.Symbology, replay_start_ns = s.ReplayStartNanoseconds }),
        registrations = registrations.Select(r => new { instrument = r.Instrument, publisher = r.Publisher, contract = r.Contract, source = r.Source, asset = r.Option ? "option" : "futures", tick = r.Tick, vwap = r.Vwap, required = r.Required })
    };
    internal unsafe NativeScopeCalculation ReadScopeCalculation(string scope, Guid generation, uint instrument, bool risk)
    {
        Span<byte> bytes = stackalloc byte[512]; var length = ScopeBytes(scope, bytes); var expected = GuidBits(generation);
        lock (gate)
        {
            EnsureLiveAccess(); NativeScopeCalculation result;
            fixed (byte* name = bytes) NativeMarketDataLibrary.Check(library.ScopeCalculation(handle.Identity, name, (uint)length, expected.Low, expected.High,
                instrument, risk ? 1u : 0u, 640, out result), "read native scope calculation");
            if (result.Size != 640 || result.Flags > 31 || result.Reserved != 0 || result.FailureKind > 6 || result.FailureInput > 1
                || result.Flags != 0 && (result.Flags & 1) == 0 || (result.Flags & 6) == 6)
                throw new InvalidDataException("Malformed native cached pricing record.");
            return result;
        }
    }
}
