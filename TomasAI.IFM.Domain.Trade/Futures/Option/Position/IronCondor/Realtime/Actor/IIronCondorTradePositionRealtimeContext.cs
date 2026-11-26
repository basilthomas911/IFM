using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
public interface IIronCondorTradePositionRealtimeContext : IRealtimeActorContext<IronCondorTradePositionRealtimeActor>
{
    ValueTask RecoverPublicationsAsync() => ValueTask.CompletedTask;
    ValueTask PublishRealtimeAsync(StrategyPositionSnapshot position, StrategyTradePlanSnapshot? plan = null) => ValueTask.CompletedTask;
    ValueTask<bool> IsClosingAsync(StrategyPositionId position) => ValueTask.FromResult(false);
    /// <summary>Checks whether an input observation still describes this actor lifetime and latest position.</summary>
    bool IsCurrentMonitoringPosition(StrategyPositionSnapshot position, Guid generationId) => true;
    /// <summary>Durable original requests shared across actor lifetimes.</summary>
    IronCondorPositionRequests Requests { get; }
    TimeProvider TimeProvider { get; }

    TradePlanParameters Parameters { get; }

    ILogger<IronCondorTradePositionRealtimeActor> Logger { get; }

    /// <summary>Captures immutable monitoring observations from the host read cache.</summary>
    IronCondorTradePlanInputs? CaptureMonitoringInputs(StrategyPositionSnapshot position, DateOnly valueDate, DateTime nowUtc) => null;
}

/// <summary>Optional original-generation evidence capability; ordinary test contexts and callers gain no bypass.</summary>
public interface IIronCondorMonitoringReplayEvidence
{
    bool IsCurrentRecordedMonitoringInput(IronCondorMonitoringInputsChangedEvent input,string payloadSha256);
    ValueTask RecordMonitoringGenerationAcceptanceAsync(IronCondorMonitoringInputsChangedEvent input,string payloadSha256);
}
