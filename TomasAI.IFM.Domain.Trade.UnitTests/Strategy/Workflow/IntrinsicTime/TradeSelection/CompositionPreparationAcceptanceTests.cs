using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

public sealed class CompositionPreparationAcceptanceTests
{
    [Fact]
    public async Task Selected_contracts_survive_committed_workflow_replay_and_compatibility_projection()
    {
        var fixture = await Reserved();
        var selection = new Shared.Strategy.Workflow.IntrinsicTime.Model.CompositionContractSelection(new('d', 64), ["leg-a", "leg-b"]);
        fixture.Restore(fixture.State.CurrentView! with { CompositionContracts = selection });
        Assert.Equal(selection, fixture.State.CurrentView!.CompositionContracts);
        Assert.Equal(selection, fixture.State.LatestWorkflow!.CompositionContracts);
    }

    [Fact]
    public async Task Acceptance_pins_market_evidence_and_complete_dispatch_to_next_workflow_revision()
    {
        var fixture = await Reserved(); var view = fixture.State.CurrentView!;
        var prepared = Prepared(view, fixture.Clock.GetUtcNow());
        var dispatch = CompositionPreparationAcceptance.Accept(view, prepared,
            CompositionPreparationAcceptance.Reference(prepared), Guid.NewGuid(), fixture.Clock.Now);
        Assert.Equal(view.WorkflowRevision + 1, dispatch.InputWorkflowRevision);
        Assert.Equal(prepared.Digest, dispatch.MarketEvidence!.PreparationSha256);
        var bytes = MessagePackBinarySerializer.Shared.Serialize(dispatch)!;
        var restored = MessagePackBinarySerializer.Shared.Deserialize<StartOrderCompositionPipelineCommand>(bytes)!;
        Assert.Equal(dispatch.MarketEvidence, restored.MarketEvidence);
        Assert.Equal(dispatch.Reservation!.Order.OrderId, restored.Reservation!.Order.OrderId);
        Assert.Equal(dispatch.SelectionBinding!.PayloadSha256, restored.SelectionBinding!.PayloadSha256);
        Assert.Null(restored.WorkflowState.CompositionDispatch); // No recursive embedded request.
    }

    [Theory]
    [InlineData("expired")] [InlineData("revision")] [InlineData("snapshot")] [InlineData("horizon")]
    public async Task Mismatched_or_expired_evidence_cannot_be_accepted(string change)
    {
        var fixture = await Reserved(); var view = fixture.State.CurrentView!; var now = fixture.Clock.GetUtcNow();
        var prepared = Prepared(view, now); var reference = CompositionPreparationAcceptance.Reference(prepared);
        if (change == "expired") now = prepared.Snapshot.ValidUntilUtc;
        if (change == "revision") view = view with { WorkflowRevision = view.WorkflowRevision + 1 };
        if (change == "snapshot") reference = reference with { SnapshotId = Guid.NewGuid() };
        if (change == "horizon") prepared = Prepared(view, now, "Monthly");
        Assert.Throws<InvalidDataException>(() => CompositionPreparationAcceptance.Accept(view, prepared, reference, Guid.NewGuid(), now.UtcDateTime));
    }

    static async Task<TradeSelectionHandoffTests.Fixture> Reserved()
    {
        var f = await TradeSelectionHandoffTests.Fixture.Create(); f.Accept();
        f.Reserve(new PortfolioFundCompositionAggregate().Reserve(f.State.CurrentView!.CompositionHandoff!.Request,
            f.Command.SelectionBinding.PortfolioSnapshot, 7001, [8001], f.Clock.Now, "preparation-test"));
        return f;
    }

    static CompositionPreparation Prepared(Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView view,
        DateTimeOffset at, string? horizon = null)
    {
        var request = new CompositionSnapshotRequest(Guid.NewGuid(), "complete", horizon ?? view.TriggerEvent.EntityId.TimePeriod.ToString(),
            Guid.NewGuid(), at, at.AddSeconds(1), false);
        var snapshot = new MarketCompositionSnapshot(1, request.SnapshotId, request.ScopeId, "fixture-v1", request.Horizon,
            request.GenerationId, at, at.AddSeconds(1), [], "");
        snapshot = snapshot with { Digest = PricingSemanticHash.Compute(snapshot) };
        var prepared = new CompositionPreparation(1, CompositionPreparationAcceptance.Key(view), "GLBX.MDP3", request, snapshot, at, "");
        return prepared with { Digest = PricingSemanticHash.Compute(prepared) };
    }
}
