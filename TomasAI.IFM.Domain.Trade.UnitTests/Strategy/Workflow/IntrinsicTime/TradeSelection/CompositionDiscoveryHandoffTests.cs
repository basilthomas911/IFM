using System.Collections.Immutable;
using Newtonsoft.Json;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

public sealed class CompositionDiscoveryHandoffTests
{
    [Theory]
    [InlineData(2, false, false)] [InlineData(2, true, false)]
    [InlineData(4, true, false)] [InlineData(4, true, true)]
    [InlineData(0, true, false)] [InlineData(0, false, false)] [InlineData(0, true, true)]
    public async Task Discovery_release_requires_realized_ownership_and_never_recaptures_accepted_evidence(int count, bool ready, bool replaced)
    {
        var at = DateTimeOffset.UtcNow.AddMinutes(-5); // Accepted evidence may be expired; handoff must not recapture it.
        var generation = Guid.NewGuid(); var workflowId = Guid.NewGuid(); var planId = new string('a', 64);
        var selected = new CompositionContractSelection(planId, Enumerable.Range(0, count).Select(x => "leg-" + x).ToImmutableArray());
        var key = new CompositionPreparationKey(workflowId, 5, new('b', 64));
        var request = new CompositionSnapshotRequest(Guid.NewGuid(), planId, "Daily", generation, at, at.AddSeconds(2), true);
        var instruments = selected.ContractIds.Select(id => new CompositionInstrumentSnapshot(new(id,
            new(id, 10, 11, 1, 1, at, at, 1, generation), null, 5000, true, null), null)).ToImmutableArray();
        var snapshot = new MarketCompositionSnapshot(1, request.SnapshotId, planId, "fixture", "Daily", generation, at, at.AddSeconds(1), instruments, "");
        snapshot = snapshot with { Digest = PricingSemanticHash.Compute(snapshot) };
        var prepared = new CompositionPreparation(2, key, "GLBX.MDP3", request, snapshot, at, "", new(planId, Guid.NewGuid(), generation));
        prepared = prepared with { Digest = PricingSemanticHash.Compute(prepared) };
        var workflow = new WorkflowStrategyStateUpdatedEvent { Id = Guid.NewGuid(), WorkflowId = new(workflowId),
            State = new() { Status = count == 0 ? WorkflowStrategyMachineStatus.Completed : WorkflowStrategyMachineStatus.Started,
                Outcome = count == 0 ? StrategyWorkflowOutcome.NoTrade : StrategyWorkflowOutcome.None, CompositionContracts = count == 0 ? null : selected, CompositionDispatch = new() { MarketEvidence = CompositionPreparationAcceptance.Reference(prepared) } } };
        var row = new EventLogReadModel(1, workflow.EventName, workflow.GetType().AssemblyQualifiedName!, 99, TomasAI.IFM.Shared.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(workflow), Guid.NewGuid(), at.ToString("O"), 6);
        var journal = Substitute.For<ICommittedBusinessEventJournal>(); journal.ReadPendingHandoffsAsync(default).Returns([row]);
        var intent = Substitute.For<IDurableSubscriptionIntentStore>();
        intent.ReadAsync("IFM", "GLBX.MDP3", default).Returns(new DurableSubscriptionSnapshot(1, "IFM", "GLBX.MDP3", 1,
            [new("IntrinsicTimeWorkflow:" + workflow.WorkflowId, 6, workflow.Id, new('c', 64), new("IntrinsicTimeWorkflow", workflow.WorkflowId.ToString(), "SelectedContracts"),
                count == 0 ? DurableAuthorityStatus.Terminal : DurableAuthorityStatus.Active, "CommittedActive", selected.ContractIds.Select(id => new DurableSubscriptionLease(Guid.NewGuid(), 1,
                    SubscriptionLeasePurpose.Strategy, new("Databento", "GLBX.MDP3", id, "mbp-1", SubscriptionAssetKind.FuturesOption, "ES-future", planId))).ToArray())]));
        var preparations = Substitute.For<ICompositionPreparationStore>(); preparations.ReadAsync(key, default).Returns(prepared);
        var market = Substitute.For<ICompositionMarketDataApi>(); market.ReleaseAsync("GLBX.MDP3", prepared.DiscoveryLease!, default).Returns(new WorkerOptionChainResult(true, null));
        var runtime = Substitute.For<IDurableCompositionReconciler>();
        runtime.ReconcileOnceAsync(default).Returns(new DurableRealization(1, replaced ? Guid.NewGuid() : generation, ready));
        var handoff = new CompositionDiscoveryHandoff(journal, intent, preparations, market, runtime);
        await handoff.CompletePendingAsync(default);
        await journal.Received(ready ? 1 : 0).CompleteHandoffAsync(99, default);
        await market.Received(ready && !replaced ? 1 : 0).ReleaseAsync("GLBX.MDP3", prepared.DiscoveryLease!, default);
        await market.DidNotReceiveWithAnyArgs().CaptureAsync(default!, default!, default);
        await preparations.DidNotReceiveWithAnyArgs().CommitAsync(default!, default);
    }
}
