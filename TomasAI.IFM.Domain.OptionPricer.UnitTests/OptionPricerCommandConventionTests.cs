using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Domain.OptionPricer.Shared.Events;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Command.State;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.State;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests;

public class OptionPricerCommandConventionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void InactiveJobRejectionDoesNotCreateEvents(int operation)
    {
        var state = new SpreadDistributionJobCommandState();
        var result = ExecuteTransition(operation, state);
        Assert.IsType<ServiceFailed<GuidResult>>(result);
        Assert.Empty(state.Events);
        Assert.False(state.Updated);
    }

    [Theory]
    [InlineData(0, SpreadDistributionJobStatus.Completed)]
    [InlineData(1, SpreadDistributionJobStatus.Failed)]
    [InlineData(2, SpreadDistributionJobStatus.Cleared)]
    [InlineData(3, SpreadDistributionJobStatus.InProgress)]
    public void ActiveJobTransitionAppliesOneEventAndCanBeReplayed(int operation, SpreadDistributionJobStatus expectedStatus)
    {
        var state = new SpreadDistributionJobCommandState();
        var submit = new SubmitSpreadDistributionJobCommand(new SpreadDistributionJobReadModel { OrderId = 1, TradeId = 2 }) { CommandId = Guid.NewGuid() };
        Assert.IsType<ServiceOk<GuidResult>>(submit.Execute(state));
        var submitted = Assert.IsType<SpreadDistributionJobSubmittedEvent>(Assert.Single(state.Events));
        Assert.Equal(submit.CommandId, submitted.CommandId);
        Assert.True(submitted.SpreadDistributionJob.InProgress);
        Assert.Equal(SpreadDistributionJobStatus.InProgress, submitted.SpreadDistributionJob.JobStatus);
        Assert.False(submit.SpreadDistributionJob.InProgress);
        state.AcceptChanges();
        Assert.IsType<ServiceOk<GuidResult>>(ExecuteTransition(operation, state));
        var transition = Assert.Single(state.Events);
        Assert.NotEqual(Guid.Empty, transition.CommandId);
        if (operation != 3)
            Assert.Equal(expectedStatus, Assert.IsType<SpreadDistributionJobStatusUpdatedEvent>(transition).JobStatus);
        var replay = new SpreadDistributionJobCommandState();
        replay.ReplayEvents(new[] { submitted, transition }.AsEnumerable());
        Assert.False(replay.IsJobStatusInProgress);
        Assert.Empty(replay.Events);
        Assert.IsType<ServiceFailed<GuidResult>>(ExecuteTransition(operation, replay));
    }

    [Fact]
    public void RepeatedSubmissionPreservesTheActiveJob()
    {
        var state = new SpreadDistributionJobCommandState();
        var initial = new SubmitSpreadDistributionJobCommand(new SpreadDistributionJobReadModel { OrderId = 1, TradeId = 2, LossProbabilityFactor = 0.25 }) { CommandId = Guid.NewGuid() };
        initial.Execute(state);
        var active = Assert.IsType<SpreadDistributionJobSubmittedEvent>(Assert.Single(state.Events)).SpreadDistributionJob;
        state.AcceptChanges();
        var repeated = initial with { CommandId = Guid.NewGuid(), SpreadDistributionJob = initial.SpreadDistributionJob with { LossProbabilityFactor = 0.9 } };
        Assert.IsType<ServiceOk<GuidResult>>(repeated.Execute(state));
        var observed = Assert.IsType<SpreadDistributionJobInProgressEvent>(Assert.Single(state.Events));
        Assert.Same(active, observed.SpreadDistributionJob);
        Assert.Equal(repeated.CommandId, observed.CommandId);
        Assert.True(state.IsJobStatusInProgress);
    }

    [Fact]
    public void DistributionEventsCarryOriginatingCommandIdentity()
    {
        var state = new SpreadDistributionCommandState();
        var insert = new InsertSpreadDistributionCommand { CommandId = Guid.NewGuid(), EntityId = new(2, new DateOnly(2026, 10, 6)) };
        Assert.IsType<ServiceOk<GuidResult>>(insert.Execute(state));
        Assert.Equal(insert.CommandId, Assert.Single(state.Events).CommandId);
        state.AcceptChanges();
        var delete = new DeleteSpreadDistributionCommand { CommandId = Guid.NewGuid(), EntityId = new(2, new DateOnly(2026, 10, 6)) };
        Assert.IsType<ServiceOk<GuidResult>>(delete.Execute(state));
        Assert.Equal(delete.CommandId, Assert.Single(state.Events).CommandId);
    }

    static ServiceResult<GuidResult> ExecuteTransition(int operation, SpreadDistributionJobCommandState state)
        => operation switch
        {
            0 => new CompleteSpreadDistributionJobCommand { CommandId = Guid.NewGuid() }.Execute(state),
            1 => new FailSpreadDistributionJobCommand { CommandId = Guid.NewGuid() }.Execute(state),
            2 => new ClearSpreadDistributionJobCommand { CommandId = Guid.NewGuid() }.Execute(state),
            _ => new DeleteSpreadDistributionJobsInProgressCommand { CommandId = Guid.NewGuid() }.Execute(state)
        };
}

