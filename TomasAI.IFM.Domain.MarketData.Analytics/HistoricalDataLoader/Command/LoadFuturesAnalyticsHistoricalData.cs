using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command;

/// <summary>Translates a parameter-only data load command into its durable Requested event.</summary>
public static class LoadFuturesAnalyticsHistoricalData
{
    /// <summary>Computes and validates the Historical Data Loader command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this LoadFuturesAnalyticsHistoricalDataCommand command, FuturesAnalyticsHistoricalDataLoaderCommandState state)
    {
        var errorMsg = "unable to apply HistoricalDataLoader event";
        var updated = command.Compute(out var historicalDataLoaderParameters) switch
        {
            _ when state.IsRequested
                => command.UpdateFailed(ref errorMsg, "The data load attempt was already requested."),
            _ when historicalDataLoaderParameters is null
                => command.UpdateFailed(ref errorMsg, "HistoricalDataLoader payload is missing"),
            _ => state.Update(command.CreateFuturesAnalyticsHistoricalDataLoaderRequestedEvent(historicalDataLoaderParameters), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Historical Data Loader result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="historicalDataLoaderParameters">The historical data loader parameters business data used by this operation.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this LoadFuturesAnalyticsHistoricalDataCommand command, out FuturesAnalyticsHistoricalDataLoaderParameters historicalDataLoaderParameters)
    {
        historicalDataLoaderParameters = command.FuturesAnalyticsHistoricalDataLoaderParameters;
        return historicalDataLoaderParameters is not null;
    }

    /// <summary>Creates the Historical Data Loader event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="historicalDataLoaderParameters">The historical data loader parameters business data used by this operation.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesAnalyticsHistoricalDataLoaderRequestedEvent CreateFuturesAnalyticsHistoricalDataLoaderRequestedEvent(this LoadFuturesAnalyticsHistoricalDataCommand command, FuturesAnalyticsHistoricalDataLoaderParameters historicalDataLoaderParameters) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Actor, FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        ReceivedOn = DateTime.UtcNow,
        FuturesAnalyticsHistoricalDataLoaderParameters = historicalDataLoaderParameters
    };
}
