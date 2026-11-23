using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Options;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

public sealed partial class WorkflowDecisionBatchTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task All_five_real_operators_consume_ready_inputs_without_io_and_a_rejected_fund_has_no_entry_effect(bool assigned)
    {
        var at = new DateTime(2026, 11, 20, 15, 0, 1, DateTimeKind.Utc);
        var clock = new FixedClock(at);
        const string contract = "ES20991218";
        var scenario = TomasAI.IFM.Tests.Support.DailyFuturesGateTestScenario.Create(timestamp: at, contractId: contract);
        var assessment = new MarketConditionAssessmentCalculator().Calculate(scenario.Command, scenario.Snapshot, scenario.Command.CommandId);
        var selection = await TradeSelection.TradeSelectionFixture.Command("LongFuture", atUtc: at,
            contractId: contract, compositionReady: true, actualAssessmentCommand: scenario.Command,
            actualAssessmentEnvelope: StrategyStageResultEnvelope.CreateAssessment(assessment), preserveAssessmentTime: true);
        var original = selection.SelectionBinding;
        var universe = new StrategySelectionUniverse { WorkflowId = selection.WorkflowId.Value, WorkflowRevision = 3,
            CorrelationId = selection.CorrelationId, InstrumentRoot = "ES", TargetHorizon = TimeFrameType.Daily,
            FrozenAtUtc = original.FrozenAtUtc, ValidUntilUtc = original.ValidUntilUtc,
            DeploymentKeys = original.DeploymentSnapshots.Select(value => value.DeploymentKey).ToArray() };
        var binding = TradeSelectionContracts.Seal(original with { SchemaVersion = 2, PortfolioSnapshot = new(),
            StrategyUniverse = universe with { PayloadSha256 = TradeSelectionContracts.EvidenceHash(universe) } });
        var parameters = scenario.Command.WorkflowView.RegimeDiscoveryParameterSet;
        var reference = new ParameterVersionRef(parameters.ParameterSetId, parameters.Version, ParameterSchemaRegistry.RegimeComponent,
            RegimeDiscoveryParameterPayload.ComputeSha256(parameters));
        var version = new ParameterSetVersion(reference, "prepared-test", "", 1, ParameterVersionStatus.Published,
            RegimeDiscoveryParameterPayload.Serialize(parameters), at.AddDays(-1), "test");
        var assignment = new ParameterAssignmentRevision(Guid.NewGuid(), new("strategy-workflow", "test", "regime-discovery", reference.ComponentCode, "{}", ""),
            1, reference, true, ParameterApplicationPolicy.NextStartup, at.AddDays(-1), "test");
        var parameterRuntime = Substitute.For<IParameterRuntimeSnapshot>();
        var run = Guid.NewGuid(); parameterRuntime.Enabled.Returns(true); parameterRuntime.RunId.Returns(run);
        parameterRuntime.Resolve(Arg.Any<string>(), Arg.Any<TimeFrameType>()).Returns(new ParameterRuntimeResolution(true, false, new(run, assignment, version)));
        var frame = EmaStackRegimePolicy.Frame(TimeFrameType.Daily);
        var window = RsiHistoricalSeedWindowModel.Create(frame, 1, new(at), new CmeFuturesMarketSessionCalendar())[0];
        RegimeDiscoverySignalCacheAdapter.Publish(new FuturesEmaSignalReadModel { IsWarm = true, Ema20 = 5003, Ema50 = 5001, Ema200 = 4999,
            Metadata = new() { SignalKey = new(MarketSeriesIdentity.ForContract(contract), MarketAnalyticsSignalKind.Ema, frame, "ema"),
                ContractId = contract, ValueDate = window.ValueDate, MarketDataAsOfUtc = window.EndUtc.AddSeconds(-1),
                CalculatedAtUtc = new(at), SchemaVersion = 2, IsValid = true, SourceSequence = 1 } });

        var market = Substitute.For<IMarketDataApi>();
        var routes = Substitute.For<ICompositionRoutePlanStore>();
        var admissions = new DatasetWorkerAdmissionRegistry();
        var generation = new DatasetWorkerAdmission("GLBX.MDP3", DateOnly.FromDateTime(at), Guid.NewGuid(), Guid.NewGuid(), 1); admissions.Admit(generation);
        var definition = new FuturesContractV3ReadModel { ContractId = contract, Symbol = "ES", LocalSymbol = "ESZ9", Exchange = "CME", Currency = "USD",
            Dataset = generation.Dataset, SchemaVersion = 1, LastTradeDate = DateOnly.FromDateTime(at.AddDays(90)), LastTradingUtc = new(at.AddDays(90)),
            MultiplierValue = 50, TickSize = .25m, DefinitionDigest = new('a', 64) };
        market.TryGetOnTheRunFuturesContract("ES", out Arg.Any<FuturesContractV3ReadModel>()).Returns(call => { call[1] = definition; return true; });
        var quote = new FuturesMarketQuoteSnapshot(5000m, 10, 5000.25m, 10, 1, 1, 45, new(at.AddMilliseconds(-200)), new(at.AddMilliseconds(-100)));
        var price = new FuturesMarketPriceSnapshot(contract, 123, 1, Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures, generation.ValueDate, quote, null);
        market.TryGetLastTickPrice(contract, out Arg.Any<FuturesMarketPriceSnapshot>()).Returns(call => { call[1] = price; return true; });
        market.GetFuturesMarketHealth(contract).Returns(new FuturesMarketHealthSnapshot(true, true,
            $"{generation.Dataset}|{generation.WorkerInstanceId:N}|{generation.GenerationId:N}|1", generation.ValueDate, new(at), 45));
        routes.SaveAsync(Arg.Any<CompositionRoutePlan>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var composition = new WorkflowCompositionInputCache(market, routes, admissions, clock);
        await composition.PrepareAsync(binding, default);
        market.ClearReceivedCalls(); routes.ClearReceivedCalls();
        var configurations = new WorkflowSelectionConfigurationCache(clock); configurations.PublishCompiled(binding);
        await using var executor = new WorkflowDecisionExecutor(timeProvider: clock);
        var runtime = new WorkflowCalculationRuntime(executor, clock,
            new Domain.Portfolio.OrderComposition.Model.PortfolioEntryDecisionCalculator(), selectionConfiguration: configurations, compositionConfiguration: composition);
        var trigger = selection.TriggerEvent with { FuturesItiSignal = selection.TriggerEvent.FuturesItiSignal! with
            { IntrinsicTimeMode = IntrinsicTimeModeType.Trending, IntrinsicPrice = 5000, IntrinsicTime = at } };
        var view = selection.WorkflowView with { DecisionRuntimeVersion = 1, DecisionRuntimeSessionId = runtime.SessionId,
            EntityId = selection.WorkflowEntityId with { Assignment = selection.WorkflowEntityId.Assignment! with { Deployment = binding.Candidates[0].DeploymentKey } },
            SelectionBinding = binding, SelectionDispatch = null, WorkflowRevision = 1, CurrentStage = StrategyWorkflowStage.RegimeDiscovery,
            StartedAtUtc = at, UpdatedAtUtc = at, ExpiresAtUtc = at.AddSeconds(10), TriggerEvent = trigger,
            RegimeDiscoveryParameterSet = parameters, RegimeDiscoveryParameterPayloadSha256 = reference.PayloadSha256,
            RegimeDiscovery = new() { ProcessingStatus = StrategyActorProcessingStatus.Processing, StartedAtUtc = at, InputWorkflowRevision = 1 },
            MarketCondition = new(), TradeSelection = new(), OrderComposition = new(), RiskManagement = new(),
            DailyFuturesEntry = new() { DcTriggerId = Guid.NewGuid(), ContractId = contract, ValueDate = trigger.EntityId.ValueDate,
                DcAtUtc = at.AddSeconds(-1), DcPrice = 4995, Threshold = 8, Atr = new(contract, 8, at.AddMinutes(-1), at.AddSeconds(-30), 1) } };
        var source = IntrinsicTimeStrategyWorkflowCommandStateTests.CreateStartedSnapshotForQualification() with
        { EntityId = view.EntityId, WorkflowId = view.WorkflowId, WorkflowRevision = 1, State = view, DispatchRegimeDiscovery = true };
        var state = new IntrinsicTimeStrategyWorkflowCommandState(); state.Apply(source, addEvent: false).Should().BeTrue(); state.SetPersistedStreamVersion(1);
        var context = Substitute.For<IIntrinsicTimeStrategyWorkflowRealtimeContext>();
        context.TimeProvider.Returns(clock); context.Logger.Returns(NullLogger<IntrinsicTimeStrategyWorkflowRealtimeActor>.Instance);
        context.Options.Returns(new IntrinsicTimeStrategyWorkflowOptions { LocalDecisionRuntimeEnabled = true });
        context.RegimeDiscoveryExecutionOptions.Returns(new RegimeDiscoveryExecutionOptions { CalculationPolicy = EmaStackRegimePolicy.Version });
        var preparedConfiguration = new WorkflowPreparedConfigurationCache();
        if (!assigned)
        {
            parameterRuntime.Resolve(Arg.Any<string>(), Arg.Any<TimeFrameType>()).Returns(new ParameterRuntimeResolution(false, false, null));
            preparedConfiguration.PublishRegime(TimeFrameType.Daily,
                new(parameters, version.PayloadJson, reference.PayloadSha256, at.AddDays(-1)),
                TomasAI.IFM.Application.Storage.ReadCacheInvalidation.Configuration, at);
        }
        context.ParameterRuntime.Returns(parameterRuntime); context.PreparedConfiguration.Returns(preparedConfiguration);
        context.RegimeDiscoverySnapshotProvider.Returns(new RegimeDiscoveryMarketSignalSnapshotProvider(clock));
        context.DecisionRuntime.Returns(runtime); context.CompositionInputs.Returns(composition);
        var observations = Substitute.For<IMarketConditionAssessmentSnapshotProvider>();
        observations.CaptureDailyFuturesMarketAsync(Arg.Any<Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentParameterSet>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(scenario.Snapshot));
        context.PreparedMarketContext.Returns(observations);
        var dates = Substitute.For<Domain.MarketData.Shared.IValueDateProvider>(); dates.ValueDate.Returns(generation.ValueDate); context.ValueDateProvider.Returns(dates);
        var funds = Substitute.For<IPortfolioDecisionStateReader>();
        var financial = new PortfolioDecisionFinancialState(new FinancialBookConfiguration { PortfolioId = 1, Currency = "USD", MigrationQualified = true }, 1, [], at, at.AddSeconds(3), 1);
        funds.TryAcquire(1, 1, Arg.Any<DateTime>(), out Arg.Any<PortfolioDecisionFinancialState?>()).Returns(call => { call[3] = financial; return true; });
        context.FinancialDecisionState.Returns(funds);
        var owner = Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>(); owner.TimeProvider.Returns(clock);
        owner.Logger.Returns(NullLogger<IntrinsicTimeStrategyWorkflowCommandActor>.Instance); owner.DecisionRuntime.Returns(runtime);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<WorkflowDecisionBatchRunner>>();

        await new WorkflowDecisionBatchRunner(() => context, logger).RunAsync(owner, state);

        var result = state.CurrentView!;
        result.StopReasonCode.Should().Be("RM.PORTFOLIO.NO_TRADE_ORDERS", result.StopReasonCode + ":" + result.RegimeDiscovery.Failure?.ErrorMessage
            + result.MarketCondition.Failure?.ErrorMessage + result.TradeSelection.Failure?.ErrorMessage + result.OrderComposition.Failure?.ErrorMessage
            + result.RiskManagement.Failure?.ErrorMessage + result.RiskManagement.Failure?.ErrorData);
        result.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        result.MarketCondition.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        result.TradeSelection.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        result.OrderComposition.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        result.RiskManagement.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        result.EntryCommit!.Phase.Should().Be(WorkflowEntryCommitPhase.Rejected);
        result.EntryCommit.Approval.Funds.Should().BeEmpty();
        result.EntryCommit.Approval.HasValidFingerprint().Should().BeTrue();
        var history = Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskHistoryIdentity.Row(state.LatestSource!);
        history!.PortfolioId.Should().Be(1); history.FundId.Should().Be(1);
        history.InvocationId.Should().Be(result.EntryCommit.Approval.Request.OperationId);
        history.Outcome.Should().Be("NoTrade");
        if (Environment.GetEnvironmentVariable("IFM_PREPARED_WORKFLOW_BENCHMARK_FIXTURES") is { Length: > 0 } directory)
            await File.WriteAllBytesAsync(Path.Combine(directory, "ready-futures-no-trade-view.msgpack"),
                Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(result));
        routes.ReceivedCalls().Should().BeEmpty();
        market.ReceivedCalls().Should().OnlyContain(call => call.GetMethodInfo().Name == "TryGetLastTickPrice" || call.GetMethodInfo().Name == "GetFuturesMarketHealth");
        context.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name.StartsWith("Request", StringComparison.Ordinal)
            || call.GetMethodInfo().Name.StartsWith("Send", StringComparison.Ordinal));
    }
}
