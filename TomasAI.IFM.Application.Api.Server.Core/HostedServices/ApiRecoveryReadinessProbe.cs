using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.HostedServices;

public static class ApiRecoveryReadinessProbe
{
    public static ApiRecoveryReadiness Capture(TradeFeedRecoveryStartupService.Restoration? status, IFuturesMarketSessionAuthority sessions,
        IActorSupervisor supervisor, TradeRealtimeCacheRegistry caches)
    {
        var date = sessions.Current.ActiveValueDate;
        var controller = Environment.GetEnvironmentVariable(ApiRecoveryRestartProtocol.ControllerVariable)=="1"
            && Directory.Exists(Environment.GetEnvironmentVariable(ApiRecoveryRestartProtocol.DirectoryVariable));
        if (!supervisor.IsReady || status is null || !status.Restored || status.ValueDate != date)
            return new(Environment.ProcessId, date, false, [], controller);
        var started = new DateTimeOffset(DateTime.SpecifyKind(status.StartedUtc, DateTimeKind.Utc));
        var proven = caches.CaptureRecoveryObservations().Where(trade => trade.ValueDate == date
            && trade.Position is { } position && trade.Plan is { } plan
            && plan.IronCondorTradePlanSnapshot?.IsComplete == true
            && position.AsOfUtc >= started.UtcDateTime && plan.CalculatedAtUtc >= started.UtcDateTime
            && position.RouteGeneration == plan.Position.RouteGeneration && position.PositionSequence >= plan.Position.PositionSequence
            && plan.Position.Legs.Length == 4 && plan.Position.Legs.All(leg => leg.OptionTick?.ObservedAtUtc >= started && leg.OptionRisk?.CalculatedAtUtc >= started)
            && trade.Legs.Length == 4 && trade.Legs.All(leg => leg.Observation?.ObservedAtUtc >= started && leg.LastValidRisk?.CalculatedAtUtc >= started)
            && caches.HasPublishedPosition(trade.TradeId, plan.Position.RouteGeneration, plan.Position.PositionSequence))
            .Select(trade => trade.TradeId.Format()).ToArray();
        return new(Environment.ProcessId, date, true, proven, controller);
    }
}
