using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Model;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command;

/// <summary>Computes ClearSpreadDistributionJob business values and applies the resulting source event.</summary>
public static class ClearSpreadDistributionJob
{
    /// <summary>Computes proposed business values, checks failure guards, and applies one source event.</summary>
    /// <param name="command">The originating command supplying business inputs and identity.</param>
    /// <param name="state">The owning state; mutations occur through Update and Apply.</param>
    /// <returns>The command identity on success, otherwise the business or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this ClearSpreadDistributionJobCommand command, SpreadDistributionJobCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply SpreadDistributionJobStatusUpdatedEvent";
        var updated = command.Compute(state.IsJobStatusInProgress, out var spreadDistributionChange) switch
        {
            _ when !spreadDistributionChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: spread distribution job {command.EntityId} is not in progress"),
            _ => state.Update(command.CreateSpreadDistributionJobClearedEvent(spreadDistributionChange), command)
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
    internal static bool Compute(this ClearSpreadDistributionJobCommand command, bool jobInProgress, out SpreadDistributionJobClearance spreadDistributionChange)
    {
        spreadDistributionChange = new(SpreadDistributionJobStatus.Cleared, jobInProgress);
        return true;
    }

    /// <summary>
    /// Creates a <see cref="SpreadDistributionJobStatusUpdatedEvent"/> indicating that the spread distribution job has been cleared.
    /// </summary>
    /// <param name="command">The clear spread distribution job command.</param>
    /// <param name="spreadDistributionChange">The computed accepted business values carried by the source event.</param>
    /// <returns>A fully populated <see cref="SpreadDistributionJobStatusUpdatedEvent"/> with status <c>Cleared</c>.</returns>
    internal static SpreadDistributionJobStatusUpdatedEvent CreateSpreadDistributionJobClearedEvent(this ClearSpreadDistributionJobCommand command, SpreadDistributionJobClearance spreadDistributionChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobStatusUpdatedEvent.Actor, SpreadDistributionJobStatusUpdatedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            JobStatus = SpreadDistributionJobStatus.Cleared
        };
}
