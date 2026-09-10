using System.Collections.Immutable;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    // Actual workflow command, projector, realtime dispatcher and all five Function actors.
    // Portfolio command/query/function actors use real NATS and durable stores.
    // External market observations and initial test authority remain deterministic fixtures.
    [Theory, Trait("Category", "FullWorkflowRisk")]
    [InlineData(TimeFrameType.Daily, "LongFuture")]
    [InlineData(TimeFrameType.Weekly, "ShortFuture")]
    [InlineData(TimeFrameType.Monthly, "BullCallDebit")]
    [InlineData(TimeFrameType.Daily, "BearPutDebit")]
    [InlineData(TimeFrameType.Weekly, "ShortBalancedIronCondor")]
    public async Task Workflow_start_runs_every_pipeline_actor_and_authorizes_a_trade(TimeFrameType horizon, string variant)
        => await RunWorkflow(horizon, variant);

    [Theory, Trait("Category", "SuccessiveWorkflowStages")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Workflow_runs_only_through_successive_endpoint(int stageCount)
        => await RunWorkflow(TimeFrameType.Daily, "LongFuture", stageCount);

    [Fact, Trait("Category", "RiskWorkflowTrace")]
    public async Task Workflow_trace_measures_risk_and_authorization_operations()
        => await RunWorkflow(TimeFrameType.Daily, "LongFuture", captureTrace: true);

    static StrategyWorkflowStageState[] PipelineStages(IntrinsicTimeStrategyWorkflowView view)
        => [view.RegimeDiscovery, view.MarketCondition, view.TradeSelection, view.OrderComposition, view.RiskManagement];

    async Task RunWorkflow(TimeFrameType horizon, string variant, int? stageCount = null, bool captureTrace = false,
        WorkflowBenchmarkSettings? benchmark = null, WorkflowBenchmarkWriter? benchmarkWriter = null)
    {
        var spans = new System.Collections.Concurrent.ConcurrentQueue<System.Diagnostics.Activity>();
        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = source => captureTrace && (source.Name == TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.WorkflowTrace.SourceName
                || source.Name == ActorTrace.SourceName || source.Name == "TomasAI.IFM.Domain.Portfolio" || source.Name == "TomasAI.IFM.PortfolioFinancial"),
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = span => spans.Enqueue(span)
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);
        var broker = Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL")
            ?? throw new InvalidOperationException("An isolated test NATS broker is required.");
        Action? releaseFixtureCalls = null;
        var measurements = new System.Collections.Concurrent.ConcurrentDictionary<string, WorkflowMeasurement>(StringComparer.Ordinal);
        var starts = new System.Collections.Concurrent.ConcurrentDictionary<Guid, ExecuteIntrinsicTimeStrategyWorkflowCommand>();
        static string Key(IntrinsicTimeStrategyWorkflowView view) => $"{view.EntityId.Format()}|{view.WorkflowId}";
        await using var host = Host(services =>
        {
            var container = (SimpleInjector.Container)services.Single(x => x.ServiceType == typeof(SimpleInjector.Container)).ImplementationInstance!;
            // Observe the post-commit enqueue boundary. Every full-workflow event still reaches
            // the production projector, queue, Scylla writes, cache and realtime dispatcher.
            var projector = Substitute.For<TomasAI.IFM.Application.EventProjector.Contracts.IEventProjector<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor.IntrinsicTimeStrategyWorkflowCommandActor>>();
            TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.EventProjector.IntrinsicTimeStrategyWorkflowEventProjector? production = null;
            projector.StartAsync(Arg.Any<ICommandActorContext>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var context = (ICommandActorContext<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor.IntrinsicTimeStrategyWorkflowCommandActor>)call.Arg<ICommandActorContext>();
                production = new(context);
                return production.StartAsync(context, call.Arg<CancellationToken>());
            });
            projector.StopAsync(Arg.Any<CancellationToken>()).Returns(call => production?.StopAsync(call.Arg<CancellationToken>()) ?? ValueTask.CompletedTask);
            projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>()).Returns((Func<NSubstitute.Core.CallInfo, ValueTask>)(async call =>
            {
                var events = call.Arg<DomainEventCollection>();
                foreach (var state in events.OfType<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent>())
                    if (measurements.TryGetValue(Key(state.State), out var measurement) && measurement.OnCommitted(state.State))
                        return; // Only the four intentionally truncated successive-stage tests suppress dispatch.
                await production!.DomainEventsProjectionAsync(events);
            }));
            container.RegisterInstance(projector);
            services.RemoveAll<IMarketConditionAssessmentSnapshotProvider>();
            var market = Substitute.For<IMarketConditionAssessmentSnapshotProvider>();
            market.CaptureAsync(Arg.Any<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentParameterSet>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var input = AssessmentFixture.Command(horizon, call.Arg<DateTime>()) with { ParameterSet = call.Arg<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentParameterSet>() };
                var snapshot = TradeSelectionFixture.Snapshot(input);
                return ValueTask.FromResult((snapshot with { Observations = snapshot.Observations.Select(x => x with { ObservedAtUtc = snapshot.EvaluatedAtUtc }).ToArray() }).Seal());
            });
            services.AddSingleton(market);
            releaseFixtureCalls = () => { projector.ClearReceivedCalls(); market.ClearReceivedCalls(); };
            container.RegisterSingleton<ICompositionPreparationStore>(() => new WorkflowMarketFixture(
                new TomasAI.IFM.Application.Storage.MarketDataDb.CompositionPreparationStore(container.GetInstance<IDbContextFactory>().MarketDataDb),
                async key => (await container.GetInstance<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>().LoadStateAsync(starts[key.WorkflowId])).CurrentView!));
        }, broker, actualPortfolio: true).WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((context, config) =>
        {
            if (benchmark is not null) WorkflowBenchmarkSettings.ValidateEnvironment(context.HostingEnvironment, config.Build(), broker);
        }));
        _ = host.CreateClient();
        var supervisor = host.Services.GetRequiredService<IActorSupervisor>();
        var producer = host.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime, $"FullWorkflowRisk{Guid.NewGuid():N}"));
        var firstFixture = true;
        WorkflowBenchmarkIteration? currentIteration = null;
        var completedIterations = 0;
        try
        {
            await host.Services.GetRequiredService<TradeSchemaDb>().CreateAllAsync();
            await host.Services.GetRequiredService<ConfigurationSchemaDb>().CreateAllAsync();
            var iterations = benchmark?.Iterations()
                ?? [new WorkflowBenchmarkIteration(new(horizon, variant), captureTrace ? "trace" : "single", 1)];
            foreach (var iteration in iterations)
            {
                currentIteration = iteration;
                horizon = iteration.Scenario.Horizon;
                variant = iteration.Scenario.Variant;
                await RunOne(iteration);
                completedIterations++;
            }
            benchmarkWriter?.Write(new { RecordType = "run_completed", benchmarkWriter.RunId, CompletedIterations = completedIterations });
        }
        catch (Exception exception)
        {
            // Also preserves failures during schema/fixture/funding setup before a sample timer exists.
            benchmarkWriter?.Write(new { RecordType = "run_failed", benchmarkWriter.RunId, CompletedIterations = completedIterations,
                Scenario = currentIteration?.Scenario.ToString(), currentIteration?.Phase, currentIteration?.Iteration, Error = exception.ToString() });
            throw;
        }
        finally
        {
            await supervisor.ShutdownAsync();
            await producer.StopAsync();
        }

        async Task RunOne(WorkflowBenchmarkIteration iteration)
        {
            spans.Clear();
            // Warm numerical code before taking the fresh market cut.
            _ = await CompositionFixture.Command(variant, horizon);
            var policy = RiskParameterSet.Default(horizon) with { ParameterSetId = Guid.NewGuid() };
            var config = host.Services.GetRequiredService<IConfigurationDbContext>();
            await config.InsertRiskManagementDraftAsync(policy, "Full workflow test fixture", "integration-test");
            await config.PublishAsync(StrategyParameterSetKind.RiskManagement, policy.ParameterSetId, 1, DateTime.UtcNow.AddSeconds(-1));
            var assessment = AssessmentFixture.Command(horizon, DateTime.UtcNow, $"FLOW{Guid.NewGuid():N}");
            // This is an authored integration profile for deterministic lab observations;
            // production defaults and all validity intersection checks remain unchanged.
            var profile = assessment.ParameterSet with { Sources = assessment.ParameterSet.Sources.Select(x => x with { MaximumAgeSeconds = 15 }).ToArray() };
            assessment = assessment with { ParameterSet = profile, ParameterPayloadSha256 = TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentHash.Parameters(profile) };
            assessment = assessment with { WorkflowView = assessment.WorkflowView with { AssessmentBinding = new() { Parameters = profile, PayloadSha256 = assessment.ParameterPayloadSha256 } } };
            var bearish = variant.Contains("Bear", StringComparison.Ordinal) || variant == "ShortFuture";
            var balanced = variant.Contains("Balanced", StringComparison.Ordinal);
            var trigger = assessment.TriggerEvent with { FuturesItiSignal = assessment.TriggerEvent.FuturesItiSignal! with
                { IntrinsicPrice = balanced ? 100 : bearish ? 95 : 105, BandLevel = balanced ? 0 : 1,
                    IntrinsicTimeTrend = bearish ? IntrinsicTimeTrendType.DownTrend : IntrinsicTimeTrendType.UpTrend } };
            assessment = assessment with { TriggerEvent = trigger, WorkflowView = assessment.WorkflowView with { TriggerEvent = trigger } };
            var selection = await TradeSelectionFixture.Command(variant, horizon, DateTime.UtcNow,
                scopeId: Random.Shared.Next(10000000, 900000000), compositionReady: true, compositionIntegrationTiming: true,
                actualAssessmentCommand: assessment, riskPolicy: policy, compositionLifetimeMilliseconds: 30000);
            await InitializeWorkflowPortfolioAsync(host.Services, selection.SelectionBinding, initializeSchema: firstFixture);
            firstFixture = false;
            var parameters = assessment.WorkflowView.RegimeDiscoveryParameterSet!;
            var cache = host.Services.GetRequiredService<IRegimeDiscoveryMarketSignalCache>();
            var request = RegimeDiscoverySnapshotRequestFactory.Create(MarketSeriesIdentity.ForContract(trigger.EntityId.ContractId), parameters);
            long sequence = 0;
            foreach (var requirement in request.Requirements)
                cache.Upsert(new RegimeDiscoverySignalObservation { Metric = requirement.Metric,
                    SignalKey = new(request.MarketSeriesIdentity, IntrinsicTimeStrategyWorkflowRuntimeIntegrationTests.SignalKind(requirement.Metric), requirement.TimeFrame, requirement.CalculationConfigurationId),
                    Value = FinancialSignal(requirement.Metric, variant), MarketDataAsOfUtc = DateTime.UtcNow, CalculatedAtUtc = DateTime.UtcNow,
                    SourceSequence = ++sequence, SchemaVersion = 1, CalculationVersion = "1", IsWarm = true, IsValid = true,
                    Availability = RegimeDiscoverySignalAvailability.Available, SignalIdentity = $"FullWorkflowFixture/{Guid.NewGuid():N}" });
            ExecuteIntrinsicTimeStrategyWorkflowCommand start = new() { CommandId = Guid.NewGuid(), EntityId = assessment.WorkflowEntityId,
                Subject = Subject(ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb, assessment.WorkflowEntityId),
                ProposedWorkflowId = assessment.WorkflowId, TriggerEventId = trigger.Id, TriggerEvent = trigger,
                CorrelationId = assessment.CorrelationId, CausationId = trigger.Id, RequestedAtUtc = selection.SelectionBinding.FrozenAtUtc, WorkflowDefinitionVersion = 1,
                RegimeDiscoveryParameterSet = parameters, RegimeDiscoveryParameterPayloadSha256 = assessment.WorkflowView.RegimeDiscoveryParameterPayloadSha256,
                FundId = selection.SelectionBinding.PortfolioSnapshot.Fund.FundId, SelectionBinding = selection.SelectionBinding,
                AssessmentBinding = new() { Parameters = assessment.ParameterSet, PayloadSha256 = assessment.ParameterPayloadSha256 } };

            var measurement = new WorkflowMeasurement(stageCount ?? 5);
            var identity = $"{start.EntityId.Format()}|{start.ProposedWorkflowId}";
            starts[start.ProposedWorkflowId.Value] = start;
            measurements[identity] = measurement;
            using var traceRun = new System.Diagnostics.Activity("workflow.integration").SetIdFormat(System.Diagnostics.ActivityIdFormat.W3C).Start();
            if (captureTrace)
            {
                traceRun.SetTag("ifm.workflow.entity", start.EntityId.Format());
                traceRun.SetTag("ifm.workflow.id", start.ProposedWorkflowId.ToString());
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var verification = new System.Diagnostics.Stopwatch();
            Exception? failure = null;
            IntrinsicTimeStrategyWorkflowView? final = null;
            Task<IntrinsicTimeStrategyWorkflowView>? completion = null;
            try
            {
                measurement.Start();
                completion = measurement.WaitAsync(start.EntityId.Format(), timeout.Token);
                var reply = await producer.RequestAsync<ExecuteIntrinsicTimeStrategyWorkflowCommand, IntrinsicTimeStrategyWorkflowEntityId, GuidResult>(start.Subject, start, start.EntityId);
                reply.Success.Should().BeTrue(reply.ErrorMessage);
                var observed = await completion;
                verification.Start();
                var repository = host.Services.GetRequiredService<SimpleInjector.Container>().GetInstance<IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
                if (stageCount is < 5)
                {
                    var captured = observed;
                    var committed = (await repository.LoadStateAsync(start)).CurrentView!;
                    committed.WorkflowRevision.Should().Be(captured.WorkflowRevision);
                    var stages = PipelineStages(committed);
                    foreach (var stage in stages.Take(stageCount.Value))
                    {
                        stage.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
                        stage.Result.Should().NotBeNull();
                        stage.SourceEventId.Should().NotBeEmpty();
                    }
                    foreach (var stage in stages.Skip(stageCount.Value))
                    {
                        stage.Result.Should().BeNull();
                        stage.SourceEventId.Should().BeEmpty();
                        stage.CompletedAtUtc.Should().BeNull();
                    }
                    committed.RiskExecution.Should().BeNull();
                    await AssertWorkflowReservationCountAsync(selection.SelectionBinding, 0);
                    output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {
                        StageCount = stageCount, Endpoint = new[] { "Regime Discovery", "Market Assessment", "Trade Selection", "Order Composer" }[stageCount.Value - 1],
                        WorkflowMilliseconds = measurement.EndpointMilliseconds, Outcome = "Endpoint accepted",
                        Stages = stages.Take(stageCount.Value).Select(x => new { x.StartedAtUtc, x.CompletedAtUtc }) }));
                    return;
                }

                final = (await repository.LoadStateAsync(start)).CurrentView;
                final!.WorkflowRevision.Should().Be(observed.WorkflowRevision);
                final.Should().NotBeNull();
                if (final?.RiskExecution is { } measured)
                    output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Scenario = $"{horizon}/{variant}",
                        StageCount = stageCount ?? 5, Endpoint = "Risk Manager / Authorized intent", WorkflowMilliseconds = measurement.AuthorizedMilliseconds, QueryVisibleMilliseconds = measurement.QueryVisibleMilliseconds, Risk = RiskLatency.Measure(measured),
                        WorkflowId = final.WorkflowId.ToString(), StartedAtUtc = final.StartedAtUtc, TerminalAtUtc = final.TerminalAtUtc, Stages = PipelineStages(final).Select(x => new { x.StartedAtUtc, x.CompletedAtUtc }), Outcome = final.Status.ToString(), Phase = final.FinancialHandoff?.Phase.ToString() }));
                var candidateAge = final?.RiskExecution is { } invocation ? (invocation.EvaluatedAtUtc - invocation.CompositionResult.ReadCompositionResult().Candidate!.EvaluatedAtUtc).TotalMilliseconds : -1;
                (final!.FinancialHandoff?.Phase).Should().Be(RiskFinancialHandoffPhase.Authorized,
                    $"candidate age {candidateAge} ms; workflow stopped at {final.CurrentStage}/{final.Status}: {final.StopReasonCode}; {final.RegimeDiscovery.Failure}; {final.MarketCondition.Failure}; {final.TradeSelection.Failure}; {final.OrderComposition.Failure}; {final.RiskManagement.Failure}");
                final.Status.Should().Be(WorkflowStrategyMachineStatus.Completed);
                final.WorkflowId.Should().Be(start.ProposedWorkflowId);
                foreach (var stage in new[] { final.RegimeDiscovery, final.MarketCondition, final.TradeSelection, final.OrderComposition, final.RiskManagement })
                {
                    stage.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
                    stage.Result.Should().NotBeNull(); stage.SourceEventId.Should().NotBeEmpty();
                }
                var risk = final.RiskManagement.Result!.RiskResult!;
                risk.Outcome.Should().Be(RiskAssessmentOutcome.Approved); risk.StrategyUnits.Should().BePositive();
                risk.TargetHorizon.Should().Be(horizon);
                final.RiskExecution!.RegimeResult.PayloadSha256.Should().Be(final.RegimeDiscovery.Result!.PayloadSha256);
                final.RiskExecution.MarketConditionResult.PayloadSha256.Should().Be(final.MarketCondition.Result!.PayloadSha256);
                final.RiskExecution.SelectionResult.PayloadSha256.Should().Be(final.TradeSelection.Result!.PayloadSha256);
                final.RiskExecution.CompositionResult.PayloadSha256.Should().Be(final.OrderComposition.Result!.PayloadSha256);
                var order = await host.Services.GetRequiredService<IPortfolioQueryApi>().GetOrderAsync(final.FinancialHandoff!.Authorization!.OrderId);
                order.Success.Should().BeTrue(order.ErrorMessage);
                order.Value!.Status.Should().Be("RiskApproved");
                order.Value.CompositionResultHash.Should().Be(risk.CompositionResultHash);
                final.FinancialHandoff!.Authorization!.StrategyUnits.Should().Be(risk.StrategyUnits);
                await AssertWorkflowReservationCountAsync(selection.SelectionBinding, 1);
                var persistedFund = await host.Services.GetRequiredService<TomasAI.IFM.Domain.Portfolio.Persistence.IPortfolioEventStore>()
                    .LoadFundAsync(new(risk.PortfolioId, risk.FundId));
                persistedFund.Orders.Single(x => x.OrderId == order.Value.OrderId).RiskAuthorization.Should().Be(final.FinancialHandoff.Authorization);
                var receipt = await host.Services.GetRequiredService<IPortfolioFinancialApi>().GetPostingReceiptAsync(
                    new() { PortfolioId = risk.PortfolioId, FundId = risk.FundId, Access = new("workflow-test", ["LedgerRead"], [risk.PortfolioId]) },
                    new(final.FinancialHandoff.ReservationRequest!.OperationId));
                receipt.Success.Should().BeTrue(receipt.ErrorMessage);
                receipt.Value!.Value!.Reservation!.Receipt.StrategyUnits.Should().Be(risk.StrategyUnits);
                if (captureTrace)
                {
                    var recorded = spans.Where(x => (string?)x.GetTagItem("ifm.workflow.entity") == start.EntityId.Format()).ToArray();
                    recorded.Should().Contain(x => x.OperationName == "risk.calculate");
                    recorded.Should().Contain(x => x.OperationName == "workflow.state.load");
                    recorded.Should().Contain(x => x.OperationName == "risk.financial_handoff");
                    var traceIds = recorded.Select(x => x.TraceId).ToHashSet();
                    traceIds.Add(traceRun.TraceId);
                    var workflowSpans = spans.Where(x => traceIds.Contains(x.TraceId)).ToArray();
                    foreach (var operation in new[] { "composer.preparation.read", "composer.accept.create_execution",
                        "workflow.project.timeline_serialize", "workflow.project.timeline_write", "workflow.project.detail_write",
                        "workflow.project.entity_write", "workflow.project.status_write", "workflow.project.active_write",
                        "workflow.project.active_delete", "workflow.project.notify", "authorization.reserve_call",
                        "authorization.fund_authorize_call", "authorization.verify.reservation_receipt",
                        "authorization.verify.fund_receipt", "authorization.advance_send", "risk.prepare.build_request" })
                        workflowSpans.Should().Contain(x => x.OperationName == operation, "the detailed timing boundary must be exercised");
                    workflowSpans.Where(x => x.OperationName is "workflow.project.timeline_serialize" or "workflow.project.state_serialize")
                        .Should().OnlyContain(x => x.GetTagItem("ifm.payload.bytes") is int && (int)x.GetTagItem("ifm.payload.bytes")! > 0);
                    recorded.Select(x => x.TraceId).Distinct().Should().ContainSingle("the workflow must preserve W3C context across actors and projector queues");
                    recorded.Should().OnlyContain(x => x.TraceId == traceRun.TraceId,
                        "all workflow services must remain in the initiating trace");
                }
            }
            catch (Exception exception)
            {
                failure = exception;
                throw;
            }
            finally
            {
                verification.Stop();
                timeout.Cancel();
                if (completion is not null)
                {
                    try { await completion; }
                    catch (Exception) when (failure is not null) { /* Original failure is preserved below. */ }
                }
                traceRun.Stop();
                // A manually created Activity also supplies context in untraced benchmark
                // runs, but ActivityListener does not export it. Include the root explicitly.
                if (captureTrace) spans.Enqueue(traceRun);
                if (benchmarkWriter is not null)
                {
                    var sample = new { RecordType = "sample", RunId = benchmarkWriter.RunId,
                        benchmark!.Label, measurement.SampleId, Scenario = iteration.Scenario.ToString(), iteration.Phase, iteration.Iteration,
                        StageCount = stageCount ?? 5, Endpoint = "Risk Manager / Authorized intent",
                        Success = failure is null, Error = failure?.ToString(),
                        measurement.StartedAtUtc, measurement.AuthorizedMilliseconds, WorkflowMilliseconds = measurement.EndpointMilliseconds,
                        measurement.QueryVisibleMilliseconds, VerificationMilliseconds = verification.Elapsed.TotalMilliseconds,
                        ElapsedMilliseconds = measurement.ElapsedMilliseconds,
                        StageAcceptedMilliseconds = measurement.Stages, measurement.ObservedActive,
                        measurement.ProcessMetrics, WorkflowId = start.ProposedWorkflowId.ToString(),
                        WorkflowEntityId = start.EntityId.Format(), PortfolioId = selection.SelectionBinding.PortfolioSnapshot.Portfolio.PortfolioId,
                        FundId = selection.SelectionBinding.PortfolioSnapshot.Fund.FundId, TraceId = traceRun.TraceId.ToString(),
                        Outcome = final?.Status.ToString(), FinancialPhase = final?.FinancialHandoff?.Phase.ToString(),
                        Risk = final?.RiskExecution is { } invocation ? RiskLatency.Measure(invocation) : null };
                    benchmarkWriter.Write(sample);
                    output.WriteLine(System.Text.Json.JsonSerializer.Serialize(sample));
                }
                measurements.TryRemove(identity, out _);
                starts.TryRemove(start.ProposedWorkflowId.Value, out _);
                if (captureTrace)
                {
                    var traceIds = spans.Where(x => (string?)x.GetTagItem("ifm.workflow.entity") == start.EntityId.Format())
                        .Select(x => x.TraceId).Append(traceRun.TraceId).ToHashSet();
                    foreach (var span in spans.Where(x => traceIds.Contains(x.TraceId)).OrderBy(x => x.StartTimeUtc))
                    {
                        var row = new { RecordType = "span", RunId = benchmarkWriter?.RunId, measurement.SampleId,
                            Operation = span.OperationName, TraceId = span.TraceId.ToString(),
                            SpanId = span.SpanId.ToString(), ParentSpanId = span.ParentSpanId.ToString(), span.StartTimeUtc,
                            Milliseconds = span.Duration.TotalMilliseconds, Tags = span.TagObjects.ToDictionary(x => x.Key, x => x.Value) };
                        if (benchmarkWriter is null) output.WriteLine(System.Text.Json.JsonSerializer.Serialize(row));
                        else benchmarkWriter.Write(row);
                    }
                }
                releaseFixtureCalls?.Invoke();
            }
        }
    }

    sealed class WorkflowMarketFixture(ICompositionPreparationStore storage, Func<CompositionPreparationKey, Task<IntrinsicTimeStrategyWorkflowView>> load) : ICompositionPreparationStore
    {
        public Task<CompositionPreparation> CommitAsync(CompositionPreparation proposed, CancellationToken token) => storage.CommitAsync(proposed, token);
        public async Task<CompositionPreparation?> ReadAsync(CompositionPreparationKey key, CancellationToken token)
        {
            var saved = await storage.ReadAsync(key, token);
            if (saved is not null) return saved;
            var view = await load(key);
            var binding = CompositionBindingResolver.Resolve(view.TradeSelection.Result!.ReadSelectionResult(), view.SelectionBinding!, DateTime.UtcNow);
            var at = DateTimeOffset.UtcNow;
            var snapshot = CompositionSnapshotAdapter.To(CompositionFixture.Snapshot(binding, at));
            var until = new DateTimeOffset(new[] { at.AddSeconds(30).UtcDateTime, view.CompositionHandoff!.Request.ExpiresAtUtc }.Min());
            snapshot = snapshot with { ValidUntilUtc = until, Digest = "", Instruments = snapshot.Instruments.Select(x => x.Instrument.Pricing is null ? x : x with
                { Instrument = x.Instrument with { Pricing = x.Instrument.Pricing with { ValidUntilUtc = until, MaximumQuoteAgeMilliseconds = 5000 } } }).ToImmutableArray() };
            snapshot = snapshot with { Digest = PricingSemanticHash.Compute(snapshot) };
            var request = new CompositionSnapshotRequest(snapshot.SnapshotId, snapshot.ScopeId, snapshot.Horizon, snapshot.GenerationId, at, snapshot.ValidUntilUtc, binding.BuilderCode != "Future");
            var prepared = new CompositionPreparation(2, key, "GLBX.MDP3", request, snapshot, at, "");
            return await storage.CommitAsync(prepared with { Digest = PricingSemanticHash.Compute(prepared) }, token);
        }
    }

}
