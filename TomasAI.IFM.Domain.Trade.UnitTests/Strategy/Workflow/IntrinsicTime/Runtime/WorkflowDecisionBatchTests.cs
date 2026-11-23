using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

[Collection(WorkflowDecisionQualificationCollection.Name)]
public sealed partial class WorkflowDecisionBatchTests
{
    [Theory]
    [InlineData("LongFuture", TimeFrameType.Daily)]
    [InlineData("BearPutDebit", TimeFrameType.Weekly)]
    [InlineData("ShortBalancedIronCondor", TimeFrameType.Monthly)]
    public async Task Local_continuations_preserve_selection_and_collect_evidence_without_transport_or_storage(string variant, TimeFrameType horizon)
    {
        var at = new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);
        var command = await TradeSelection.TradeSelectionFixture.Command(variant, horizon, atUtc: at, compositionReady: true);
        var original = command.SelectionBinding;
        var universe = new StrategySelectionUniverse
        {
            WorkflowId = command.WorkflowId.Value, WorkflowRevision = command.InputWorkflowRevision,
            CorrelationId = command.CorrelationId, InstrumentRoot = TradeSelectionContracts.CommonPolicy(original).InstrumentRoot,
            TargetHorizon = horizon, FrozenAtUtc = original.FrozenAtUtc, ValidUntilUtc = original.ValidUntilUtc,
            DeploymentKeys = original.DeploymentSnapshots.Select(graph => graph.DeploymentKey).ToArray()
        };
        var binding = TradeSelectionContracts.Seal(original with { SchemaVersion = 2, PortfolioSnapshot = new(),
            StrategyUniverse = universe with { PayloadSha256 = TradeSelectionContracts.EvidenceHash(universe) } });
        command = command with { SelectionBinding = binding, WorkflowView = command.WorkflowView with { SelectionBinding = binding } };
        command = TradeSelectionDispatch.Create(command.WorkflowView, Guid.NewGuid());
        var clock = new FixedClock(at.AddMilliseconds(10));
        var configurations = new WorkflowSelectionConfigurationCache(clock);
        configurations.PublishCompiled(command.SelectionBinding);
        await using var executor = new WorkflowDecisionExecutor(timeProvider: clock);
        var runtime = new WorkflowCalculationRuntime(executor, clock, selectionConfiguration: configurations);
        var view = command.WorkflowView with { DecisionRuntimeVersion = 1, DecisionRuntimeSessionId = runtime.SessionId,
            SelectionDispatch = command, TradeSelection = command.WorkflowView.TradeSelection with { StartedAtUtc = at } };
        var source = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification() with
        { EntityId = view.EntityId, WorkflowId = view.WorkflowId, WorkflowRevision = view.WorkflowRevision, State = view };
        var state = new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(source, addEvent: false).Should().BeTrue();
        state.SetPersistedStreamVersion(1);
        var market = Substitute.For<IMarketDataApi>();
        var routes = Substitute.For<ICompositionRoutePlanStore>();
        var context = Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();
        context.TimeProvider.Returns(clock);
        context.Logger.Returns(NullLogger<IntrinsicTimeStrategyWorkflowRealtimeActor>.Instance);
        context.DecisionRuntime.Returns(runtime);
        context.CompositionInputs.Returns(new WorkflowCompositionInputCache(market, routes, new DatasetWorkerAdmissionRegistry(), clock));
        var dates = Substitute.For<TomasAI.IFM.Domain.MarketData.Shared.IValueDateProvider>();
        dates.ValueDate.Returns(DateOnly.FromDateTime(at));
        context.ValueDateProvider.Returns(dates);
        var owner = Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        owner.TimeProvider.Returns(clock);
        owner.Logger.Returns(NullLogger<IntrinsicTimeStrategyWorkflowCommandActor>.Instance);
        owner.DecisionRuntime.Returns(runtime);
        var expected = TradeSelectionEvaluator.Evaluate(command);

        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<WorkflowDecisionBatchRunner>>();
        await new WorkflowDecisionBatchRunner(() => context, logger).RunAsync(owner, state);

        var result = state.CurrentView!;
        result.TradeSelection.Result.Should().NotBeNull(result.StopReasonCode + ":" + result.TradeSelection.Failure?.ErrorMessage);
        TradeSelectionContracts.DecisionEvidenceHash(result.TradeSelection.Result!.SelectionResult!).Should().Be(TradeSelectionContracts.DecisionEvidenceHash(expected));
        result.Status.Should().NotBe(WorkflowStrategyMachineStatus.Started);
        result.EntryCommit.Should().BeNull();
        result.StopReasonCode.Should().NotBe("WF.DECISION.INTERRUPTED", string.Join(";", logger.ReceivedCalls()
            .SelectMany(call => call.GetArguments()).OfType<Exception>().Select(error => error.ToString())));
        state.Events.Count.Should().BeGreaterThanOrEqualTo(2, "selection and the unavailable composition input are separate recorded outcomes");
        state.PersistedStreamVersion.Should().Be(1, "the owner has not yet committed the evidence batch");
        market.ReceivedCalls().Should().BeEmpty();
        routes.ReceivedCalls().Should().BeEmpty();
        context.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name.StartsWith("Send", StringComparison.Ordinal)
            || call.GetMethodInfo().Name.StartsWith("Request", StringComparison.Ordinal));
    }

    sealed class FixedClock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now); }
}
