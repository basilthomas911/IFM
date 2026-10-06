using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Model;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command;

/// <summary>Computes DeleteSpreadDistributionJobsInProgress business values and applies the resulting source event.</summary>
public static class DeleteSpreadDistributionJobsInProgress
{
    /// <summary>Computes proposed business values, checks failure guards, and applies one source event.</summary>
    /// <param name="command">The originating command supplying business inputs and identity.</param>
    /// <param name="state">The owning state; mutations occur through Update and Apply.</param>
    /// <returns>The command identity on success, otherwise the business or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this DeleteSpreadDistributionJobsInProgressCommand command, SpreadDistributionJobCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply SpreadDistributionJobsInProgressDeletedEvent";
        var updated = command.Compute(state.IsJobStatusInProgress, out var spreadDistributionChange) switch
        {
            _ when !spreadDistributionChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: spread distribution job {command.EntityId} is not in progress"),
            _ => state.Update(command.CreateSpreadDistributionJobsInProgressDeletedEvent(spreadDistributionChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable business values without changing state or pending events.</summary>
    /// <param name="command">The requested business operation.</param>
    /// <param name="jobInProgress">Whether the current job permits this transition.</param>
    /// <param name="spreadDistributionChange">The computed business values to carry in the event.</param>
    /// <returns>True when computation completes; acceptance is checked before application.</returns>
    internal static bool Compute(this DeleteSpreadDistributionJobsInProgressCommand command, bool jobInProgress, out SpreadDistributionJobsRemoval spreadDistributionChange)
    {
        spreadDistributionChange = new(new(command.EntityId.OrderId, command.EntityId.TradeId), jobInProgress);
        return true;
    }

    /// <summary>
    /// Creates a SpreadDistributionJobsInProgressDeletedEvent from a DeleteSpreadDistributionJobsInProgressCommand.
    /// </summary>
    /// <param name="command">The command requesting deletion of all in-progress jobs for the associated option trade.</param>
    /// <param name="spreadDistributionChange">The computed accepted business values carried by the source event.</param>
    /// <returns>A fully populated <see cref="SpreadDistributionJobsInProgressDeletedEvent"/>.</returns>
    internal static SpreadDistributionJobsInProgressDeletedEvent CreateSpreadDistributionJobsInProgressDeletedEvent(this DeleteSpreadDistributionJobsInProgressCommand command, SpreadDistributionJobsRemoval spreadDistributionChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobsInProgressDeletedEvent.Actor, SpreadDistributionJobsInProgressDeletedEvent.Verb, command.EntityId.Format()),
            EntityId = spreadDistributionChange.OptionTradeId,
            CreatedBy = command.OriginatedBy,
            CreatedOn = command.OriginatedOn,
        };

}
