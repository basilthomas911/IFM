using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command;

public static class GenerateFuturesTdiSignal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool Execute(this GenerateFuturesTdiSignalCommand e, FuturesTdiSignalCommandState state)
    {
        if (!e.Compute(state.TdiSignal, out var model))
            return false;
        return state.Update(e.CreateFuturesTdiSignalGeneratedEvent(model!), e);
    }

    /// <summary>
    /// Attempts to create a new futures TDI signal compute model based on the specified command and read model.
    /// </summary>
    /// <param name="e">The command containing the input RSI signals used to generate the TDI signal compute model.</param>
    /// <param name="tdiSignal">The read model representing the current TDI signal state to use as a basis for computation.</param>
    /// <param name="computeModel">When this method returns, contains the resulting futures TDI signal compute model if the operation succeeds;
    /// otherwise, contains null.</param>
    /// <returns>true if the compute model was successfully created; otherwise, false.</returns>
    internal static bool Compute(
        this GenerateFuturesTdiSignalCommand e,
        FuturesTdiSignalReadModel? tdiSignal,
        out FuturesTdiSignalCompute? computeModel)
        => FuturesTdiSignalCompute.Create(e.FuturesRsiSignals, tdiSignal, e.Configuration, out computeModel);

    /// <summary>
    /// Creates a new instance of the <see cref="FuturesTdiSignalGeneratedEvent"/> using the specified command
    /// and trend direction type.
    /// </summary>
    internal static FuturesTdiSignalGeneratedEvent CreateFuturesTdiSignalGeneratedEvent(
        this GenerateFuturesTdiSignalCommand e,
        FuturesTdiSignalCompute computed)
    {
        var entityId = new FuturesTdiSignalEntityId(
            e.FuturesTdiSignalId.ContractId,
            e.FuturesTdiSignalId.ValueDate,
            e.EntityId.TimePeriod,
            e.Configuration.ConfigurationId);
        var current = computed.CurrentRsiSignal;
        return new FuturesTdiSignalGeneratedEvent
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesTdiSignalGeneratedEvent.Actor, FuturesTdiSignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesTdiSignal = new(
                e.FuturesTdiSignalId.ContractId,
                e.FuturesTdiSignalId.ValueDate,
                e.EntityId.TimePeriod,
                current.Timestamp,
                e.Configuration,
                current.Price,
                current.RSI,
                computed.PriceLine,
                computed.SignalLine,
                computed.MarketBaseLine,
                computed.UpperVolatilityBand,
                computed.LowerVolatilityBand,
                computed.TrendDirection,
                computed.TrendStrength,
                computed.Cross,
                computed.MarketState,
                current.SourceSequence,
                current.SourceEventTimestamp),
            CreatedBy = e.OriginatedBy,
            CreatedOn = e.OriginatedOn
        };
    }
}
