using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-05")]
public sealed class FinancialWorkflowRecoveryIntegrationTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Restart_reads_latest_authoritative_snapshot_without_any_projection()
    {
        _=fixture;var first=Snapshot();var stream=Stream(first);
        await Save(stream,first,0);
        var second=first with { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),WorkflowRevision=2,
            State=first.State with { WorkflowRevision=2 } };
        await Save(stream,second,1);
        var page=await new FinancialWorkflowRecoveryJournal(Transactions()).ReadPageAsync(0,exactStream:stream);
        page.StreamsRead.Should().Be(1);page.Snapshots.Should().ContainSingle();
        page.Snapshots[0].WorkflowRevision.Should().Be(2);
        page.Snapshots[0].CommandId.Should().Be(second.CommandId);
        (await new FinancialWorkflowRecoveryJournal(Transactions()).ReadPageAsync(page.NextStreamId,exactStream:stream)).StreamsRead.Should().Be(0);
    }

    [Fact]
    public async Task Repeated_pass_discovers_a_commit_that_was_invisible_during_the_previous_pass()
    {
        var first=Snapshot();var stream=Stream(first);await Save(stream,first,0);
        var second=first with { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),WorkflowRevision=2,
            State=first.State with { WorkflowRevision=2 } };
        var appended=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending=Transactions().ExecuteAsync(async(db,ct)=>
        {
            await db.AppendAsync(stream,second.CommandId,second,1,ct);
            appended.SetResult();await release.Task.WaitAsync(TimeSpan.FromSeconds(10));return true;
        });
        try
        {
            await appended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var journal=new FinancialWorkflowRecoveryJournal(Transactions());
            (await journal.ReadPageAsync(0,exactStream:stream)).Snapshots.Single().WorkflowRevision.Should().Be(1);
        }
        finally { release.TrySetResult();await pending; }
        (await new FinancialWorkflowRecoveryJournal(Transactions()).ReadPageAsync(0,exactStream:stream))
            .Snapshots.Single().WorkflowRevision.Should().Be(2);
    }

    [Fact]
    public async Task Rolled_back_snapshot_cannot_replace_the_recoverable_committed_request()
    {
        var first=Snapshot();var stream=Stream(first);await Save(stream,first,0);
        var second=first with { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),WorkflowRevision=2,
            State=first.State with { WorkflowRevision=2 } };
        await FluentActions.Awaiting(()=>Transactions().ExecuteAsync<bool>(async(db,ct)=>
        {
            await db.AppendAsync(stream,second.CommandId,second,1,ct);
            throw new InvalidOperationException("Deliberate pre-commit failure");
        })).Should().ThrowAsync<InvalidOperationException>();
        var page=await new FinancialWorkflowRecoveryJournal(Transactions()).ReadPageAsync(0,exactStream:stream);
        page.Snapshots.Single().CommandId.Should().Be(first.CommandId);
    }

    [Fact]
    public async Task Invalid_latest_snapshot_is_reported_without_blocking_pagination_or_replaying_an_older_snapshot()
    {
        var first=Snapshot();var stream=Stream(first);await Save(stream,first,0);
        var invalid=first with { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),WorkflowRevision=2 };
        await Save(stream,invalid,1);
        var journal=new FinancialWorkflowRecoveryJournal(Transactions());
        var page=await journal.ReadPageAsync(0,exactStream:stream);
        page.StreamsRead.Should().Be(1);page.Snapshots.Should().BeEmpty();
        page.InvalidStreamIds.Should().Equal(page.NextStreamId);
        (await journal.ReadPageAsync(page.NextStreamId,exactStream:stream)).StreamsRead.Should().Be(0);
        var repaired=invalid with { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),WorkflowRevision=3,
            State=first.State with { WorkflowRevision=3 } };
        await Save(stream,repaired,2);
        var next=await journal.ReadPageAsync(0,exactStream:stream);
        next.InvalidStreamIds.Should().BeEmpty();next.Snapshots.Single().WorkflowRevision.Should().Be(3);
    }

    static Task Save(string stream,WorkflowStrategyStateUpdatedEvent value,long version)
        =>Transactions().ExecuteAsync(async(db,ct)=> { await db.AppendAsync(stream,value.CommandId,value,version,ct);return true; });
    static string Stream(WorkflowStrategyStateUpdatedEvent snapshot)
        =>new ActorSubject(ActorType.Command,RedispatchCurrentStrategyPipelineCommand.Actor,"test",snapshot.EntityId.Format()).StreamId;
    static WorkflowStrategyStateUpdatedEvent Snapshot()
    {
        var entity=IntrinsicTimeStrategyWorkflowEntityId.Create(new FuturesItiSignalEntityId(
            "RECOVERY-"+Guid.NewGuid().ToString("N"),DateOnly.FromDateTime(DateTime.UtcNow),TimeFrameType.Daily));
        // Terminal test snapshots cannot accidentally start a workflow in a concurrently running development API.
        var view=new IntrinsicTimeStrategyWorkflowView { EntityId=entity,WorkflowId=StrategyWorkflowId.New(TimeProvider.System),WorkflowRevision=1,
            Status=WorkflowStrategyMachineStatus.Completed,CurrentStage=StrategyWorkflowStage.RiskManagement,
            UpdatedAtUtc=DateTime.UtcNow.AddSeconds(-20),ExpiresAtUtc=DateTime.UtcNow.AddMinutes(1) };
        return new() { Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),EntityId=entity,State=view,WorkflowId=view.WorkflowId,
            WorkflowRevision=1,UpdatedAtUtc=view.UpdatedAtUtc,ReceivedOn=view.UpdatedAtUtc,
            Subject=new(ActorType.Event,WorkflowStrategyStateUpdatedEvent.Actor,WorkflowStrategyStateUpdatedEvent.Verb,entity.Format()) };
    }
}
