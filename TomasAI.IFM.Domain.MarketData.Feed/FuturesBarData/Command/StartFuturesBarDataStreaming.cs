using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command;

/// <summary>Handles StartFuturesBarDataStreaming commands through computation and state-owned event application.</summary>
public static class StartFuturesBarDataStreaming
{
    /// <summary>Computes the requested business change and applies its event only after acceptance guards pass.</summary>
    /// <param name="command">The originating command and its business inputs.</param>
    /// <param name="state">The owning event-sourced state; mutations occur only through event application.</param>
    /// <returns>The command identity on success, or the business rejection or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this StartFuturesBarDataStreamingCommand command, FuturesBarDataCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FuturesBarDataStreamingStartedEvent";
        var updated = command.Compute(out var futuresBarDataStreamingStart) switch
        {
            _ => state.Update(command.CreateFuturesBarDataStreamingStartedEvent(futuresBarDataStreamingStart), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes the proposed business inputs without mutating state or creating pending events.</summary>
    /// <param name="command">The command supplying the requested business values.</param>
    /// <param name="futuresBarDataStreamingStart">The proposed business values passed to the event factory.</param>
    /// <returns>True when the proposed inputs have been computed; acceptance guards are evaluated before application.</returns>
    internal static bool Compute(this StartFuturesBarDataStreamingCommand command, out FuturesBarDataStreamingStart futuresBarDataStreamingStart)
    {
        futuresBarDataStreamingStart = new(command.Contracts, command.ValueDate);
        return true;
    }

    /// <summary>Creates the source event from computed business values with the originating command identity.</summary>
    /// <param name="command">The originating command supplying route and audit metadata.</param>
    /// <param name="futuresBarDataStreamingStart">The accepted business values to carry in the event.</param>
    /// <returns>A source event ready for the owning state's Update and Apply path.</returns>
    internal static FuturesBarDataStreamingStartedEvent CreateFuturesBarDataStreamingStartedEvent(this StartFuturesBarDataStreamingCommand command, FuturesBarDataStreamingStart futuresBarDataStreamingStart)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesBarDataStreamingStartedEvent.Actor, FuturesBarDataStreamingStartedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Contracts = futuresBarDataStreamingStart.FuturesContracts,
            ValueDate = futuresBarDataStreamingStart.ValueDate,
            StartedOn = command.OriginatedOn,
            StartedBy = command.OriginatedBy
        };
}
