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
public sealed class IronCondorTradePositionRealtimeContext : EventActorContext, IIronCondorTradePositionRealtimeContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    public IronCondorTradePositionRealtimeContext(IActorSupervisor supervisor, ILogger<IronCondorTradePositionRealtimeActor> logger, IDbContextFactory? databases = null, IFinancialQueryStore? financialQueries = null, IndividualOptionRiskReader? optionRisk = null, StrategyRiskParameterSetResolver? riskParameterSets = null) : base(supervisor, new(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName))
    {
        Logger = logger;
        inputReader = databases is null ? null : new(databases, logger, financialQueries, optionRisk, stoppingToken: RealtimeGeneration.Token, publishInputChange: async (position, date, inputs, now) =>
        {
            await SendAsync<TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorMonitoringInputsChangedEvent, StrategyPositionId>(new() { Subject = new(ActorType.Realtime, IronCondorTradePositionRealtimeActor.ActorName, TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorMonitoringInputsChangedEvent.Verb, position.Id.Format()), Id = Guid.NewGuid(), EntityId = position.Id, PositionSnapshot = position, ValueDate = date, IronCondorTradePlanInputs = inputs, ReceivedOn = now, MonitoringGenerationId = RealtimeGeneration.Id }).ConfigureAwait(false);
        }, riskParameterSets: riskParameterSets);
    }

    readonly IronCondorMonitoringInputReader? inputReader;
    readonly System.Collections.Concurrent.ConcurrentDictionary<StrategyPositionId, StrategyPositionSnapshot> positions = new();
    /// <summary>Captures current monitoring inputs without waiting for a storage refresh.</summary>
    public IronCondorTradePlanInputs? CaptureMonitoringInputs(StrategyPositionSnapshot position, DateOnly valueDate, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(position);
        RealtimeGeneration.Token.ThrowIfCancellationRequested();
        positions.AddOrUpdate(position.Id, position, (_, current) => position.RouteGeneration > current.RouteGeneration || position.RouteGeneration == current.RouteGeneration && position.PositionSequence >= current.PositionSequence ? position : current);
        return inputReader?.Capture(position, valueDate, nowUtc);
    }

    /// <summary>Fences delayed input notifications from a retired lifetime or preceding position revision.</summary>
    public bool IsCurrentMonitoringPosition(StrategyPositionSnapshot position, Guid generationId)
    {
        ArgumentNullException.ThrowIfNull(position);
        return !RealtimeGeneration.Token.IsCancellationRequested && generationId == RealtimeGeneration.Id && positions.TryGetValue(position.Id, out var current) && position.RouteGeneration == current.RouteGeneration && position.PositionSequence == current.PositionSequence;
    }

    public TimeProvider TimeProvider { get; } = TimeProvider.System;
    public TradePlanParameters Parameters { get; } = new();
    public ILogger<IronCondorTradePositionRealtimeActor> Logger { get; }
}
