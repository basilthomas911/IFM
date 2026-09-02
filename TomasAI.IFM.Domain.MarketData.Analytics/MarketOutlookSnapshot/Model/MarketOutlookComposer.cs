using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using CacheComponentType = TomasAI.IFM.Application.MarketData.MarketOutlook.MarketOutlookComponentType;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model;

/// <summary>Pure versionless Market Outlook hot-cache composer.</summary>
public static class MarketOutlookComposer
{
    public static MarketOutlookReadModel Compose(
        MarketOutlookInputState state,
        MarketOutlookRefreshTrigger trigger,
        DateTime updatedAtUtc,
        FuturesEmaSignalReadModel? liveEma = null,
        FuturesBbSignalReadModel? liveBb = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var entityId = state.EntityId;
        var ema = liveEma ?? state.FuturesEmaSignal;
        var bb = liveBb ?? state.FuturesBbSignal;
        var priceVolatility = MarketOutlookPriceVolatilityClassifier.Classify(
            state.VixFuturesSessionOpenPrice,
            state.VixFuturesPrice);
        var eod = ApplyLivePrice(
            state.FuturesEodData,
            entityId,
            state.CurrentEsPrice,
            bb,
            priceVolatility);
        var missing = MissingInputs(eod, state, ema, bb);
        var tradeSignal = ComputeTradeSignal(eod, state) ?? state.FuturesTradeSignal;
        return new MarketOutlookReadModel
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            UpdatedAtUtc = DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc),
            MarketDataAsOfUtc = state.MarketDataAsOfUtc,
            RefreshTrigger = trigger,
            FuturesEodData = eod,
            FuturesTradeSignal = tradeSignal,
            MissingInputs = string.Join(", ", missing),
            FuturesRsiSignal = state.FuturesRsiSignal,
            FuturesTdiSignal = state.FuturesTdiSignal,
            TrendDirectionChange = state.TrendDirectionChange,
            TrendExtremeChange = state.TrendExtremeChange,
            TrendReversalChange = state.TrendReversalChange,
            LatestItiTrendSignal = state.LatestItiTrendSignal,
            VixFuturesPrice = state.VixFuturesPrice,
            FuturesEmaSignal = ema,
            FuturesBbSignal = bb,
            EsPriceAvailability = Availability(
                state, CacheComponentType.EsTrade, state.CurrentEsPrice > 0, true, updatedAtUtc),
            RsiAvailability = state.FuturesRsiSignal switch
            {
                { IsWarm: true, RSI: >= 0d } => Availability(
                    state, CacheComponentType.Rsi, true, true, updatedAtUtc),
                null => MarketOutlookInputAvailability.Unavailable,
                _ => MarketOutlookInputAvailability.Warming
            },
            TdiAvailability = state.FuturesTdiSignal is null
                ? MarketOutlookInputAvailability.Warming
                : Availability(state, CacheComponentType.Tdi, true, true, updatedAtUtc),
            ItiAvailability = state.LatestItiTrendSignal is null
                ? MarketOutlookInputAvailability.Unavailable
                : Availability(state, CacheComponentType.ItiLatest, true, true, updatedAtUtc),
            VxAvailability = state.VixFuturesPrice > 0
                ? Availability(state, CacheComponentType.Vx, true, true, updatedAtUtc)
                : MarketOutlookInputAvailability.Unavailable,
            DailyAnalyticsAvailability = ema is { IsWarm: true } && bb is { IsWarm: true }
                ? MarketOutlookInputAvailability.Available
                : ema is null && bb is null
                    ? MarketOutlookInputAvailability.Unavailable
                    : MarketOutlookInputAvailability.Warming,
            FeedHealth = FeedHealth(state, updatedAtUtc),
            FeedHealthReason = FeedHealthReason(state)
        };
    }

    static MarketOutlookInputAvailability Availability(
        MarketOutlookInputState state,
        CacheComponentType component,
        bool present,
        bool valid,
        DateTime nowUtc)
    {
        if (!present) return MarketOutlookInputAvailability.Unavailable;
        if (!valid) return MarketOutlookInputAvailability.Invalid;
        if (!state.Positions.TryGetValue(component, out var position))
            return MarketOutlookInputAvailability.Available;
        return nowUtc - position.SourceTimestampUtc > TimeSpan.FromMinutes(15)
            ? MarketOutlookInputAvailability.Stale
            : MarketOutlookInputAvailability.Available;
    }

    static string FeedHealth(MarketOutlookInputState state, DateTime nowUtc)
    {
        if (!string.Equals(state.FeedHealth, "Unavailable", StringComparison.OrdinalIgnoreCase))
            return state.FeedHealth;

        if (!state.Positions.TryGetValue(CacheComponentType.EsTrade, out var position))
            return "Unavailable";
        var age = nowUtc - position.SourceTimestampUtc;
        return age <= TimeSpan.FromMinutes(5)
            ? "Green"
            : age <= TimeSpan.FromMinutes(15) ? "Yellow" : "Red";
    }

    static string FeedHealthReason(MarketOutlookInputState state)
    {
        if (!string.IsNullOrWhiteSpace(state.FeedHealthReason))
            return state.FeedHealthReason;
        return string.Equals(state.FeedHealth, "Unavailable", StringComparison.OrdinalIgnoreCase)
            ? "Interim status inferred from the most recent ES receipt until native watchdog health is supplied."
            : string.Empty;
    }

    static FuturesEodDataV2ReadModel ApplyLivePrice(
        FuturesEodDataV2ReadModel? source,
        MarketOutlookEntityId entityId,
        decimal? currentPrice,
        FuturesBbSignalReadModel? bb,
        PriceVolatilityType priceVolatility)
    {
        var eod = source ?? new FuturesEodDataV2ReadModel
        {
            ContractId = entityId.ContractId,
            ValueDate = entityId.ValueDate,
            Symbol = "ES"
        };
        if (currentPrice is not > 0)
            return eod with { PriceVolatility = priceVolatility };
        var price = currentPrice.Value;
        var open = eod.OpenPrice;
        var change = open > 0 ? (double)((price - open) / open) : 0d;
        var mdi = bb?.Position20 is { } position
            ? (double)Math.Clamp(position * 100m, 0m, 100m)
            : eod.MarketDirectionIndicator;
        return eod with
        {
            ClosePrice = price,
            HighPrice = eod.HighPrice <= 0 ? price : Math.Max(eod.HighPrice, price),
            LowPrice = eod.LowPrice <= 0 ? price : Math.Min(eod.LowPrice, price),
            DailyPercentChange = change,
            MarketDirectionIndicator = mdi,
            PriceVolatility = priceVolatility
        };
    }

    static FuturesTradeSignalV2ReadModel? ComputeTradeSignal(
        FuturesEodDataV2ReadModel eod,
        MarketOutlookInputState state)
    {
        if (!eod.IsValid)
            return null;
        var command = new UpdateFuturesTradeSignalCommand(
            eod,
            state.FuturesRsiSignal,
            state.FuturesTdiSignal,
            new FuturesItiSignalDataReadModel(
                state.TrendDirectionChange,
                state.TrendExtremeChange,
                state.TrendReversalChange),
            state.VixFuturesPrice ?? 0m,
            FuturesTradeSignalPrerequisites.SignalTimePeriod);
        return command.Compute(out FuturesTradeSignalCompute compute)
            ? compute.FuturesTradeSignal
            : null;
    }

    static List<string> MissingInputs(
        FuturesEodDataV2ReadModel eod,
        MarketOutlookInputState state,
        FuturesEmaSignalReadModel? ema,
        FuturesBbSignalReadModel? bb)
    {
        List<string> missing = [];
        if (!eod.IsValid) missing.Add("EOD");
        if (state.FuturesRsiSignal is not { IsWarm: true, RSI: >= 0d }) missing.Add("RSI warming");
        if (state.LatestItiTrendSignal is null) missing.Add("ITI trend");
        if (state.VixFuturesPrice is not > 0) missing.Add("VX price");
        if (ema is not { IsWarm: true }) missing.Add("EMA");
        if (bb is not { IsWarm: true }) missing.Add("Bollinger Bands");
        return missing;
    }
}
