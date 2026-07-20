using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Events;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;

/// <summary>
/// Represents the event-sourced state of Spread Distribution Job commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for Spread Distribution Job operations by applying domain events.
/// It mirrors the logic of <see cref="OptionPricer.SpreadDistribution.Job.SpreadDistributionJobBoundedContextState"/>.</remarks>
public class SpreadDistributionJobCommandState(IOptionPricerDbContext db)
    : BaseEventSourceActorState<SpreadDistributionJobCommandState>, IEventSourceActorState<SpreadDistributionJobCommandState>
{
    SpreadDistributionJobReadModel? _spreadDistributionJob = default!;

    internal SpreadDistributionJobReadModel? SpreadDistributionJob => _spreadDistributionJob;

    /// <summary>
    /// Gets or sets the unique identifier for the actor thread associated with this state.
    /// </summary>
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// Applies the specified domain event to update the state of the current object.
    /// </summary>
    /// <param name="domainEvent">The domain event to apply. Must be of a supported type.</param>
    /// <returns><see langword="true"/> if the domain event was successfully applied; otherwise, <see langword="false"/>.</returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                SpreadDistributionJobSubmittedEvent e => On(e),
                SpreadDistributionJobStatusUpdatedEvent e => On(e),
                SpreadDistributionJobsInProgressDeletedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Checks if a spread distribution job is in progress for the given id.
    /// </summary>
    /// <param name="id">The spread distribution entity identifier.</param>
    /// <returns><see langword="true"/> if a job is in progress; otherwise, <see langword="false"/>.</returns>
    public bool IsJobStatusInProgress
        => _spreadDistributionJob?.InProgress ?? false;

    /// <summary>
    /// Spread distribution submitted.
    /// </summary>
    /// <param name="e">The spread distribution submitted event.</param>
    bool On(SpreadDistributionJobSubmittedEvent e)
    {
        EventInitHelper.SetProperty(e, nameof(SpreadDistributionJobSubmittedEvent.SpreadDistributionJob), e.SpreadDistributionJob with { JobStatus = SpreadDistributionJobStatus.InProgress, InProgress = true });
        _spreadDistributionJob = e.SpreadDistributionJob;
        return true;
    }

    /// <summary>
    /// Updates the job status of the spread distribution job based on the specified event.
    /// </summary>
    /// <param name="e">An event that contains the updated job status information for the spread distribution job. Cannot be null.</param>
    /// <returns>Always returns <see langword="true"/>, indicating that the job status was updated.</returns>
    bool On(SpreadDistributionJobStatusUpdatedEvent e)
    {
        _spreadDistributionJob = _spreadDistributionJob! with { JobStatus = e.JobStatus,  InProgress = false };
        return true;
    }

    /// <summary>
    /// Handles a spread distribution job deletion event by performing necessary cleanup operations.
    /// </summary>
    /// <remarks>This method resets the internal reference to the spread distribution job, ensuring that any
    /// associated state is cleared after a deletion event.</remarks>
    /// <param name="e">The event data that contains information about the deleted spread distribution job.</param>
    /// <returns>Always returns <see langword="true"/>, indicating that the event was processed successfully.</returns>
    bool On(SpreadDistributionJobsInProgressDeletedEvent e)
    {
        _spreadDistributionJob = null;
        return true;
    }
}
