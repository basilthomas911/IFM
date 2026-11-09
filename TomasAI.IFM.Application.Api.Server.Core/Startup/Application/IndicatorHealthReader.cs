using System.Collections.Concurrent;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Application.Api.Server.Core.Startup.Application;

/// <summary>Checks persisted observations and restores their consumer caches without replaying or resetting healthy actors.</summary>
public interface IIndicatorHealthReader
{
    /// <summary>Returns actual producer and consumer qualification for the active session.</summary>
    Task<IndicatorHealthEntry[]> ReadAsync(DateOnly date, CancellationToken token);
}

/// <summary>Reads actual durable observations and checks the same effective configuration as workflow admission.</summary>
public sealed class IndicatorHealthReader(IDbContextFactory databases, IMarketDataApi market,
    IMarketSessionCalendar calendar, TimeProvider clock, IRegimeDiscoveryMarketSignalSnapshotProvider snapshots,
    IParameterRuntimeSnapshot? parameters = null, StoredSessionIndicatorWarmup? warmup = null) : IIndicatorHealthReader
{
    /// <summary>Reads every automatically attached ES interval plus session VWAP and VX dependencies with bounded concurrency.</summary>
    public async Task<IndicatorHealthEntry[]> ReadAsync(DateOnly date, CancellationToken token)
    {
        if (!market.TryGetOnTheRunFuturesContract("ES", out var contract))
            return [new("ES", TimeFrameType.None, "Contract", false, false, null, null, "Current ES contract unavailable")];
        var now = clock.GetUtcNow();
        var rows = new ConcurrentBag<IndicatorHealthEntry>();
        await Parallel.ForEachAsync(IndicatorHealthRegistry.IntradayFrames.Append(TimeFrameType.Daily),
            new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = token }, async (frame, ct) =>
            {
                var expected = IndicatorHealthRegistry.MinimumSourceTime(frame, now, calendar);
                var db = databases.MarketDataDb;
                // Sequential reads inside three independent frames bound the connection count.
                var adx = await db.GetLastFuturesAdxSignalAsync(contract.ContractId, date, frame, 14, ct).ConfigureAwait(false);
                if (adx is { IsWarm: true, Metadata.IsValid: true }) RegimeDiscoverySignalCacheAdapter.Publish(adx);
                Add("ADX14/DI", adx?.IsWarm == true, adx?.Metadata?.IsValid == true, adx?.Metadata?.MarketDataAsOfUtc);
                var atrDate = frame == TimeFrameType.Daily ? RsiHistoricalSeedWindowModel.Create(frame, 1, now.AddSeconds(-30), calendar)[0].ValueDate : date;
                var atr = await db.GetLastFuturesAtrSignalAsync(contract.ContractId, atrDate, frame, 14, ct).ConfigureAwait(false);
                if (atr is { IsWarm: true, AtrRatio: > 0, Metadata.IsValid: true }) RegimeDiscoverySignalCacheAdapter.Publish(atr);
                Add("ATR14/Baseline", atr?.IsWarm == true, atr?.Metadata?.IsValid == true && atr.AtrRatio is > 0, atr?.Metadata?.MarketDataAsOfUtc);
                var macd = await db.GetLastFuturesMacdSignalAsync(contract.ContractId, date, frame, 9, 12, 26, ct).ConfigureAwait(false);
                if (macd is { IsWarm: true, Metadata.IsValid: true }) RegimeDiscoverySignalCacheAdapter.Publish(macd);
                Add("MACD9/12/26", macd?.IsWarm == true, macd?.Metadata?.IsValid == true, macd?.Metadata?.MarketDataAsOfUtc);
                foreach (var period in new[] { 13, 14 })
                {
                    var rsi = await db.GetLastFuturesRsiSignalAsync(contract.ContractId, date, frame, period, ct).ConfigureAwait(false);
                    if (rsi is { IsWarm: true, Metadata.IsValid: true }) RegimeDiscoverySignalCacheAdapter.Publish(rsi);
                    Add("RSI" + period, rsi?.IsWarm == true, rsi?.Metadata?.IsValid == true, rsi?.Metadata?.MarketDataAsOfUtc);
                }
                if (FuturesTdiConfiguration.IsSupportedIntraday(frame))
                {
                    var tdi = await db.GetLastFuturesTdiSignalAsync(contract.ContractId, date, frame, FuturesTdiConfiguration.StandardConfigurationId, ct).ConfigureAwait(false);
                    if (tdi is { SchemaVersion: >= 2 }) RegimeDiscoverySignalCacheAdapter.Publish(tdi);
                    Add("TDI", tdi is not null, tdi is { SchemaVersion: >= 2 }, tdi is null ? null : new DateTimeOffset(DateTime.SpecifyKind(tdi.SourceEventTimestamp, DateTimeKind.Utc)));
                }
                var liveSeries = frame == TimeFrameType.Daily ? MarketSeriesIdentity.ForContract(contract.ContractId) : StoredSessionIndicatorWarmup.LiveIndicatorSeries(contract.ContractId);
                var bb = await db.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(liveSeries, date, frame, ct).ConfigureAwait(false);
                // The live continuous-series producer carries the active contract alias.
                if (RegimeDiscoverySignalCacheAdapter.TryGetLatestBb(contract.ContractId, frame, out var live)
                    && live.Metadata.SignalKey.MarketSeriesIdentity == liveSeries
                    && (bb is null || live.Metadata.MarketDataAsOfUtc > bb.Metadata.MarketDataAsOfUtc)) bb = live;
                if (bb is { IsWarm: true, Metadata.IsValid: true }) RegimeDiscoverySignalCacheAdapter.Publish(bb);
                Add("Bollinger20/Baseline", bb?.IsWarm == true, bb is { Metadata.IsValid: true, Width20Ratio: > 0, Position20: not null }, bb?.Metadata.MarketDataAsOfUtc);
                // The durable band projection contains its same-observation EMA
                // centerline. A volatile EMA cache may be empty until the next
                // four-hour bar even though that committed dependency is warm.
                Add("Bollinger EMA20 centerline", bb?.Ema20Center is not null,
                    bb is { Metadata.IsValid: true, Ema20Center: not null }, bb?.Metadata.MarketDataAsOfUtc);

                void Add(string name, bool warm, bool valid, DateTimeOffset? source)
                    => rows.Add(IndicatorHealthRegistry.Evaluate(contract.ContractId, frame, name, warm, valid, source, expected));
            }).ConfigureAwait(false);

        var dailyMinimum = IndicatorHealthRegistry.MinimumSourceTime(TimeFrameType.Daily, now, calendar);
        RegimeDiscoverySignalCacheAdapter.TryGetLatestEma(contract.ContractId, TimeFrameType.Daily, out var ema);
        rows.Add(IndicatorHealthRegistry.Evaluate(contract.ContractId, TimeFrameType.Daily, "EMA20/50/200/Slopes",
            ema?.IsWarm == true, ema is { Metadata.IsValid: true, Ema20: not null, Ema50: not null, Ema200: not null,
                Ema20Slope: not null, Ema50Slope: not null, Ema200Slope: not null }, ema?.Metadata.MarketDataAsOfUtc, dailyMinimum));
        var vwap = await databases.MarketDataDb.GetLatestFuturesVwapSignalAsync(contract.ContractId, date, FuturesVwapConfiguration.Standard.ConfigurationId, token).ConfigureAwait(false);
        rows.Add(IndicatorHealthRegistry.Evaluate(contract.ContractId, TimeFrameType.Daily, "Session VWAP",
            vwap?.IsWarm == true, vwap is { IsValid: true, IsTickExact: true, CumulativeVolume: > 0, Vwap: > 0 }, vwap?.AsOfUtc, null));
        var vx = await databases.MarketDataDb.GetLatestFuturesVxTermStructureSignalAsync(date, FuturesVxTermStructureConfiguration.Standard.ConfigurationId, token).ConfigureAwait(false);
        if (vx is { IsWarm: true, IsValid: true }) RegimeDiscoverySignalCacheAdapter.Publish(vx);
        rows.Add(IndicatorHealthRegistry.Evaluate(vx?.FrontVxContractId ?? "VX", TimeFrameType.Daily, "VX front/back",
            vx?.IsWarm == true, vx is { IsValid: true, FrontVxPrice: > 0, FrontBackRatio: > 0 }, vx?.FrontSourceTimestampUtc, null));
        if (market.TryGetFuturesTermStructureContracts("VX", out var curve))
        {
            foreach (var vxContract in new[] { curve.Front, curve.Back })
            foreach (var frame in IndicatorHealthRegistry.IntradayFrames)
            {
                RegimeDiscoverySignalCacheAdapter.TryGetLatestBb(vxContract.ContractId, frame, out var band);
                band ??= await databases.MarketDataDb.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(
                    MarketSeriesIdentity.ForContract(vxContract.ContractId), date, frame, token).ConfigureAwait(false);
                // VX can have quiet intervals. Its qualified last-known bands are
                // not made invalid merely because no new VX trade occurred.
                var entry = IndicatorHealthRegistry.Evaluate(vxContract.ContractId, frame, "Bollinger20/Baseline",
                    band?.IsWarm == true, band is { Metadata.IsValid: true, Width20Ratio: > 0, Position20: not null }, band?.Metadata.MarketDataAsOfUtc, null);
                rows.Add(!entry.Ready && warmup?.Failure(vxContract.ContractId, frame) is { } detail ? entry with { Reason = entry.Reason + ": " + detail } : entry);
            }
        }
        foreach (var horizon in new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly })
        {
            var bucket = horizon switch
            {
                TimeFrameType.Weekly => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
                TimeFrameType.Monthly => new DateOnly(date.Year, date.Month, 1),
                _ => date
            };
            var iti = await databases.MarketDataDb.GetFuturesItiTimeFrameStateAsync(contract.ContractId, horizon, bucket, token).ConfigureAwait(false);
            rows.Add(IndicatorHealthRegistry.Evaluate(contract.ContractId, horizon, "ITI state",
                iti is not null, iti is { Threshold: > 0, BandSize: > 0, IntrinsicPrice: > 0 },
                iti is null ? null : new DateTimeOffset(DateTime.SpecifyKind(iti.IntrinsicTime, DateTimeKind.Utc)), null));
            var selected = parameters?.Resolve("IntrinsicTimeStrategy", horizon);
            if (selected?.IsDisabled == true) continue;
            var applied = selected?.Applied;
            var set = applied is null
                ? (await databases.ConfigurationDb.GetEffectiveRegimeDiscoveryAsync(now.UtcDateTime, horizon, token).ConfigureAwait(false))?.ParameterSet
                : applied.Version.Values().Get<RegimeDiscoveryParameterSet>();
            if (set is null)
            {
                rows.Add(new(contract.ContractId, horizon, "RegimeDiscovery inputs", false, false, null, null, "Effective parameter set unavailable"));
                continue;
            }
            var request = RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract(contract.ContractId), set);
            var captured = await snapshots.CaptureAsync(request, token).ConfigureAwait(false);
            var required = request.Requirements.Where(item => item.IsRequired).Select(item => (item.Metric, item.TimeFrame)).ToHashSet();
            var failures = captured.Issues.Where(item => required.Contains((item.Metric, item.SignalKey.TimeFrame))).ToArray();
            rows.Add(new(contract.ContractId, horizon, "RegimeDiscovery inputs", captured.IsSuccess, captured.IsSuccess,
                captured.Snapshot is { } snapshot ? new DateTimeOffset(snapshot.MarketDataAsOfUtc, TimeSpan.Zero) : null, null,
                captured.IsSuccess ? string.Empty : string.Join(", ", failures.Select(item => $"{item.SignalKey.TimeFrame}/{item.Metric}: {item.Availability}"))));
        }
        return rows.OrderBy(row => row.ContractId, StringComparer.Ordinal).ThenBy(row => row.TimeFrame).ThenBy(row => row.Indicator, StringComparer.Ordinal).ToArray();
    }
}
