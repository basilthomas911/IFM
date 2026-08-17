using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

/// <summary>
/// Establishes the small, authoritative Development data baseline required by G0.
/// Data enters through the public NATS command API and is verified through public queries.
/// </summary>
public static class G0DevelopmentDataFixture
{
    const int WindowSize = 20;
    const int MinimumChartBars = 2;

    public static async Task<G0DevelopmentSeedResult> EnsureAsync(
        G0QuerySession session,
        FuturesContractV2ReadModel contract,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var valueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var eodSeeded = await EnsureEodAsync(session, contract, valueDate, timeout, cancellationToken);
        var barSeeded = await EnsureBarCoreAsync(session, contract, valueDate, timeout, cancellationToken);
        return new G0DevelopmentSeedResult(valueDate, eodSeeded, barSeeded);
    }

    /// <summary>
    /// Ensures a current bar exists for a secondary displayed contract without changing its EOD state.
    /// </summary>
    public static Task<bool> EnsureBarAsync(
        G0QuerySession session,
        FuturesContractV2ReadModel contract,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => EnsureBarCoreAsync(
            session,
            contract,
            DateOnly.FromDateTime(DateTime.UtcNow),
            timeout,
            cancellationToken);

    static async Task<bool> EnsureEodAsync(
        G0QuerySession session,
        FuturesContractV2ReadModel contract,
        DateOnly valueDate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var existing = await session.MarketDataFeed
            .GetLastFuturesEodDataAsync(contract.ContractId, contract.LastTradeDate)
            .WaitAsync(timeout, cancellationToken);
        if (existing.Success && existing.Value is not null)
            return false;

        var range = Enumerable.Range(0, WindowSize)
            .Select(offset => CreateEod(contract, valueDate.AddDays(-offset), 5400m - offset * 5m))
            .ToArray();
        var tick = new FuturesTickDataV2ReadModel(
            contract.ContractId,
            valueDate,
            tickId: 1,
            tickTime: new TimeOnly(12, 0),
            price: range[0].ClosePrice,
            size: 100);
        var normalCurve = new NormalCurveTableReadModel([new NormalCurveDataReadModel(0, 50)]);
        var response = await session.MarketDataFeedCommands.InsertFuturesEodDataAsync(
                valueDate,
                tick,
                contract,
                range[0],
                range,
                normalCurve,
                WindowSize,
                Array.Empty<VixFuturesEodDataReadModel>())
            .WaitAsync(timeout, cancellationToken);
        if (!response.Success)
            throw new InvalidOperationException($"G0 EOD seed command failed: {response.ErrorMessage}");

        await WaitUntilAsync(
            async () =>
            {
                var result = await session.MarketDataFeed
                    .GetLastFuturesEodDataAsync(contract.ContractId, contract.LastTradeDate)
                    .WaitAsync(timeout, cancellationToken);
                return result.Success && result.Value is not null;
            },
            timeout,
            "G0 EOD seed was accepted but did not become durable.",
            cancellationToken);
        return true;
    }

    static async Task<bool> EnsureBarCoreAsync(
        G0QuerySession session,
        FuturesContractV2ReadModel contract,
        DateOnly valueDate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var endDate = DateTime.UtcNow.AddSeconds(1);
        var startDate = endDate.AddHours(-6);
        var existing = await session.MarketDataFeed
            .GetFuturesBarDataAsync(
                contract.ContractId,
                contract.Symbol,
                valueDate,
                startDate,
                endDate)
            .WaitAsync(timeout, cancellationToken);
        if (!existing.Success)
            throw new InvalidOperationException($"G0 bar baseline query failed: {existing.ErrorMessage}");
        var bars = existing.Value ?? [];
        var usableBars = bars
            .Where(bar => bar.BarRateType == BarRateType.FifteenSeconds
                          && IsExpectedDevelopmentScale(contract.Symbol, bar.BarValue))
            .OrderBy(bar => bar.BarDate)
            .TakeLast(MinimumChartBars)
            .ToArray();
        if (usableBars.Length >= MinimumChartBars)
            return false;

        var timestamps = bars.Select(static bar => bar.BarDate).ToHashSet();
        var reference = usableBars.OrderBy(static bar => bar.BarDate).LastOrDefault();
        for (var index = usableBars.Length; index < MinimumChartBars; index++)
        {
            var barDate = endDate.AddSeconds(-15 * (MinimumChartBars - index));
            while (!timestamps.Add(barDate))
                barDate = barDate.AddSeconds(-1);
            var baseValue = reference?.BarValue ?? (contract.Symbol == "VX" ? 20m : 5400m);
            var increment = contract.Symbol == "VX" ? 0.05m : 2m;
            var bar = new FuturesBarDataReadModel(
                contract.ContractId,
                contract.Symbol,
                valueDate,
                barDate,
                BarRateType.FifteenSeconds,
                barValue: baseValue + increment * (index + 1),
                upTrendTrigger: reference?.UpTrendTrigger ?? 0.65,
                downTrendTrigger: reference?.DownTrendTrigger ?? 0.35);
            var response = await session.MarketDataFeedCommands.InsertFuturesBarDataAsync(bar)
                .WaitAsync(timeout, cancellationToken);
            if (!response.Success)
                throw new InvalidOperationException($"G0 bar seed command failed: {response.ErrorMessage}");
        }

        await WaitUntilAsync(
            async () =>
            {
                var result = await session.MarketDataFeed
                    .GetFuturesBarDataAsync(
                        contract.ContractId,
                        contract.Symbol,
                        valueDate,
                        startDate,
                        DateTime.UtcNow.AddSeconds(1))
                    .WaitAsync(timeout, cancellationToken);
                return result.Success
                       && result.Value?.Count(bar =>
                           bar.BarRateType == BarRateType.FifteenSeconds
                           && IsExpectedDevelopmentScale(contract.Symbol, bar.BarValue)) >= MinimumChartBars;
            },
            timeout,
            $"G0 chart baseline did not reach {MinimumChartBars} durable bars.",
            cancellationToken);
        return true;
    }

    static bool IsExpectedDevelopmentScale(string symbol, decimal value)
        => symbol switch
        {
            "ES" => value is >= 1_000m and <= 10_000m,
            "VX" => value is >= 5m and <= 200m,
            _ => value > 0m
        };

    static FuturesEodDataV2ReadModel CreateEod(
        FuturesContractV2ReadModel contract,
        DateOnly valueDate,
        decimal close)
        => new(
            contract.ContractId,
            valueDate,
            contract.Symbol,
            openPrice: close - 10m,
            highPrice: close + 15m,
            lowPrice: close - 20m,
            closePrice: close,
            volume: 100_000,
            dailyPercentChange: 0.2,
            dailyStdDev: 0.01,
            dailyStdDevAmount: 50,
            upperBand: (double)(close + 100m),
            mean: (double)close,
            lowerBand: (double)(close - 100m),
            marketDirection: MarketDirectionType.NeutralUp,
            marketVolatility: MarketVolatilityType.Normal,
            priceDirection: PriceDirectionType.Rising,
            priceVolatility: PriceVolatilityType.Falling,
            windowSize: WindowSize);

    static async Task WaitUntilAsync(
        Func<Task<bool>> predicate,
        TimeSpan timeout,
        string timeoutMessage,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        while (!timeoutSource.IsCancellationRequested)
        {
            if (await predicate())
                return;
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
        throw new TimeoutException(timeoutMessage);
    }
}

public sealed record G0DevelopmentSeedResult(DateOnly ValueDate, bool EodSeeded, bool BarSeeded);
