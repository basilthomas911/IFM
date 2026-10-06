using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command;

/// <summary>Owns the concrete command handler and event factories for this analytics operation.</summary>
public static class InsertMarketOutlookSnapshot
{
    /// <summary>Computes and validates the Market Outlook Snapshot command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this InsertMarketOutlookSnapshotCommand command, MarketOutlookSnapshotCommandState state)
    {
        if (Equals(state.Snapshot, command.MarketOutlook))
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var errorMsg = "unable to apply MarketOutlookSnapshot event";
        var updated = command.Compute(out var marketOutlook) switch
        {
            _ when marketOutlook is null
                => command.UpdateFailed(ref errorMsg, "MarketOutlookSnapshot payload is missing"),
            _ => state.Update(command.CreateMarketOutlookSnapshotInsertedEvent(marketOutlook), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Market Outlook Snapshot result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="marketOutlook">The market outlook business data used by this operation.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this InsertMarketOutlookSnapshotCommand command, out MarketOutlookReadModel marketOutlook)
    {
        marketOutlook = command.MarketOutlook;
        return marketOutlook is not null;
    }

    /// <summary>Creates the Market Outlook Snapshot event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="marketOutlook">The market outlook business data used by this operation.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static MarketOutlookSnapshotInsertedEvent CreateMarketOutlookSnapshotInsertedEvent(this InsertMarketOutlookSnapshotCommand command, MarketOutlookReadModel marketOutlook) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, MarketOutlookSnapshotInsertedEvent.Actor, MarketOutlookSnapshotInsertedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        Id = Guid.NewGuid(),
        AggregateId = command.EntityId.Format(),
        EventSource = command.EventSource,
        ReceivedOn = DateTime.UtcNow,
        MarketOutlook = marketOutlook
    };
}
