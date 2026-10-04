using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.OptionPricer.Pricing;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>A display-only option chain for emulator use while the live feed is stopped.</summary>
internal static class FrozenEmulatorOptionChain
{
    private const double DefaultEmulatorVolatility = 0.20;
    internal static async Task<ServiceResult<EvaluatedOptionChainReadModel>> ExecuteAsync(
        GetEvaluatedOptionChainQuery query, IMarketDataQueryContext context, CancellationToken token)
    {
        var timer = Stopwatch.StartNew();
        var definitions = await context.DbFactory.SecuritiesDb.GetCachedOptionContractDefinitionsAsync(
            query.UnderlyingSymbol, query.UnderlyingContractId, query.ExpiryDate, query.ProviderRoots, token)
            .ConfigureAwait(false);
        var definitionsMs = timer.Elapsed.TotalMilliseconds;
        var inputs = await OptionChainWindowInputs.GetAsync(
            context, query.UnderlyingSymbol, query.UnderlyingContractId, token).ConfigureAwait(false);
        var window = OptionChainStrikeWindow.Select(definitions.Select(row => row.Definition),
            inputs.Price, inputs.Deviation, query.StandardDeviationMultiplier, query.RequiredContractIds);
        if (window.Contracts.Length == 0 || inputs.Price is not > 0)
            return new ServiceFailed<EvaluatedOptionChainReadModel>(503,
                "Stored underlying price or selected option definitions are unavailable for emulator preview.");
        var inputsMs = timer.Elapsed.TotalMilliseconds - definitionsMs;

        // The operational date may already be the next session's date during a weekend closure.
        // The last EOD observation identifies the session to read persisted option ticks from.
        var observationDate = inputs.ObservationDate;
        var at = DateTimeOffset.UtcNow;
        var contracts = new EvaluatedOptionContractReadModel[window.Contracts.Length];
        var storedQuotes = observationDate is { } date
            ? await context.DbFactory.MarketDataDb.GetFuturesOptionChainQuoteDataAsync(
                query.UnderlyingContractId, query.ExpiryDate, date, token).ConfigureAwait(false)
            : [];
        var byContract = storedQuotes.ToDictionary(quote => quote.ContractId, StringComparer.Ordinal);
        await Parallel.ForEachAsync(Enumerable.Range(0, contracts.Length), new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = 8
        }, async (index, cancellationToken) =>
        {
            var definition = window.Contracts[index];
            byContract.TryGetValue(definition.ContractId, out var stored);
            contracts[index] = Evaluate(definition, stored, inputs.Price.Value, inputs.Deviation, at);
        });
        // Upgrade legacy stored snapshots that lack Greeks. Conditional writes cannot replace a newer tick.
        var enriched = contracts.Where(x => x.GreeksValid && byContract.TryGetValue(x.ContractId, out var quote)
                && quote.Vega == 0 && x.Vega is > 0)
            .Select(x => byContract[x.ContractId] with { ImpliedVolatility = x.ImpliedVolatility!.Value,
                UnderlyingPrice = (double)inputs.Price.Value, Delta = x.Delta!.Value,
                Gamma = x.Gamma!.Value, Vega = x.Vega!.Value, Theta = x.Theta!.Value, Rho = x.Rho!.Value }).ToArray();
        if (enriched.Length > 0)
            await context.DbFactory.MarketDataDb.UpdateFuturesOptionChainQuoteGreeksAsync(
                query.UnderlyingContractId, query.ExpiryDate, enriched).ConfigureAwait(false);
        var evaluationMs = timer.Elapsed.TotalMilliseconds - definitionsMs - inputsMs;
        context.Logger.LogInformation(
            "Frozen emulator option chain {Expiry}: {Count} contracts, projected quotes {QuoteCount}; definitions {DefinitionsMs:F1} ms, inputs/window {InputsMs:F1} ms, bulk quotes/pricing {EvaluationMs:F1} ms, total {TotalMs:F1} ms.",
            query.ExpiryDate, contracts.Length, storedQuotes.Count,
            definitionsMs, inputsMs, evaluationMs, timer.Elapsed.TotalMilliseconds);

        var source = observationDate?.ToString("yyyy-MM-dd") ?? "stored inputs";
        var modelNote = inputs.Deviation is > 0 ? "historical/model quotes" : "historical/model quotes; 20% default IV";
        FrozenEmulatorQuoteStore.Publish(contracts, at);
        return new ServiceOk<EvaluatedOptionChainReadModel>(new(query.UnderlyingContractId,
            query.ExpiryDate, inputs.Price, window.LowerBound, window.UpperBound,
            $"Frozen emulator preview ({source}; {modelNote})", at, contracts));
    }

    internal static EvaluatedOptionContractReadModel Evaluate(FuturesOptionContractReadModel definition,
        FuturesOptionTickDataV2ReadModel? stored, decimal underlyingPrice, decimal? dailyDeviation,
        DateTimeOffset at)
    {
        var strike = definition.GetExactStrikePrice();
        var isCall = definition.OptionType.StartsWith("C", StringComparison.OrdinalIgnoreCase);
        var expiry = definition.ExpirationUtc ?? new DateTimeOffset(
            definition.ContractMonth.ToDateTime(new TimeOnly(23, 59)), TimeSpan.Zero);
        var years = Math.Max((expiry - at).TotalDays / 365.25, 0);
        var observedQuote = stored is { BidPrice: > 0, AskPrice: > 0 }
            && stored.AskPrice >= stored.BidPrice;
        var volatility = stored is { ImpliedVolatility: > 0 and < 4 } ? stored.ImpliedVolatility
            : dailyDeviation is > 0
                ? Math.Clamp((double)(dailyDeviation.Value / underlyingPrice) * Math.Sqrt(252), 0.05, 2.0)
                : DefaultEmulatorVolatility;
        var calculator = new OptionCalculator();
        var request = new OptionPricingRequest(UnderlyingKind.Futures,
            definition.ExerciseStyle == ReferenceExerciseStyle.American ? ExerciseKind.American : ExerciseKind.European,
            definition.PremiumStyle == ReferencePremiumStyle.FuturesStyleVariation ? PremiumKind.FuturesStyle : PremiumKind.PaidUpfront,
            isCall ? OptionSide.Call : OptionSide.Put, (double)underlyingPrice,
            (double)strike, years, 0);
        if (years > 0 && observedQuote && stored is not { ImpliedVolatility: > 0 and < 4 })
        {
            var implied = calculator.ImpliedVolatility(request,
                (stored!.BidPrice + stored.AskPrice) / 2);
            if (implied.Success) volatility = implied.Value!.Value.Volatility;
        }
        var hasStoredGreeks = stored is { Vega: > 0 } && double.IsFinite(stored.Vega)
            && double.IsFinite(stored.Delta) && double.IsFinite(stored.Gamma)
            && double.IsFinite(stored.Theta) && double.IsFinite(stored.Rho);
        var greeks = !hasStoredGreeks && years > 0 && double.IsFinite(volatility)
            ? calculator.Price(request, volatility) : default;
        var result = years > 0 && double.IsFinite(volatility)
            ? calculator.PriceAndDelta(request, volatility)
            : default;
        var model = result.Success ? result.Value : null;
        decimal? bid = observedQuote ? (decimal)stored!.BidPrice : null;
        decimal? ask = observedQuote ? (decimal)stored!.AskPrice : null;
        if (!observedQuote && model is { Price: > 0 } value)
        {
            var tick = definition.TickSize is > 0 ? definition.TickSize.Value : 0.25m;
            var midpoint = (decimal)value.Price;
            bid = Math.Max(tick, Math.Floor(midpoint / tick) * tick);
            ask = Math.Max(bid.Value + tick, Math.Ceiling(midpoint / tick) * tick);
        }
        return new(definition.ContractId, strike, isCall, bid, ask,
            observedQuote ? (uint)Math.Max(stored!.BidSize, 0) : null,
            observedQuote ? (uint)Math.Max(stored!.AskSize, 0) : null,
            stored is { OptionPrice: > 0 } ? (decimal)stored.OptionPrice : null, null,
            double.IsFinite(volatility) ? volatility : null, model?.Price,
            hasStoredGreeks ? stored!.Delta : greeks.Value?.Delta ?? model?.Delta,
            hasStoredGreeks ? stored!.Gamma : greeks.Value?.Gamma,
            hasStoredGreeks ? stored!.Vega : greeks.Value?.Vega,
            hasStoredGreeks ? stored!.Theta : greeks.Value?.Theta,
            hasStoredGreeks ? stored!.Rho : greeks.Value?.Rho, stored?.Volume, stored?.OpenInterest,
            hasStoredGreeks || greeks.Success, true, stored is { TickId: > 100000000000000000 } ? DateTimeOffset.UnixEpoch.AddTicks(stored.TickId / 100) : null, null, model is null ? null : at, stored?.VolumeValueDate, stored?.OpenInterestValueDate);
    }
}
