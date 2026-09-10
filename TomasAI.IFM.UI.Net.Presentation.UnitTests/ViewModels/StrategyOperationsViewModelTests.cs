using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using Xunit.Abstractions;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class StrategyOperationsViewModelTests
{
    const string ContractId = "ESZ26";
    static readonly DateOnly ValueDate = new(2026, 8, 21);
    readonly ITestOutputHelper _output;

    public StrategyOperationsViewModelTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Initialize_SubscribesBeforeHistoryAndPublishesCompleteSelectedTimeFrame()
    {
        var daily = Signal(TimeFrameType.Daily, 1, IntrinsicTimeModeType.Trending);
        var dailyDirection = Signal(TimeFrameType.Daily, 4, IntrinsicTimeModeType.TrendDirectionChanged);
        var weekly = Signal(TimeFrameType.Weekly, 2, IntrinsicTimeModeType.TrendDirectionChanged);
        var monthly = Signal(TimeFrameType.Monthly, 3, IntrinsicTimeModeType.TrendExtremeChanged);
        var subject = CreateSubject();
        subject.QueryApi.GetFuturesItiSignalHistoryAsync(ContractId, ValueDate, TimeFrameType.Daily)
            .Returns(_ =>
            {
                subject.EventSource.Publish(daily);
                return Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                    new ServiceOk<FuturesItiSignalV2ReadModel[]>([daily, dailyDirection]));
            });
        subject.QueryApi.GetFuturesItiSignalHistoryAsync(ContractId, ValueDate, TimeFrameType.Weekly)
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                new ServiceOk<FuturesItiSignalV2ReadModel[]>([weekly])));
        subject.QueryApi.GetFuturesItiSignalHistoryAsync(ContractId, ValueDate, TimeFrameType.Monthly)
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                new ServiceOk<FuturesItiSignalV2ReadModel[]>([monthly])));

        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        subject.ViewModel.IsListening.Should().BeTrue();
        subject.ViewModel.TimeFrames.Should().Equal(
            TimeFrameType.Daily,
            TimeFrameType.Weekly,
            TimeFrameType.Monthly);
        subject.ViewModel.SelectedTimeFrame.Should().Be(TimeFrameType.Daily);
        subject.ViewModel.StatusText.Should().StartWith("Intrinsic Time Daily:");
        subject.ViewModel.Events.Should().HaveCount(2)
            .And.OnlyContain(row => row.TimePeriod == TimeFrameType.Daily);
        subject.ViewModel.Events.Single(row => row.SequenceId == daily.SequenceId)
            .IsHistorical.Should().BeFalse("the live overlap arrived after subscription and won deduplication");
        subject.ViewModel.Events.Single(row => row.SequenceId == dailyDirection.SequenceId)
            .IsHistorical.Should().BeTrue();

        subject.ViewModel.SelectedTimeFrame = TimeFrameType.Weekly;
        subject.ViewModel.StatusText.Should().StartWith("Intrinsic Time Weekly:");
        subject.ViewModel.Events.Should().ContainSingle()
            .Which.TimePeriod.Should().Be(TimeFrameType.Weekly);
        subject.ViewModel.SelectedTimeFrame = TimeFrameType.Monthly;
        subject.ViewModel.Events.Should().ContainSingle()
            .Which.TimePeriod.Should().Be(TimeFrameType.Monthly);
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Listener_PublishesEveryItiModeAndStopsCleanly()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        var modes = Enum.GetValues<IntrinsicTimeModeType>();
        for (var index = 0; index < modes.Length; index++)
        {
            subject.EventSource.Publish(Signal(
                (TimeFrameType)((index % 3) + (int)TimeFrameType.Daily),
                0,
                modes[index]) with
            {
                IntrinsicTime = new DateTime(2026, 8, 21, 13, 30, 0, DateTimeKind.Utc)
                    .AddSeconds(index),
                IntrinsicPrice = 6500 + index
            });
        }

        foreach (var timeFrame in subject.ViewModel.TimeFrames)
        {
            subject.ViewModel.SelectedTimeFrame = timeFrame;
            var expectedModes = modes
                .Where((_, index) =>
                    (TimeFrameType)((index % 3) + (int)TimeFrameType.Daily) == timeFrame);
            subject.ViewModel.Events.Select(row => row.Mode)
                .Should().BeEquivalentTo(expectedModes);
            subject.ViewModel.Events.Should().OnlyContain(row => row.TimePeriod == timeFrame);
            subject.ViewModel.Events.Should().OnlyContain(row => row.SequenceId == 0);
        }

        subject.ViewModel.SelectedTimeFrame = TimeFrameType.Daily;
        var retainedDailyCount = subject.ViewModel.Events.Count;
        await subject.ViewModel.StopAsync(CancellationToken.None);
        subject.EventSource.Publish(Signal(
            TimeFrameType.Daily,
            100,
            IntrinsicTimeModeType.TrendDirectionChanged));

        subject.ViewModel.Events.Should().HaveCount(retainedDailyCount);
        subject.ViewModel.IsListening.Should().BeFalse();
        subject.EventSource.IsStarted.Should().BeFalse();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Listener_FiltersContextDeduplicatesAndRetainsCompleteHistory()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);

        subject.EventSource.Publish(Signal(TimeFrameType.OneMinute, 1, IntrinsicTimeModeType.Trending));
        subject.EventSource.Publish(Signal(TimeFrameType.Daily, 2, IntrinsicTimeModeType.Trending) with
        {
            ContractId = "NQZ26"
        });

        var duplicate = Signal(TimeFrameType.Daily, 3, IntrinsicTimeModeType.Trending);
        subject.EventSource.Publish(duplicate);
        subject.EventSource.Publish(duplicate);
        for (var sequence = 4; sequence <= 520; sequence++)
        {
            subject.EventSource.Publish(Signal(
                TimeFrameType.Daily,
                sequence,
                IntrinsicTimeModeType.PredictedIntervalChanged));
        }

        subject.ViewModel.Events.Should().HaveCount(518);
        subject.ViewModel.Events.Should().OnlyContain(row =>
            row.ContractId == ContractId
            && row.ValueDate == ValueDate
            && row.TimePeriod == TimeFrameType.Daily);
        subject.ViewModel.Events.Select(row => row.SequenceId)
            .Should().BeInDescendingOrder();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Reconciliation_RecoversEveryPointAfterNotificationGap()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 21, 14, 0, 0, TimeSpan.Zero));
        var interval = TimeSpan.FromMinutes(1);
        var missed = Signal(
            TimeFrameType.Daily,
            10,
            IntrinsicTimeModeType.TrendExtremeChanged);
        var authoritativeHead = Signal(
            TimeFrameType.Daily,
            11,
            IntrinsicTimeModeType.TrendDirectionChanged);
        var dailyHistoryCalls = 0;
        var subject = CreateSubject(timeProvider, interval);
        subject.QueryApi.GetFuturesItiSignalHistoryAsync(
                ContractId,
                ValueDate,
                TimeFrameType.Daily)
            .Returns(_ => Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                new ServiceOk<FuturesItiSignalV2ReadModel[]>(
                    Interlocked.Increment(ref dailyHistoryCalls) == 1
                        ? []
                        : [missed, authoritativeHead])));
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        subject.ViewModel.Events.Should().BeEmpty();

        timeProvider.Advance(interval);
        await WaitUntilAsync(() => subject.ViewModel.Events.Count == 2);

        subject.ViewModel.Events.Select(row => row.SequenceId).Should().Equal(11, 10);
        subject.ViewModel.Events.Should().OnlyContain(row => row.IsHistorical);
        dailyHistoryCalls.Should().Be(2, "the recovery cadence reloads authoritative history");
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Reconciliation_DeduplicatesPersistedSignalAfterZeroSequenceLiveNotification()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 21, 14, 0, 0, TimeSpan.Zero));
        var interval = TimeSpan.FromMinutes(1);
        var persisted = Signal(
            TimeFrameType.Weekly,
            12,
            IntrinsicTimeModeType.TrendReversalChanged) with
        {
            TimeFrameStartValueDate = default
        };
        var live = persisted with
        {
            SequenceId = 0,
            TimeFrameStartValueDate = ValueDate.AddDays(-4)
        };
        var weeklyHistoryCalls = 0;
        var subject = CreateSubject(timeProvider, interval);
        subject.QueryApi.GetFuturesItiSignalHistoryAsync(
                ContractId,
                ValueDate,
                TimeFrameType.Weekly)
            .Returns(_ =>
            {
                Interlocked.Increment(ref weeklyHistoryCalls);
                return Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                    new ServiceOk<FuturesItiSignalV2ReadModel[]>(
                        weeklyHistoryCalls == 1 ? [] : [persisted]));
            });

        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        subject.ViewModel.SelectedTimeFrame = TimeFrameType.Weekly;
        subject.EventSource.Publish(live);
        subject.ViewModel.Events.Should().ContainSingle()
            .Which.IsHistorical.Should().BeFalse();

        timeProvider.Advance(interval);
        await WaitUntilAsync(() => Volatile.Read(ref weeklyHistoryCalls) == 2);

        subject.ViewModel.Events.Should().ContainSingle(
            "persistence-only metadata does not create a duplicate display event");
        subject.ViewModel.Events.Single().IsHistorical.Should().BeFalse(
            "the live notification arrived first and remains the displayed instance");
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Operations_DefaultsToStrategyAndAllowsEveryTab()
    {
        var subject = CreateSubject();
        var operations = new OperationsViewModel(subject.ViewModel);

        operations.SelectedView.Should().Be(OperationsViewType.Strategy);
        foreach (var view in Enum.GetValues<OperationsViewType>())
        {
            operations.SelectView(view);
            operations.SelectedView.Should().Be(view);
        }

        await operations.DisposeAsync();
    }

    [Fact]
    public async Task WorkflowNotifications_RevealOnlyStartedActorsAndApplyApprovedColors()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        var workflow = Workflow(1) with
        {
            RegimeDiscovery = Stage(StrategyActorProcessingStatus.Processing)
        };

        subject.WorkflowEventSource.Publish(workflow);

        subject.ViewModel.Workflows.Should().ContainSingle();
        subject.ViewModel.Workflows[0].PipelineActors.Should().ContainSingle()
            .Which.DisplayState.Should().Be(PipelineActorDisplayState.Processing);

        subject.WorkflowEventSource.Publish(workflow with
        {
            WorkflowRevision = 2,
            RegimeDiscovery = Stage(
                StrategyActorProcessingStatus.Completed,
                StrategyWorkflowContinuationDecision.Proceed),
            MarketCondition = Stage(StrategyActorProcessingStatus.Processing),
            CurrentStage = StrategyWorkflowStage.MarketCondition
        });

        subject.ViewModel.Workflows[0].PipelineActors.Select(actor => actor.DisplayState)
            .Should().Equal(PipelineActorDisplayState.Continued, PipelineActorDisplayState.Processing);
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task WorkflowNotifications_StopAtAnyActorAndNeverRegressRevision()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        var failed = Workflow(3) with
        {
            CurrentStage = StrategyWorkflowStage.TradeSelection,
            Status = WorkflowStrategyMachineStatus.Failed,
            Outcome = StrategyWorkflowOutcome.PipelineFailed,
            RegimeDiscovery = Stage(StrategyActorProcessingStatus.Completed, StrategyWorkflowContinuationDecision.Proceed),
            MarketCondition = Stage(StrategyActorProcessingStatus.Completed, StrategyWorkflowContinuationDecision.Proceed),
            TradeSelection = Stage(StrategyActorProcessingStatus.Failed)
        };

        subject.WorkflowEventSource.Publish(failed);
        subject.WorkflowEventSource.Publish(failed with
        {
            WorkflowRevision = 2,
            Status = WorkflowStrategyMachineStatus.Started,
            Outcome = StrategyWorkflowOutcome.None
        });

        var row = subject.ViewModel.Workflows.Single();
        row.WorkflowRevision.Should().Be(3);
        row.PipelineActors.Select(actor => actor.DisplayState).Should().Equal(
            PipelineActorDisplayState.Continued,
            PipelineActorDisplayState.Continued,
            PipelineActorDisplayState.Stopped);
        row.PipelineActors.Should().HaveCount(3, "actors after the stopping stage remain hidden");
        row.EndState.Should().Be("Pipeline Failed");
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task SelectedWorkflow_DetailsRetainEveryStageHeadingAndExplicitMissingResults()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        var workflow = Workflow(1) with
        {
            RegimeDiscovery = Stage(StrategyActorProcessingStatus.Processing)
        };
        subject.WorkflowEventSource.Publish(workflow);

        subject.ViewModel.SelectWorkflow(workflow.WorkflowId);

        subject.ViewModel.SelectedWorkflowDetails.Should().Contain("=== REGIME DISCOVERY RESULT ===")
            .And.Contain("=== MARKET CONDITION RESULT ===")
            .And.Contain("=== TRADE SELECTION RESULT ===")
            .And.Contain("=== ORDER COMPOSITION RESULT ===")
            .And.Contain("=== RISK MANAGEMENT RESULT ===")
            .And.Contain("None — workflow did not reach this pipeline result yet.");
        await subject.ViewModel.DisposeAsync();
    }

    [Theory]
    [InlineData(StrategyWorkflowStage.RegimeDiscovery, 1)]
    [InlineData(StrategyWorkflowStage.MarketCondition, 2)]
    [InlineData(StrategyWorkflowStage.TradeSelection, 3)]
    [InlineData(StrategyWorkflowStage.OrderComposition, 4)]
    [InlineData(StrategyWorkflowStage.RiskManagement, 5)]
    public async Task WorkflowStop_UsesTheStoppingActorAsFinalRedCircle(
        StrategyWorkflowStage stage,
        int visibleActors)
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        subject.WorkflowEventSource.Publish(AtStage(
            Workflow(1),
            stage,
            StrategyActorProcessingStatus.Completed,
            StrategyWorkflowContinuationDecision.Stop) with
        {
            Status = WorkflowStrategyMachineStatus.Completed,
            Outcome = StrategyWorkflowOutcome.NoTrade
        });

        var row = subject.ViewModel.Workflows.Single();
        row.PipelineActors.Should().HaveCount(visibleActors);
        row.PipelineActors.Take(visibleActors - 1).Should()
            .OnlyContain(actor => actor.DisplayState == PipelineActorDisplayState.Continued);
        row.PipelineActors[^1].DisplayState.Should().Be(PipelineActorDisplayState.Stopped);
        row.EndState.Should().Be("No Trade");
        await subject.ViewModel.DisposeAsync();
    }

    [Theory]
    [InlineData(StrategyActorProcessingStatus.Failed, StrategyWorkflowOutcome.PipelineFailed, "Pipeline Failed")]
    [InlineData(StrategyActorProcessingStatus.TimedOut, StrategyWorkflowOutcome.TimedOut, "Timed Out")]
    [InlineData(StrategyActorProcessingStatus.Cancelled, StrategyWorkflowOutcome.Cancelled, "Cancelled")]
    public async Task WorkflowTerminalFailures_UseRedForTheActiveActor(
        StrategyActorProcessingStatus processingStatus,
        StrategyWorkflowOutcome outcome,
        string endState)
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        var machineStatus = processingStatus switch
        {
            StrategyActorProcessingStatus.TimedOut => WorkflowStrategyMachineStatus.TimedOut,
            StrategyActorProcessingStatus.Cancelled => WorkflowStrategyMachineStatus.Cancelled,
            _ => WorkflowStrategyMachineStatus.Failed
        };
        subject.WorkflowEventSource.Publish(AtStage(
            Workflow(1),
            StrategyWorkflowStage.OrderComposition,
            processingStatus) with { Status = machineStatus, Outcome = outcome });

        var row = subject.ViewModel.Workflows.Single();
        row.PipelineActors.Should().HaveCount(4);
        row.PipelineActors[^1].DisplayState.Should().Be(PipelineActorDisplayState.Stopped);
        row.EndState.Should().Be(endState);
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task ApprovedAndRiskRejectedWorkflows_RenderTheFiveActorTerminalSemantics()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        var approved = AtStage(
            Workflow(1),
            StrategyWorkflowStage.RiskManagement,
            StrategyActorProcessingStatus.Completed,
            StrategyWorkflowContinuationDecision.Proceed) with
        {
            Status = WorkflowStrategyMachineStatus.Completed,
            Outcome = StrategyWorkflowOutcome.Completed
        };
        var rejected = AtStage(
            Workflow(1),
            StrategyWorkflowStage.RiskManagement,
            StrategyActorProcessingStatus.Completed,
            StrategyWorkflowContinuationDecision.Stop) with
        {
            Status = WorkflowStrategyMachineStatus.Completed,
            Outcome = StrategyWorkflowOutcome.NoTrade
        };
        subject.WorkflowEventSource.Publish(approved);
        subject.WorkflowEventSource.Publish(rejected);

        var approvedRow = subject.ViewModel.Workflows.Single(row => row.WorkflowId == approved.WorkflowId);
        approvedRow.PipelineActors.Should().HaveCount(5)
            .And.OnlyContain(actor => actor.DisplayState == PipelineActorDisplayState.Continued);
        approvedRow.EndState.Should().Be("Approved");
        var rejectedRow = subject.ViewModel.Workflows.Single(row => row.WorkflowId == rejected.WorkflowId);
        rejectedRow.PipelineActors.Take(4).Should()
            .OnlyContain(actor => actor.DisplayState == PipelineActorDisplayState.Continued);
        rejectedRow.PipelineActors[^1].DisplayState.Should().Be(PipelineActorDisplayState.Stopped);
        rejectedRow.EndState.Should().Be("No Trade");
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task SameRevisionConflict_IsDiagnosedWithoutReplacingTheRetainedView()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        var original = Workflow(5) with
        {
            RegimeDiscovery = Stage(StrategyActorProcessingStatus.Processing)
        };
        subject.WorkflowEventSource.Publish(original);
        subject.WorkflowEventSource.Publish(original with { StopReasonCode = "CONFLICTING-COPY" });

        subject.ViewModel.Workflows.Single().WorkflowRevision.Should().Be(5);
        subject.ViewModel.Workflows.Single().PipelineActors.Single().DisplayState
            .Should().Be(PipelineActorDisplayState.Processing);
        subject.ViewModel.LastError.Should().NotBeNull();
        subject.ViewModel.LastError!.ErrorCode.Should().Be(409);
        subject.ViewModel.LastError.Caption.Should().Be("Strategy Workflow Revision Conflict");
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task Reconciliation_RecoversMissedTerminalWorkflowAndReusesBoundedTerminalCache()
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 21, 14, 0, 0, TimeSpan.Zero));
        var interval = TimeSpan.FromMinutes(1);
        var terminal = AtStage(
            Workflow(2),
            StrategyWorkflowStage.RiskManagement,
            StrategyActorProcessingStatus.Completed,
            StrategyWorkflowContinuationDecision.Proceed) with
        {
            Status = WorkflowStrategyMachineStatus.Completed,
            Outcome = StrategyWorkflowOutcome.Completed
        };
        var subject = CreateSubject(timeProvider, interval);
        var historyCalls = 0;
        subject.WorkflowQueryApi.GetRecentAsync(
                terminal.EntityId.Format(),
                Arg.Any<DateTime>(),
                Arg.Any<int>())
            .Returns(_ => Task.FromResult<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>>(
                new ServiceOk<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>(
                    Interlocked.Increment(ref historyCalls) == 1
                        ? []
                        : [History(terminal)])));
        subject.WorkflowQueryApi.GetByIdAsync(terminal.WorkflowId, terminal.WorkflowRevision)
            .Returns(new ServiceOk<IntrinsicTimeStrategyWorkflowReadModel>(Detail(terminal)));

        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        subject.ViewModel.Workflows.Should().BeEmpty();
        timeProvider.Advance(interval);
        await WaitUntilAsync(() => subject.ViewModel.Workflows.Count == 1);
        subject.ViewModel.Workflows.Single().EndState.Should().Be("Approved");

        timeProvider.Advance(interval);
        await WaitUntilAsync(() => Volatile.Read(ref historyCalls) >= 3);
        await subject.WorkflowQueryApi.Received(1).GetByIdAsync(terminal.WorkflowId, terminal.WorkflowRevision);
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    public async Task WorkflowRows_AreBoundedFilteredByTimeframeAndRejectUpdatesAfterStop()
    {
        var subject = CreateSubject();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        for (var index = 0; index < 501; index++)
            subject.WorkflowEventSource.Publish(Workflow(index + 1) with
            {
                WorkflowId = new StrategyWorkflowId(Guid.NewGuid()),
                StartedAtUtc = new DateTime(2026, 8, 21, 13, 0, 0, DateTimeKind.Utc).AddSeconds(index)
            });
        subject.ViewModel.Workflows.Should().HaveCount(500);

        var weekly = Workflow(1) with
        {
            WorkflowId = new StrategyWorkflowId(Guid.NewGuid()),
            EntityId = IntrinsicTimeStrategyWorkflowEntityId.Create(
                new FuturesItiSignalEntityId(ContractId, ValueDate, TimeFrameType.Weekly))
        };
        subject.WorkflowEventSource.Publish(weekly);
        subject.ViewModel.SelectedTimeFrame = TimeFrameType.Weekly;
        subject.ViewModel.Workflows.Should().ContainSingle()
            .Which.WorkflowId.Should().Be(weekly.WorkflowId);

        await subject.ViewModel.StopAsync(CancellationToken.None);
        subject.WorkflowEventSource.Publish(weekly with
        {
            WorkflowId = new StrategyWorkflowId(Guid.NewGuid()),
            WorkflowRevision = 2
        });
        subject.ViewModel.Workflows.Should().ContainSingle();
        await subject.ViewModel.DisposeAsync();
    }

    [Fact]
    [Trait("Category", "PerformanceObservation")]
    public async Task WorkflowUiLatency_IsMeasuredWithoutQualificationLimit()
    {
        var terminal = AtStage(
            Workflow(7),
            StrategyWorkflowStage.RiskManagement,
            StrategyActorProcessingStatus.Completed,
            StrategyWorkflowContinuationDecision.Proceed) with
        {
            Status = WorkflowStrategyMachineStatus.Completed,
            Outcome = StrategyWorkflowOutcome.Completed
        };
        var subject = CreateSubject();
        subject.WorkflowQueryApi.GetRecentAsync(
                terminal.EntityId.Format(),
                Arg.Any<DateTime>(),
                Arg.Any<int>())
            .Returns(new ServiceOk<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>([History(terminal)]));
        subject.WorkflowQueryApi.GetByIdAsync(terminal.WorkflowId, terminal.WorkflowRevision)
            .Returns(new ServiceOk<IntrinsicTimeStrategyWorkflowReadModel>(Detail(terminal)));

        var timer = System.Diagnostics.Stopwatch.StartNew();
        await subject.ViewModel.InitializeAsync(CancellationToken.None);
        timer.Stop();
        var hydrationMilliseconds = timer.Elapsed.TotalMilliseconds;
        subject.ViewModel.SelectWorkflow(terminal.WorkflowId);

        var updated = terminal with
        {
            WorkflowRevision = terminal.WorkflowRevision + 1,
            UpdatedAtUtc = terminal.UpdatedAtUtc.AddMilliseconds(1)
        };
        timer.Restart();
        subject.WorkflowEventSource.Publish(updated);
        timer.Stop();
        var liveUpdateMilliseconds = timer.Elapsed.TotalMilliseconds;

        subject.ViewModel.Workflows.Single().WorkflowRevision.Should().Be(updated.WorkflowRevision);
        subject.ViewModel.SelectedWorkflowDetails.Should().Contain($"Revision: {updated.WorkflowRevision}");
        _output.WriteLine(
            "SWUI observational timing: one persisted workflow hydrate/startup={0:F3} ms; selected live revision reduce+details={1:F3} ms. No qualification limit applied.",
            hydrationMilliseconds,
            liveUpdateMilliseconds);
        await subject.ViewModel.DisposeAsync();
    }

    static Subject CreateSubject(
        TimeProvider? timeProvider = null,
        TimeSpan? reconciliationInterval = null)
    {
        var queryApi = Substitute.For<IMarketDataAnalyticsQueryApi>();
        queryApi.GetFuturesItiSignalAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<TimeFrameType>())
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel>>(
                new ServiceOk<FuturesItiSignalV2ReadModel>(new())));
        queryApi.GetFuturesItiSignalHistoryAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<TimeFrameType>())
            .Returns(Task.FromResult<ServiceResult<FuturesItiSignalV2ReadModel[]>>(
                new ServiceOk<FuturesItiSignalV2ReadModel[]>([])));

        var consumer = Substitute.For<IFuturesItiSignalUIEventConsumer>();
        var eventSource = new TestEventSource(consumer);
        var workflowApi = Substitute.For<IIntrinsicTimeStrategyWorkflowQueryApi>();
        workflowApi.GetRecentAsync(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(Task.FromResult<ServiceResult<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>>(
                new ServiceOk<IntrinsicTimeStrategyWorkflowHistoryReadModel[]>([])));
        var workflowConsumer = Substitute.For<IIntrinsicTimeStrategyWorkflowUIEventConsumer>();
        var workflowEventSource = new TestWorkflowEventSource(workflowConsumer);
        var model = new StrategyOperationsService(queryApi, consumer, workflowApi, workflowConsumer);
        return new Subject(
            new StrategyOperationsViewModel(
                model,
                ContractId,
                ValueDate,
                timeProvider,
                reconciliationInterval),
            queryApi,
            eventSource,
            workflowApi,
            workflowEventSource);
    }

    static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeout)
                throw new TimeoutException("The expected reconciled presentation state was not published.");
            await Task.Delay(10);
        }
    }

    static FuturesItiSignalV2ReadModel Signal(
        TimeFrameType period,
        long sequence,
        IntrinsicTimeModeType mode)
        => new()
        {
            ContractId = ContractId,
            ValueDate = ValueDate,
            TimePeriod = period,
            SequenceId = sequence,
            IntrinsicTime = new DateTime(2026, 8, 21, 13, 30, 0, DateTimeKind.Utc)
                .AddSeconds(sequence),
            IntrinsicTimeGroupId = 1,
            IntrinsicTimeLength = sequence,
            IntrinsicPrice = 6500 + sequence,
            IntrinsicTimeTrend = sequence % 2 == 0
                ? IntrinsicTimeTrendType.DownTrend
                : IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeMode = mode,
            TrendPrice = 6500,
            TrendExtreme = 6520,
            TrendReversal = 6480,
            TrendDelta = 20,
            TargetDelta = 30,
            TradingDays = 20,
            Threshold = 5,
            UpTrendTrigger = 6505,
            DownTrendTrigger = 6495,
            TimeFrameStartValueDate = ValueDate
        };

    sealed record Subject(
        StrategyOperationsViewModel ViewModel,
        IMarketDataAnalyticsQueryApi QueryApi,
        TestEventSource EventSource,
        IIntrinsicTimeStrategyWorkflowQueryApi WorkflowQueryApi,
        TestWorkflowEventSource WorkflowEventSource);

    sealed class TestEventSource
    {
        Action<FuturesItiSignalUpdatedNotifyEvent>? _eventAction;

        public TestEventSource(IFuturesItiSignalUIEventConsumer consumer)
        {
            consumer.StartAsync(
                    Arg.Any<Guid>(),
                    Arg.Any<Action<FuturesItiSignalUpdatedNotifyEvent>>())
                .Returns(call =>
                {
                    _eventAction = call.ArgAt<Action<FuturesItiSignalUpdatedNotifyEvent>>(1);
                    IsStarted = true;
                    return ValueTask.CompletedTask;
                });
            consumer.StopAsync(Arg.Any<Guid>()).Returns(_ =>
            {
                IsStarted = false;
                return ValueTask.CompletedTask;
            });
        }

        public bool IsStarted { get; private set; }

        public void Publish(FuturesItiSignalV2ReadModel signal)
            => (_eventAction ?? throw new InvalidOperationException("Listener not started."))(
                new FuturesItiSignalUpdatedNotifyEvent
                {
                    Subject = new ActorSubject(
                        ActorType.Notify,
                        FuturesItiSignalUpdatedNotifyEvent.Actor,
                        FuturesItiSignalUpdatedNotifyEvent.Verb,
                        signal.EntityId.Format()),
                    Id = Guid.NewGuid(),
                    SourceEventId = Guid.NewGuid(),
                    EntityId = signal.EntityId,
                    CommandId = Guid.NewGuid(),
                    ReceivedOn = signal.IntrinsicTime,
                    FuturesItiSignal = signal
                });
    }

    sealed class TestWorkflowEventSource
    {
        Action<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent>? _eventAction;

        public TestWorkflowEventSource(IIntrinsicTimeStrategyWorkflowUIEventConsumer consumer)
        {
            consumer.StartAsync(
                    Arg.Any<Guid>(),
                    Arg.Any<Action<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent>>())
                .Returns(call =>
                {
                    _eventAction = call.ArgAt<Action<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent>>(1);
                    return ValueTask.CompletedTask;
                });
            consumer.StopAsync(Arg.Any<Guid>()).Returns(ValueTask.CompletedTask);
        }

        public void Publish(IntrinsicTimeStrategyWorkflowView view)
            => (_eventAction ?? throw new InvalidOperationException("Workflow listener not started."))(
                new IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent
                {
                    Subject = new ActorSubject(
                        ActorType.Notify,
                        IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Actor,
                        IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent.Verb,
                        view.EntityId.Format()),
                    Id = Guid.NewGuid(),
                    SourceEventId = Guid.NewGuid(),
                    EntityId = view.EntityId,
                    WorkflowId = view.WorkflowId,
                    WorkflowRevision = view.WorkflowRevision,
                    State = view,
                    UpdatedAtUtc = view.UpdatedAtUtc
                });
    }

    static IntrinsicTimeStrategyWorkflowView Workflow(long revision)
    {
        var entity = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId(ContractId, ValueDate, TimeFrameType.Daily));
        var signal = Signal(TimeFrameType.Daily, 1, IntrinsicTimeModeType.TrendDirectionChanged);
        var now = new DateTime(2026, 8, 21, 13, 30, 1, DateTimeKind.Utc);
        var trigger = new FuturesItiSignalGeneratedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor,
                FuturesItiSignalGeneratedEvent.Verb, signal.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = signal.EntityId,
            CommandId = Guid.NewGuid(),
            CreatedOn = now,
            ReceivedOn = now,
            FuturesItiSignal = signal
        };
        return new IntrinsicTimeStrategyWorkflowView
        {
            EntityId = entity,
            WorkflowId = new StrategyWorkflowId(Guid.NewGuid()),
            TriggerEventId = trigger.Id,
            TriggerEvent = trigger,
            WorkflowDefinitionVersion = 1,
            Status = WorkflowStrategyMachineStatus.Started,
            CurrentStage = StrategyWorkflowStage.RegimeDiscovery,
            WorkflowRevision = revision,
            StartedAtUtc = now,
            UpdatedAtUtc = now.AddMilliseconds(revision),
            ExpiresAtUtc = now.AddMinutes(1)
        };
    }

    static StrategyWorkflowStageState Stage(
        StrategyActorProcessingStatus status,
        StrategyWorkflowContinuationDecision continuation = StrategyWorkflowContinuationDecision.None)
        => new()
        {
            ProcessingStatus = status,
            ContinuationDecision = continuation,
            StartedAtUtc = status == StrategyActorProcessingStatus.NotStarted
                ? null
                : new DateTime(2026, 8, 21, 13, 30, 1, DateTimeKind.Utc)
        };

    static IntrinsicTimeStrategyWorkflowView AtStage(
        IntrinsicTimeStrategyWorkflowView workflow,
        StrategyWorkflowStage currentStage,
        StrategyActorProcessingStatus currentStatus,
        StrategyWorkflowContinuationDecision currentContinuation = StrategyWorkflowContinuationDecision.None)
    {
        var completed = Stage(
            StrategyActorProcessingStatus.Completed,
            StrategyWorkflowContinuationDecision.Proceed);
        StrategyWorkflowStageState StateFor(StrategyWorkflowStage stage)
            => (int)stage < (int)currentStage
                ? completed
                : stage == currentStage
                    ? Stage(currentStatus, currentContinuation)
                    : new StrategyWorkflowStageState();
        return workflow with
        {
            CurrentStage = currentStage,
            RegimeDiscovery = StateFor(StrategyWorkflowStage.RegimeDiscovery),
            MarketCondition = StateFor(StrategyWorkflowStage.MarketCondition),
            TradeSelection = StateFor(StrategyWorkflowStage.TradeSelection),
            OrderComposition = StateFor(StrategyWorkflowStage.OrderComposition),
            RiskManagement = StateFor(StrategyWorkflowStage.RiskManagement)
        };
    }

    static IntrinsicTimeStrategyWorkflowHistoryReadModel History(IntrinsicTimeStrategyWorkflowView workflow)
        => new(
            workflow.EntityId.Format(),
            workflow.StartedAtUtc,
            workflow.WorkflowId,
            StrategyWorkflowStatus.Completed,
            workflow.Outcome,
            workflow.CurrentStage,
            workflow.WorkflowRevision,
            workflow.TerminalAtUtc,
            workflow.StopReasonCode);

    static IntrinsicTimeStrategyWorkflowReadModel Detail(IntrinsicTimeStrategyWorkflowView workflow)
        => new(
            workflow.WorkflowId,
            workflow.EntityId.Format(),
            workflow.EntityId.WorkflowDefinitionId,
            workflow.WorkflowDefinitionVersion,
            workflow.EntityId.ItiSignalEntityId.ContractId,
            workflow.EntityId.ItiSignalEntityId.TimeFrameStartValueDate,
            workflow.EntityId.ItiSignalEntityId.TimePeriod,
            workflow.TriggerEventId,
            workflow.CorrelationId,
            StrategyWorkflowStatus.Completed,
            workflow.Outcome,
            workflow.CurrentStage,
            workflow.WorkflowRevision,
            1,
            2,
            MessagePackSerializer.Serialize(workflow),
            workflow.StopReasonCode,
            workflow.StartedAtUtc,
            workflow.TerminalAtUtc,
            workflow.UpdatedAtUtc);
}
