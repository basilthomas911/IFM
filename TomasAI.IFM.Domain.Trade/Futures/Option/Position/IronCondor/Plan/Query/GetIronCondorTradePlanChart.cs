using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query;

public static class GetIronCondorTradePlanChart
{

    extension(GetIronCondorTradePlanChartQuery query)
    {
        public async ValueTask<ServiceResult<StrategyTradePlanChartSeries>> ExecuteAsync(IIronCondorTradePlanQueryContext context, CancellationToken token)
        {
            var series = await LoadSeriesAsync(context.DbFactory.TradeDb, context.DbFactory.TradePlanDb.DbReader,
                query.PlanId.Position, query.PlanId.ValueDate, context.Clock.GetUtcNow(), token).ConfigureAwait(false);
            context.Logger.LogInformation("Iron Condor lifetime chart loaded; PositionId={PositionId}; ValueDate={ValueDate}; Samples={Samples}",
                query.PlanId.Position.Format(), query.PlanId.ValueDate, series.Points.Length);
            return new ServiceResult<StrategyTradePlanChartSeries>(series);
        }
    }

    internal static async Task<StrategyTradePlanChartSeries> LoadSeriesAsync(ITradeDbReadContext tradeDb, ITradePlanDbReadContext planDb,
        StrategyPositionId position, DateOnly valueDate, DateTimeOffset now, CancellationToken token)
    {
        var trade = await tradeDb.GetEstablishedTradeAsync(position.Trade, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The chart requires an established trade.");
        if (trade.StrategyKind != TradeStrategyKind.IronCondor || trade.TradeDate is not { } tradeDate || trade.MaturityDate is not { } maturity)
            throw new InvalidOperationException("The chart requires Iron Condor trade and maturity dates.");
        var window = ResolveWindow(tradeDate, maturity, valueDate, now, trade.EstablishedAtUtc, trade.ClosedAtUtc);
        var history = new List<StrategyPositionSnapshot>();
        byte[]? cursor = null;
        if (window.ObservationEndUtc >= window.StartUtc)
        {
            do
            {
                var page = await tradeDb.GetStrategyPositionHistoryAsync(position.PositionId,
                    window.StartUtc, window.ObservationEndUtc, 1000, cursor, token).ConfigureAwait(false);
                history.AddRange(page.Items.Where(p => p.Id == position));
                cursor = page.PagingState;
            } while (cursor is not null);
        }
        var daily = DailySnapshots(history, trade, valueDate, window.ObservationEndUtc).ToArray();
        var dayEnds = daily.ToDictionary(p => p.ValueDate, p => p.CloseBoundaryUtc ?? p.AsOfUtc);
        var forwards = dayEnds.Count == 0 ? new Dictionary<DateOnly, decimal?>()
            : await planDb.GetDailyForwardSpreadsAsync(position, dayEnds, token).ConfigureAwait(false);
        var points = daily.Select(p => DailyPoint(p, trade) with
        {
            ForwardSpread = forwards.GetValueOrDefault(p.ValueDate)
        }).ToList();
        if (window.ObservationEndUtc >= window.SessionStartUtc)
        {
            var intraday = await planDb.GetChartRangeAsync(position,
                valueDate, valueDate, window.SessionStartUtc, window.ObservationEndUtc, token).ConfigureAwait(false);
            points.AddRange(intraday.Points);
        }
        return new StrategyTradePlanChartSeries(window.StartUtc, window.EndUtc, points.OrderBy(p => p.AtUtc).ToArray());
    }

    internal static StrategyTradePlanChartPoint[] DailyPoints(IEnumerable<StrategyPositionSnapshot> history,
        EstablishedTradeDefinition trade, DateOnly valueDate, DateTime observationEndUtc)
        => DailySnapshots(history, trade, valueDate, observationEndUtc).Select(p => DailyPoint(p, trade)).OrderBy(p => p.AtUtc).ToArray();

    static StrategyTradePlanChartPoint DailyPoint(StrategyPositionSnapshot position, EstablishedTradeDefinition trade)
        => new(position.CloseBoundaryUtc ?? position.AsOfUtc,
            StrategyPositionHistoryProjection.EnrichPosition(position, trade).HistoryValuation!.NetPrice);

    static IEnumerable<StrategyPositionSnapshot> DailySnapshots(IEnumerable<StrategyPositionSnapshot> history,
        EstablishedTradeDefinition trade, DateOnly valueDate, DateTime observationEndUtc)
        => history.Where(p => p.Phase == StrategyPositionPhase.EndOfDay)
            .Select(p => p.ValueDate == default ? p with
            {
                ValueDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(p.AsOfUtc, FuturesTradingValueDate.MarketTimeZone))
            } : p)
            .Where(p => p.ValueDate >= trade.TradeDate
                && p.ValueDate < valueDate && p.ValueDate <= trade.MaturityDate && p.AsOfUtc <= observationEndUtc)
            .GroupBy(p => p.ValueDate)
            .Select(group => group.OrderByDescending(p => p.PositionSequence).First())
            .Where(p => p.Legs.Length > 0 && p.Legs.All(l => l.CurrentPrice >= 0 && l.SignedQuantity != 0))
            .Where(p => (p.CloseBoundaryUtc ?? p.AsOfUtc) <= observationEndUtc);

    internal static (DateTime StartUtc, DateTime EndUtc, DateTime SessionStartUtc, DateTime ObservationEndUtc) ResolveWindow(
        DateOnly tradeDate, DateOnly maturity, DateOnly valueDate, DateTimeOffset now, DateTime establishedAtUtc, DateTime? closedAtUtc)
    {
        if (maturity < tradeDate || valueDate < tradeDate)
            throw new ArgumentException("Chart dates must start at or after the trade date and mature after it.");
        var start = FuturesTradingValueDate.GetSessionStartUtc(tradeDate).UtcDateTime;
        var end = FuturesTradingValueDate.GetSessionEndUtc(maturity).UtcDateTime;
        var sessionStart = FuturesTradingValueDate.GetSessionStartUtc(valueDate).UtcDateTime;
        sessionStart = sessionStart < establishedAtUtc ? establishedAtUtc : sessionStart;
        var observedEnd = new[] { now.UtcDateTime, end, FuturesTradingValueDate.GetSessionEndUtc(valueDate).UtcDateTime,
            closedAtUtc ?? end }.Min();
        return (start, end, sessionStart, observedEnd);
    }
}
