using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Model;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command;

/// <summary>Computes SubmitSpreadDistributionJob business values and applies the resulting source event.</summary>
public static class SubmitSpreadDistributionJob
{
    /// <summary>Computes proposed business values, checks failure guards, and applies one source event.</summary>
    /// <param name="command">The originating command supplying business inputs and identity.</param>
    /// <param name="state">The owning state; mutations occur through Update and Apply.</param>
    /// <returns>The command identity on success, otherwise the business or application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this SubmitSpreadDistributionJobCommand command, SpreadDistributionJobCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply SpreadDistributionJobSubmittedEvent";
        var updated = command.Compute(state.SpreadDistributionJob, out var spreadDistributionChange) switch
        {
            _ => state.Update(command.CreateSpreadDistributionJobEvent(spreadDistributionChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable business values without changing state or pending events.</summary>
    /// <param name="command">The requested business operation.</param>
    /// <param name="activeSpreadDistributionJob">The current job whose active submission must be preserved.</param>
    /// <param name="spreadDistributionChange">The computed business values to carry in the event.</param>
    /// <returns>True when computation completes; acceptance is checked before application.</returns>
    internal static bool Compute(this SubmitSpreadDistributionJobCommand command, SpreadDistributionJobReadModel? activeSpreadDistributionJob, out SpreadDistributionJobSubmission spreadDistributionChange)
    {
        spreadDistributionChange = new(activeSpreadDistributionJob is { InProgress: true } ? activeSpreadDistributionJob : command.SpreadDistributionJob with { JobStatus = SpreadDistributionJobStatus.InProgress, InProgress = true }, activeSpreadDistributionJob?.InProgress == true);
        return true;
    }

    /// <summary>Selects the appropriate job event while preserving an existing active submission.</summary>
    /// <param name="command">The originating command supplying identity and routing metadata.</param>
    /// <param name="spreadDistributionChange">The computed submission and active-job decision.</param>
    /// <returns>The source event representing the submission outcome.</returns>
    internal static IEvent CreateSpreadDistributionJobEvent(this SubmitSpreadDistributionJobCommand command, SpreadDistributionJobSubmission spreadDistributionChange)
        => spreadDistributionChange.AlreadyInProgress
            ? command.CreateSpreadDistributionJobInProgressEvent(spreadDistributionChange)
            : command.CreateSpreadDistributionJobSubmittedEvent(spreadDistributionChange);

    /// <summary>
    /// Creates a <see cref="SpreadDistributionJobSubmittedEvent"/> from a
    /// <see cref="SubmitSpreadDistributionJobCommand"/>.
    /// Embeds the full <see cref="SpreadDistributionJobReadModel"/> payload from the command into the event.
    /// </summary>
    /// <param name="command">The command carrying the spread distribution job to submit.</param>
    /// <returns>A fully populated <see cref="SpreadDistributionJobSubmittedEvent"/>.</returns>
    /// <param name="spreadDistributionChange">The computed job submission payload.</param>
    internal static SpreadDistributionJobSubmittedEvent CreateSpreadDistributionJobSubmittedEvent(this SubmitSpreadDistributionJobCommand command, SpreadDistributionJobSubmission spreadDistributionChange)
         => new()
         {
             CommandId = command.CommandId,
             Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobSubmittedEvent.Actor, SpreadDistributionJobSubmittedEvent.Verb, command.EntityId.Format()),
             EntityId = command.EntityId,
             SpreadDistributionJob = spreadDistributionChange.SpreadDistributionJob,
             CreatedBy = command.OriginatedBy,
             CreatedOn = command.OriginatedOn
         };

    /// <summary>
    /// Creates a <see cref="SpreadDistributionJobInProgressEvent"/> from a
    /// <see cref="SubmitSpreadDistributionJobCommand"/> when a job is already running for the entity.
    /// Uses the current <paramref name="spreadDistributionChange"/> read model rather than the one embedded
    /// in the command, preserving the active job state.
    /// </summary>
    /// <param name="command">The submit command that triggered the in-progress transition.</param>
    /// <param name="spreadDistributionChange">The current read model of the job already in progress.</param>
    /// <returns>A fully populated <see cref="SpreadDistributionJobInProgressEvent"/>.</returns>
    internal static SpreadDistributionJobInProgressEvent CreateSpreadDistributionJobInProgressEvent(this SubmitSpreadDistributionJobCommand command, SpreadDistributionJobSubmission spreadDistributionChange)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, SpreadDistributionJobInProgressEvent.Actor, SpreadDistributionJobInProgressEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            SpreadDistributionJob = spreadDistributionChange.SpreadDistributionJob,
            CreatedBy = command.OriginatedBy,
            CreatedOn = command.OriginatedOn
        };
}
