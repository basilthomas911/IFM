using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using Xunit.Abstractions;
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
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

[Collection(WorkflowDecisionQualificationCollection.Name)]
public sealed class WorkflowTwoSecondPipelineTests(ITestOutputHelper output)
{
    [Fact]
    public async Task First_workflow_after_actual_startup_preparation_meets_the_original_deadline()
    {
        var preparation=new WorkflowRuntimePreparation(new Domain.Portfolio.OrderComposition.Model.PortfolioEntryDecisionCalculator());
        var startupGc0=GC.CollectionCount(0);var startupGc1=GC.CollectionCount(1);var startupGc2=GC.CollectionCount(2);var startupPause=GC.GetTotalPauseDuration();
        var result=await preparation.PrepareAsync();
        output.WriteLine($"StartupPreparation;ProcessId={Environment.ProcessId};Methods={result.PreparedMethods};Contracts={result.PreparedContracts};Ms={result.Elapsed.TotalMilliseconds:F3}");
        output.WriteLine($"StartupGc;Gen0={GC.CollectionCount(0)-startupGc0};Gen1={GC.CollectionCount(1)-startupGc1};Gen2={GC.CollectionCount(2)-startupGc2};PauseMs={(GC.GetTotalPauseDuration()-startupPause).TotalMilliseconds:F3}");
        Assert.True(preparation.IsReady);Assert.Null(preparation.Failure);
        await RunScenario("LongFuture",TimeFrameType.Daily,runtimePreparation:preparation);
    }
    [Fact]
    public async Task Prepared_noncooperative_input_settles_terminal_state_before_two_seconds()
    {
        var preparation=new WorkflowRuntimePreparation(new Domain.Portfolio.OrderComposition.Model.PortfolioEntryDecisionCalculator());
        await preparation.PrepareAsync();
        await RunScenario("BearPutDebit",TimeFrameType.Weekly,"BlockedMarket",runtimePreparation:preparation);
    }
    [Theory]
    [InlineData("LongFuture", TimeFrameType.Daily)]
    [InlineData("BearPutDebit", TimeFrameType.Weekly)]
    [InlineData("ShortBalancedIronCondor", TimeFrameType.Monthly)]
    public async Task Cached_pipeline_finishes_or_rejects_current_options_policy_under_two_seconds_without_input_io_or_retries(string variant, TimeFrameType horizon)
    {
        await RunScenario(variant,horizon,allowColdStart:true);
        await RunScenario(variant,horizon);
    }

    [Theory]
    [InlineData("MissingOptionCache")]
    [InlineData("MissingCompiledSelection")]
    [InlineData("Backwardation")]
    [InlineData("Expired")]
    [InlineData("BlockedMarket")]
    public async Task Adverse_inputs_end_once_without_fetch_retry_or_late_state_mutation(string scenario)
        => await RunScenario("BearPutDebit",TimeFrameType.Weekly,scenario);

    [Fact]
    public async Task Three_timeframes_run_concurrently_with_independent_ready_and_cold_input_outcomes()
    {
        var clock=new FixedClock(new(2026,11,20,15,0,1,DateTimeKind.Utc));
        await using var executor=new WorkflowDecisionExecutor(concurrency:4,capacity:32,timeProvider:clock);
        var barrier=new ReadyBarrier(3);
        await Task.WhenAll(RunScenario("LongFuture",TimeFrameType.Daily,"Ready",clock,executor,barrier),
            RunScenario("BearPutDebit",TimeFrameType.Weekly,"MissingEma",clock,executor,barrier),
            RunScenario("ShortBalancedIronCondor",TimeFrameType.Monthly,"Ready",clock,executor,barrier));
    }

    async Task RunScenario(string variant,TimeFrameType horizon,string condition="Ready",FixedClock? sharedClock=null,WorkflowDecisionExecutor? sharedExecutor=null,ReadyBarrier? barrier=null,bool allowColdStart=false,WorkflowRuntimePreparation? runtimePreparation=null)
    {
        var at = new DateTime(2026, 11, 20, 15, 0, 1, DateTimeKind.Utc);
        var clock = sharedClock??new FixedClock(at);
        var contract = "ESZ6.QUAL."+Guid.NewGuid().ToString("N");
        var options = horizon != TimeFrameType.Daily;
        var assigned = true;
        var scenario = TomasAI.IFM.Tests.Support.DailyFuturesGateTestScenario.Create(timestamp: at, contractId: contract);
        var assessment = new MarketConditionAssessmentCalculator().Calculate(scenario.Command, scenario.Snapshot, scenario.Command.CommandId);
        var selection = await TradeSelection.TradeSelectionFixture.Command(variant, horizon, atUtc: at,
            contractId: contract, compositionReady: true, cacheDefaults: options, actualAssessmentCommand: options ? null : scenario.Command,
            actualAssessmentEnvelope: options ? null : StrategyStageResultEnvelope.CreateAssessment(assessment), preserveAssessmentTime: true);
        var readyOptions = options ? await OrderComposer.CompositionFixture.Command(variant,horizon,atUtc:at,actualSelectionCommand:selection,cacheDefaults:true) : null;
        var marketContext = options ? MarketCondition.AssessmentFixture.Command(horizon,at,contract) : scenario.Command;
        var observationSnapshot = options ? TradeSelection.TradeSelectionFixture.Snapshot(marketContext) with
        { ReferenceInstrumentId=contract, Observations = [.. TradeSelection.TradeSelectionFixture.Snapshot(marketContext).Observations.Select(observation=>observation with{ObservedAtUtc=at,ReceivedAtUtc=at}),
            new() { SourceId="VxFront",Value=condition=="Backwardation"?22:18,Reason="VXF1",ObservedAtUtc=at,ReceivedAtUtc=at,Sequence=1,Availability=Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model.MarketSourceAvailability.Available,Validity=Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model.MarketSourceValidity.Valid },
            new() { SourceId="VxSecond",Value=19,Reason="VXF2",ObservedAtUtc=at,ReceivedAtUtc=at,Sequence=1,Availability=Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model.MarketSourceAvailability.Available,Validity=Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model.MarketSourceValidity.Valid }] } : scenario.Snapshot;
        var original = selection.SelectionBinding;
        var universe = new StrategySelectionUniverse { WorkflowId = selection.WorkflowId.Value, WorkflowRevision = 3,
            CorrelationId = selection.CorrelationId, InstrumentRoot = "ES", TargetHorizon = horizon,
            FrozenAtUtc = original.FrozenAtUtc, ValidUntilUtc = original.ValidUntilUtc,
            DeploymentKeys = original.DeploymentSnapshots.Select(value => value.DeploymentKey).ToArray() };
        var binding = TradeSelectionContracts.Seal(original with { SchemaVersion = 2, PortfolioSnapshot = new(),
            StrategyUniverse = universe with { PayloadSha256 = TradeSelectionContracts.EvidenceHash(universe) } });
        var parameters = selection.WorkflowView.RegimeDiscoveryParameterSet;
        var reference = new ParameterVersionRef(parameters.ParameterSetId, parameters.Version, ParameterSchemaRegistry.RegimeComponent,
            RegimeDiscoveryParameterPayload.ComputeSha256(parameters));
        var version = new ParameterSetVersion(reference, "prepared-test", "", 1, ParameterVersionStatus.Published,
            RegimeDiscoveryParameterPayload.Serialize(parameters), at.AddDays(-1), "test");
        var assignment = new ParameterAssignmentRevision(Guid.NewGuid(), new("strategy-workflow", "test", "regime-discovery", reference.ComponentCode, "{}", ""),
            1, reference, true, ParameterApplicationPolicy.NextStartup, at.AddDays(-1), "test");
        var parameterRuntime = Substitute.For<IParameterRuntimeSnapshot>();
        var run = Guid.NewGuid(); parameterRuntime.Enabled.Returns(true); parameterRuntime.RunId.Returns(run);
        parameterRuntime.Resolve(Arg.Any<string>(), Arg.Any<TimeFrameType>()).Returns(new ParameterRuntimeResolution(true, false, new(run, assignment, version)));
        var frame = EmaStackRegimePolicy.Frame(horizon);
        var window = RsiHistoricalSeedWindowModel.Create(frame, 1, new(at), new CmeFuturesMarketSessionCalendar())[0];
        RegimeDiscoverySignalCacheAdapter.Publish(new FuturesEmaSignalReadModel { IsWarm = condition!="MissingEma", Ema20 = horizon == TimeFrameType.Weekly ? 4999 : horizon == TimeFrameType.Monthly ? 5000 : 5003,
            Ema50 = 5001, Ema200 = horizon == TimeFrameType.Weekly ? 5003 : 4999,
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
        var currentPrice=new PricePublication(price);
        market.TryGetLastTickPrice(contract, out Arg.Any<FuturesMarketPriceSnapshot>()).Returns(call => { call[1] = Volatile.Read(ref currentPrice).Value; return true; });
        market.GetFuturesMarketHealth(contract).Returns(new FuturesMarketHealthSnapshot(true, true,
            $"{generation.Dataset}|{generation.WorkerInstanceId:N}|{generation.GenerationId:N}|1", generation.ValueDate, new(at), 45));
        routes.SaveAsync(Arg.Any<CompositionRoutePlan>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        using var optionCache = new Application.MarketData.OptionChainCache.OptionChainCache(clock);
        if (readyOptions is not null && condition!="MissingOptionCache")
        {
            var snapshot = Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.CompositionSnapshotAdapter.To(readyOptions.MarketSnapshot);
            var profile = SelectionConstructionPolicy.Read(TradeSelectionContracts.Policy(binding, readyOptions.CompositionBinding.Selected.CompositionPolicyReference).PayloadJson).OptionChainCache!;
            var date = snapshot.StrategyOptionChainValueDate!.Value;
            var scope = Application.MarketData.OptionChainCache.StrategyOptionChainScope.Key(profile.ParameterSetId,profile.Version,date,snapshot.StrategyOptionChainBias!,horizon.ToString());
            optionCache.Admit(snapshot.GenerationId);
            Assert.True(optionCache.Publish(snapshot,date,profile.ConfigurationDigest,1,scope));
        }
        market.OptionChainCache.Returns(optionCache);
        var composition = new WorkflowCompositionInputCache(market, routes, admissions, clock);
        await composition.PrepareAsync(binding, default);
        market.ClearReceivedCalls(); routes.ClearReceivedCalls();
        var configurations = new WorkflowSelectionConfigurationCache(clock); if(condition!="MissingCompiledSelection")configurations.PublishCompiled(binding);
        await using var ownedExecutor = sharedExecutor is null?new WorkflowDecisionExecutor(timeProvider:clock):null;
        var executor=sharedExecutor??ownedExecutor!;
        var runtime = new WorkflowCalculationRuntime(executor, clock,
            new Domain.Portfolio.OrderComposition.Model.PortfolioEntryDecisionCalculator(), selectionConfiguration: configurations, compositionConfiguration: composition,preparation:runtimePreparation);
        var trigger = selection.TriggerEvent with { FuturesItiSignal = selection.TriggerEvent.FuturesItiSignal! with
            { IntrinsicTimeMode = IntrinsicTimeModeType.Trending, IntrinsicPrice = 5000, IntrinsicTime = at } };
        var view = selection.WorkflowView with { DecisionRuntimeVersion = 1, DecisionRuntimeSessionId = runtime.SessionId,
            EntityId = selection.WorkflowEntityId with { Assignment = new(1,1,"Qualification",options ? horizon == TimeFrameType.Weekly ? "VerticalSpread" : "IronCondor" : "Future","ES",horizon,binding.Candidates[0].DeploymentKey,1,at.AddDays(-1),null) },
            SelectionBinding = binding, SelectionDispatch = null, WorkflowRevision = 1, CurrentStage = StrategyWorkflowStage.RegimeDiscovery,
            StartedAtUtc = at, UpdatedAtUtc = at, ExpiresAtUtc = at.AddSeconds(10), TriggerEvent = trigger,
            RegimeDiscoveryParameterSet = parameters, RegimeDiscoveryParameterPayloadSha256 = reference.PayloadSha256,
            RegimeDiscovery = new() { ProcessingStatus = StrategyActorProcessingStatus.Processing, StartedAtUtc = at, InputWorkflowRevision = 1 },
            MarketCondition = new(), TradeSelection = new(), OrderComposition = new(), RiskManagement = new(),
            DailyFuturesEntry = options?null:new() { DcTriggerId = Guid.NewGuid(), ContractId = contract, ValueDate = trigger.EntityId.ValueDate,
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
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(call=>ValueTask.FromResult((observationSnapshot with{EvaluatedAtUtc=call.Arg<DateTime>()}).Seal()));
        observations.CaptureOptionObservationAsync(Arg.Any<Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentParameterSet>(),
            Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(call=>ValueTask.FromResult((observationSnapshot with{EvaluatedAtUtc=call.Arg<DateTime>()}).Seal()));
        context.PreparedMarketContext.Returns(observations);
        var dates = Substitute.For<Domain.MarketData.Shared.IValueDateProvider>(); dates.ValueDate.Returns(generation.ValueDate); context.ValueDateProvider.Returns(dates);
        var funds = Substitute.For<IPortfolioDecisionStateReader>();
        var financial = new PortfolioDecisionFinancialState(new FinancialBookConfiguration { PortfolioId = 1, Currency = "USD", MigrationQualified = true }, 1, [], at, at.AddSeconds(3), 1);
        funds.TryAcquire(1, 1, Arg.Any<DateTime>(), out Arg.Any<PortfolioDecisionFinancialState?>()).Returns(call => { call[3] = financial; return true; });
        context.FinancialDecisionState.Returns(funds);
        var owner = Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>(); owner.TimeProvider.Returns(clock);
        owner.Logger.Returns(NullLogger<IntrinsicTimeStrategyWorkflowCommandActor>.Instance); owner.DecisionRuntime.Returns(runtime);
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<WorkflowDecisionBatchRunner>>();
        var lateMarket=new TaskCompletionSource<Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(condition=="BlockedMarket")observations.CaptureOptionObservationAsync(Arg.Any<Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentParameterSet>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(_=>new ValueTask<Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentSnapshot>(lateMarket.Task));
        using var producerStop=new CancellationTokenSource();
        long producerSequence=45;
        var producer=Task.Run(async()=>
        {
            try
            {
                while(!producerStop.IsCancellationRequested)
                {
                    var observed=clock.GetUtcNow();
                    var liveQuote=new FuturesMarketQuoteSnapshot(5000m,10,5000.25m,10,1,1,Interlocked.Increment(ref producerSequence),observed.AddMilliseconds(-20),observed.AddMilliseconds(-10));
                    var livePrice=new FuturesMarketPriceSnapshot(contract,123,1,Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures,generation.ValueDate,liveQuote,null);
                    Volatile.Write(ref currentPrice,new PricePublication(livePrice));
                    await Task.Delay(10,producerStop.Token);
                }
            }
            catch(OperationCanceledException)when(producerStop.IsCancellationRequested){}
        });
        var calculations = new ConcurrentDictionary<string,List<double>>(StringComparer.Ordinal);
        var boundaries=new ConcurrentDictionary<string,double>(StringComparer.Ordinal);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meter) => { if(instrument.Meter.Name==WorkflowDecisionTelemetry.MeterName) meter.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<double>((instrument,value,tags,_) =>
        {
            if(instrument.Name is not("ifm.strategy.decision.calculation" or "ifm.strategy.decision.stage"))return;
            var stage="";var timeframe="";foreach(var tag in tags){if(tag.Key=="stage")stage=tag.Value?.ToString()??"";if(tag.Key=="timeframe")timeframe=tag.Value?.ToString()??"";}
            if(timeframe!=horizon.ToString())return;
            if(instrument.Name=="ifm.strategy.decision.stage"){boundaries[stage]=value;return;}
            var values=calculations.GetOrAdd(stage,_=>[]);lock(values)values.Add(value);
        });
        listener.Start(); if(barrier is not null)await barrier.WaitAsync();
        if(condition=="Expired")clock.Advance(TimeSpan.FromSeconds(3));
        var batchGc0=GC.CollectionCount(0);var batchGc1=GC.CollectionCount(1);var batchGc2=GC.CollectionCount(2);var batchPause=GC.GetTotalPauseDuration();
        clock.Start();var elapsed=Stopwatch.StartNew();
        try{await new WorkflowDecisionBatchRunner(() => context, logger).RunAsync(owner, state);}
        finally{elapsed.Stop();await producerStop.CancelAsync();await producer;}
        if(condition=="BlockedMarket")
        {
            var eventCount=state.Events.Count;var stopped=MessagePack.MessagePackSerializer.Serialize(state.CurrentView);
            lateMarket.TrySetResult((observationSnapshot with{EvaluatedAtUtc=clock.GetUtcNow().UtcDateTime}).Seal());
            await Task.Delay(50);Assert.Equal(eventCount,state.Events.Count);Assert.Equal(stopped,MessagePack.MessagePackSerializer.Serialize(state.CurrentView));
        }
        output.WriteLine($"Scenario={condition};Horizon={horizon};Family={variant};WholeCachedBatchMs={elapsed.Elapsed.TotalMilliseconds:F3};StageMs={string.Join(';',calculations.Select(pair=>pair.Key+":"+string.Join(',',pair.Value.Select(value=>value.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)))))}");
        output.WriteLine($"DecisionGc;Gen0={GC.CollectionCount(0)-batchGc0};Gen1={GC.CollectionCount(1)-batchGc1};Gen2={GC.CollectionCount(2)-batchGc2};PauseMs={(GC.GetTotalPauseDuration()-batchPause).TotalMilliseconds:F3};BoundaryMs={string.Join(';',boundaries.Select(pair=>pair.Key+":"+pair.Value.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)))}");
        if(allowColdStart)
        {
            output.WriteLine($"ColdStartEvidence:Terminal={state.DecisionView!.StopReasonCode};CompletedStages={calculations.Count};BudgetExceeded={elapsed.Elapsed>=TimeSpan.FromSeconds(2)}");
            Assert.NotEqual(WorkflowStrategyMachineStatus.Started,state.DecisionView.Status);
            // This is fail-closed cold-start evidence, not a two-second success
            // qualification. Detailed reason/duration are retained above, and the
            // following separately prepared ready run must satisfy the exact path.
            Assert.True(state.DecisionView.EntryCommit is null || state.DecisionView.EntryCommit.Phase==WorkflowEntryCommitPhase.Rejected);
            if(state.DecisionView.EntryCommit is{} coldCommit)Assert.Empty(coldCommit.Approval.Funds);
            return;
        }
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2),"Cached decision boundary exceeded its complete terminal-settlement budget.");
        output.WriteLine("Terminal="+state.CurrentView!.StopReasonCode+";failures="+state.CurrentView.TradeSelection.Failure?.ErrorMessage+";"+state.CurrentView.OrderComposition.Failure?.ErrorMessage+";"+state.CurrentView.MarketCondition.Failure?.ErrorMessage+";logs="+string.Join(";",logger.ReceivedCalls().SelectMany(call=>call.GetArguments()).OfType<Exception>()));
        if(condition!="Ready")
        {
            Assert.NotEqual(WorkflowStrategyMachineStatus.Started,state.CurrentView!.Status);
            Assert.Null(state.CurrentView.EntryCommit);
            Assert.Empty(funds.ReceivedCalls());
            Assert.All(calculations,entry=>Assert.Single(entry.Value));
            Assert.Empty(routes.ReceivedCalls());
            Assert.DoesNotContain(market.ReceivedCalls(),call=>call.GetMethodInfo().Name.Contains("Async",StringComparison.Ordinal));
            Assert.DoesNotContain(context.ReceivedCalls(),call=>call.GetMethodInfo().Name.StartsWith("Request",StringComparison.Ordinal)||call.GetMethodInfo().Name.StartsWith("Send",StringComparison.Ordinal));
            if(condition=="Expired")Assert.Equal("WorkflowExecutionExpired",state.CurrentView.StopReasonCode);
            if(condition=="BlockedMarket")Assert.Equal("WF.DECISION.INTERRUPTED",state.CurrentView.StopReasonCode);
            if(condition=="Expired")Assert.Empty(calculations);
            if(condition=="MissingEma"){Assert.True(calculations.Count<=1);Assert.Empty(observations.ReceivedCalls());Assert.NotEqual(StrategyActorProcessingStatus.Completed,state.CurrentView.RegimeDiscovery.ProcessingStatus);}
            if(condition=="Backwardation")Assert.Equal("MC.OPTIONS.VX_BACKWARDATION",state.CurrentView.StopReasonCode);
            if(condition=="BlockedMarket")Assert.Single(observations.ReceivedCalls());
            var errors=string.Join(";",logger.ReceivedCalls().SelectMany(call=>call.GetArguments()).OfType<Exception>());
            Assert.DoesNotContain("WF.DECISION.IO_FORBIDDEN",errors,StringComparison.Ordinal);
            return;
        }
        Assert.All(calculations,entry=>{Assert.Single(entry.Value);Assert.All(entry.Value,value=>Assert.InRange(value,0,1999.999));});
        if(options)
        {
            Assert.Equal(3,calculations.Count);Assert.Equal("TS.EVIDENCE.UNKNOWN",state.CurrentView!.StopReasonCode);
            Assert.Equal(WorkflowStrategyMachineStatus.Completed,state.CurrentView.Status);
            Assert.Null(state.CurrentView.EntryCommit);Assert.Empty(funds.ReceivedCalls());
            var rejected=state.CurrentView.TradeSelection.Result!.SelectionResult!.GlobalEvidence.Where(row=>row.Status==SelectionRuleStatus.Rejected).ToArray();
            Assert.Contains(rejected,row=>row.RuleId=="G19"&&row.ReasonCode=="TS.EVIDENCE.UNKNOWN");
            output.WriteLine("OptionsEligibilityGap="+string.Join(';',rejected.Select(row=>row.RuleId+":"+row.ReasonCode)));
            Assert.Empty(routes.ReceivedCalls());Assert.DoesNotContain(market.ReceivedCalls(),call=>call.GetMethodInfo().Name.Contains("Async",StringComparison.Ordinal));
            Assert.DoesNotContain(context.ReceivedCalls(),call=>call.GetMethodInfo().Name.StartsWith("Request",StringComparison.Ordinal)||call.GetMethodInfo().Name.StartsWith("Send",StringComparison.Ordinal));
            return;
        }
        Assert.Equal(5,calculations.Count);

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
        routes.ReceivedCalls().Should().BeEmpty();
        market.ReceivedCalls().Should().OnlyContain(call => call.GetMethodInfo().Name == "TryGetLastTickPrice" || call.GetMethodInfo().Name == "GetFuturesMarketHealth" || call.GetMethodInfo().Name=="get_OptionChainCache");
        Assert.Single(observations.ReceivedCalls());
        context.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name.StartsWith("Request", StringComparison.Ordinal)
            || call.GetMethodInfo().Name.StartsWith("Send", StringComparison.Ordinal));
    }
    sealed class FixedClock(DateTime now) : TimeProvider
    {
        long started;
        long offset;
        public void Start()=>Interlocked.CompareExchange(ref started,Stopwatch.GetTimestamp(),0);
        public void Advance(TimeSpan value)=>Interlocked.Add(ref offset,value.Ticks);
        public override DateTimeOffset GetUtcNow()=>new DateTimeOffset(now).AddTicks(Volatile.Read(ref offset)).Add(Volatile.Read(ref started) is var stamp && stamp!=0?Stopwatch.GetElapsedTime(stamp):TimeSpan.Zero);
    }
    sealed record PricePublication(FuturesMarketPriceSnapshot Value);
    sealed class ReadyBarrier(int count)
    {
        int arrived;readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task WaitAsync(){if(Interlocked.Increment(ref arrived)==count)ready.TrySetResult();return ready.Task;}
    }
}
