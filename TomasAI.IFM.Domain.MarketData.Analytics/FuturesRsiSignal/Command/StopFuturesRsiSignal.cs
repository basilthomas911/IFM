using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;

public static class StopFuturesRsiSignal
{
    /// <summary>
    /// Executes the StopFuturesRsiSignalCommand by updating the FuturesRsiSignalCommandState with a new FuturesRsiSignalStoppedEvent.
    /// </summary>
    /// <param name="e">The command to execute.</param>
    /// <param name="state">The state to update.</param>
    /// <returns><see langword="true"/> if the command was executed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool Execute(this StopFuturesRsiSignalCommand e, FuturesRsiSignalCommandState state)
        => state.Update(e.CreateFuturesRsiSignalStoppedEvent(), e);

    /// <summary>
    /// Creates a new FuturesRsiSignalStoppedEvent from the StopFuturesRsiSignalCommand.
    /// </summary>
    /// <param name="e">The command containing the details required to construct the stopped RSI signal event.</param>
    /// <returns>A FuturesRsiSignalStoppedEvent initialized with the entity ID, originator, and timestamp from the provided command.</returns>
    internal static FuturesRsiSignalStoppedEvent CreateFuturesRsiSignalStoppedEvent(this StopFuturesRsiSignalCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesRsiSignalStoppedEvent.Actor, FuturesRsiSignalStoppedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            StoppedOn = e.OriginatedOn,
            StoppedBy = e.OriginatedBy
        };
}
