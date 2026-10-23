using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query;

public static class GetIronCondorTradePlanChart
{
    public static async ValueTask ExecuteAsync(this GetIronCondorTradePlanChartQuery query, IIronCondorTradePlanQueryContext context, CancellationToken token)
    {
        var window = ResolveWindow(query.PlanId.ValueDate, context.Clock.GetUtcNow());
        var series = await context.DbFactory.TradePlanDb.DbReader.GetChartRangeAsync(query.PlanId.Position,
            window.FirstDate,window.LastDate,window.StartUtc,window.EndUtc,token).ConfigureAwait(false);
        context.Logger.LogInformation("Iron Condor chart range loaded; PositionId={PositionId}; FirstValueDate={FirstValueDate}; LastValueDate={LastValueDate}; StartUtc={StartUtc}; EndUtc={EndUtc}; Samples={Samples}",
            query.PlanId.Position.Format(),window.FirstDate,window.LastDate,window.StartUtc,window.EndUtc,series.Points.Length);
        await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceResult<StrategyTradePlanChartSeries>(series)).ConfigureAwait(false);
    }

    internal static (DateOnly FirstDate,DateOnly LastDate,DateTime StartUtc,DateTime EndUtc) ResolveWindow(DateOnly valueDate, DateTimeOffset now)
    {
        var close = FuturesTradingValueDate.GetSessionEndUtc(valueDate);
        var end = now < close ? now : close;
        var start = end.AddHours(-6);
        return (FuturesTradingValueDate.GetOperational(start),FuturesTradingValueDate.GetOperational(end),start.UtcDateTime,end.UtcDateTime);
    }
}
