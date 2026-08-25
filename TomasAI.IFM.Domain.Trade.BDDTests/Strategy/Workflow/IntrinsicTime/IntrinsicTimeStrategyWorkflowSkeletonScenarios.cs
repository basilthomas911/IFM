using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.BDDTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Runs scripted ITSW-12 workflow skeleton scenarios without registering fake production pipeline actors.</summary>
public sealed class IntrinsicTimeStrategyWorkflowSkeletonScenarios
{
    /// <summary>Completes all five stages and proves deterministic replay for each eligible ITI timeframe.</summary>
    [Theory]
    [InlineData(TimeFrameType.Daily)]
    [InlineData(TimeFrameType.Weekly)]
    [InlineData(TimeFrameType.Monthly)]
    public void Scripted_pipeline_completes_all_stages_and_replays_exactly(TimeFrameType period)
    {
        var scenario = new Scenario(period);

        scenario.Start();
        foreach (var stage in Stages)
            scenario.Complete(stage);

        scenario.State.HasActiveWorkflow.Should().BeFalse();
        scenario.State.LatestWorkflow!.Status.Should().Be(StrategyWorkflowStatus.Completed);
        scenario.State.LatestWorkflow.Outcome.Should().Be(StrategyWorkflowOutcome.Completed);
        Stages.Select(stage => StageState(scenario.State.LatestWorkflow, stage).Result?.ResultType)
            .Should().Equal(Stages.Select(stage => $"Skeleton.{stage}"));

        var replayed = new IntrinsicTimeStrategyWorkflowCommandState();
        replayed.ReplayEvents(scenario.Events);
        MessagePackSerializer.Serialize(replayed.LatestWorkflow)
            .Should().Equal(MessagePackSerializer.Serialize(scenario.State.LatestWorkflow));
        replayed.Events.Should().BeEmpty();
    }

    /// <summary>Stops deterministically when a scripted pipeline fails.</summary>
    [Fact]
    public void Scripted_pipeline_failure_stops_workflow()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.Start();
        scenario.Fail(StrategyWorkflowStage.RegimeDiscovery);

        scenario.State.LatestWorkflow!.Outcome.Should().Be(StrategyWorkflowOutcome.PipelineFailed);
        scenario.State.LatestWorkflow.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Failed);
    }

    /// <summary>Stops deterministically on timeout and retains the bounded timeout identity.</summary>
    [Fact]
    public void Scripted_pipeline_timeout_stops_workflow_and_deduplicates_timeout()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.Start();
        var timeoutId = scenario.Timeout(StrategyWorkflowStage.RegimeDiscovery);

        scenario.State.LatestWorkflow!.Outcome.Should().Be(StrategyWorkflowOutcome.TimedOut);
        scenario.State.HasProcessedTimeout(timeoutId).Should().BeTrue();
    }

    /// <summary>Confirms a persisted trigger cannot start a duplicate workflow execution.</summary>
    [Fact]
    public void Duplicate_trigger_is_rejected_by_reconstructed_state()
    {
        var scenario = new Scenario(TimeFrameType.Daily);
        scenario.Start();

        scenario.State.IsDuplicateTrigger(scenario.TriggerId).Should().BeTrue();
        scenario.State.CanAcceptStart(scenario.TriggerId).Should().BeFalse();
    }

    static readonly StrategyWorkflowStage[] Stages =
    [
        StrategyWorkflowStage.RegimeDiscovery,
        StrategyWorkflowStage.MarketCondition,
        StrategyWorkflowStage.TradeSelection,
        StrategyWorkflowStage.OrderComposition,
        StrategyWorkflowStage.RiskManagement
    ];

    static StrategyWorkflowStageState StageState(IntrinsicTimeStrategyWorkflowState state, StrategyWorkflowStage stage)
        => stage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => state.RegimeDiscovery,
            StrategyWorkflowStage.MarketCondition => state.MarketCondition,
            StrategyWorkflowStage.TradeSelection => state.TradeSelection,
            StrategyWorkflowStage.OrderComposition => state.OrderComposition,
            StrategyWorkflowStage.RiskManagement => state.RiskManagement,
            _ => throw new ArgumentOutOfRangeException(nameof(stage))
        };

    sealed class Scenario
    {
        readonly DateTime _started = new(2026, 8, 25, 18, 0, 0, DateTimeKind.Utc);
        long _eventId;

        public Scenario(TimeFrameType period)
        {
            EntityId = IntrinsicTimeStrategyWorkflowEntityId.Create(
                new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 25), period));
            WorkflowId = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000201"));
            TriggerId = Guid.Parse("0198E212-3C00-7000-8000-000000000202");
        }

        public IntrinsicTimeStrategyWorkflowCommandState State { get; } = new();
        public List<IEvent> Events { get; } = [];
        public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; }
        public StrategyWorkflowId WorkflowId { get; }
        public Guid TriggerId { get; }

        public void Start()
        {
            var trigger = new FuturesItiSignalGeneratedEvent { Id = TriggerId, EntityId = EntityId.ItiSignalEntityId };
            Apply(new StrategyWorkflowStartAcceptedEvent
            {
                EntityId = EntityId, WorkflowId = WorkflowId, WorkflowRevision = 1,
                CorrelationId = TriggerId, CausationId = TriggerId,
                Stage = StrategyWorkflowStage.RegimeDiscovery, TriggerEventId = TriggerId,
                TriggerEvent = trigger, WorkflowDefinitionVersion = 1, StartedAtUtc = _started
            });
            Apply(new IntrinsicTimeStrategyWorkflowStartedEvent
            {
                EntityId = EntityId, WorkflowId = WorkflowId, WorkflowRevision = 1,
                CorrelationId = TriggerId, CausationId = TriggerId,
                NextPipelineStage = StrategyWorkflowStage.RegimeDiscovery,
                NextPipelineActorType = ActorType.Command,
                NextPipelineActorName = "RegimeDiscoveryPipelineCommand",
                NextPipelineBoundedContext = BoundedContextName.RegimeDiscoveryPipelineBoundedContext,
                NextPipelineCommandId = Guid.NewGuid(), WorkflowState = State.ActiveWorkflow!,
                TriggerEvent = trigger, RequestedAtUtc = _started, StartedAtUtc = _started
            });
        }

        public void Complete(StrategyWorkflowStage stage)
        {
            var revision = State.ActiveWorkflow!.WorkflowRevision + 1;
            var sourceId = Guid.NewGuid();
            Apply(StageEvent(stage, "ResultRecorded", revision,
                ("SourceEventId", sourceId),
                ("Result", new StrategyStageResultEnvelope
                {
                    ResultId = Guid.NewGuid(), ResultType = $"Skeleton.{stage}", SchemaVersion = 1,
                    ContentType = "application/x-msgpack", Payload = new byte[] { 1, 2, 3 },
                    ProducedAtUtc = _started.AddMinutes(revision)
                }),
                ("RecordedAtUtc", _started.AddMinutes(revision))));
            Apply(StageEvent(stage, "ContinuationEvaluated", revision,
                ("Decision", StrategyWorkflowContinuationDecision.Proceed),
                ("RuleSetId", "Skeleton"), ("RuleSetVersion", 1),
                ("ReasonCodes", Array.Empty<string>()),
                ("EvaluatedAtUtc", _started.AddMinutes(revision))));

            if (stage == StrategyWorkflowStage.RiskManagement)
            {
                Apply(new IntrinsicTimeStrategyWorkflowCompletedEvent
                {
                    EntityId = EntityId, WorkflowId = WorkflowId, WorkflowRevision = revision,
                    CorrelationId = TriggerId, CausationId = sourceId, Stage = stage,
                    CompletedAtUtc = _started.AddMinutes(revision)
                });
                return;
            }

            var next = Stages[Array.IndexOf(Stages, stage) + 1];
            Apply(new IntrinsicTimeStrategyWorkflowContinuedEvent
            {
                EntityId = EntityId, WorkflowId = WorkflowId, WorkflowRevision = revision,
                CorrelationId = TriggerId, CausationId = sourceId,
                CompletedPipelineStage = stage, NextPipelineStage = next,
                NextPipelineActorType = ActorType.Command, NextPipelineActorName = $"{next}PipelineCommand",
                NextPipelineBoundedContext = Route(next), NextPipelineCommandId = Guid.NewGuid(),
                WorkflowState = State.ActiveWorkflow!, TriggerEvent = new FuturesItiSignalGeneratedEvent
                { Id = TriggerId, EntityId = EntityId.ItiSignalEntityId },
                ContinuationRuleSetId = "Skeleton", ContinuationRuleSetVersion = 1,
                RequestedAtUtc = _started.AddMinutes(revision), ContinuedAtUtc = _started.AddMinutes(revision)
            });
        }

        public void Fail(StrategyWorkflowStage stage)
        {
            var revision = State.ActiveWorkflow!.WorkflowRevision + 1;
            Apply(StageEvent(stage, "Failed", revision,
                ("SourceEventId", Guid.NewGuid()),
                ("Failure", new StrategyPipelineFailure { ErrorCode = 1, ErrorMessage = "scripted", FailedAtUtc = _started }),
                ("FailedAtUtc", _started)));
            Stop(revision, stage, StrategyWorkflowOutcome.PipelineFailed, "SCRIPTED_FAILURE");
        }

        public Guid Timeout(StrategyWorkflowStage stage)
        {
            var timeoutId = Guid.NewGuid();
            var revision = State.ActiveWorkflow!.WorkflowRevision + 1;
            Apply(StageEvent(stage, "TimedOut", revision,
                ("TimeoutId", timeoutId), ("TimedOutAtUtc", _started)));
            Stop(revision, stage, StrategyWorkflowOutcome.TimedOut, "SCRIPTED_TIMEOUT");
            return timeoutId;
        }

        void Stop(long revision, StrategyWorkflowStage stage, StrategyWorkflowOutcome outcome, string reason)
            => Apply(new IntrinsicTimeStrategyWorkflowStoppedEvent
            {
                EntityId = EntityId, WorkflowId = WorkflowId, WorkflowRevision = revision,
                CorrelationId = TriggerId, CausationId = Guid.NewGuid(), Stage = stage,
                Outcome = outcome, ReasonCode = reason, StoppedAtUtc = _started
            });

        IEvent StageEvent(StrategyWorkflowStage stage, string suffix, long revision,
            params (string Name, object Value)[] values)
        {
            var prefix = stage.ToString();
            var typeName = $"TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.StrategyWorkflow{prefix}{suffix}Event";
            var type = typeof(StrategyWorkflowStartAcceptedEvent).Assembly.GetType(typeName, throwOnError: true)!;
            var domainEvent = (IEvent)Activator.CreateInstance(type)!;
            Set(domainEvent, "EntityId", EntityId);
            Set(domainEvent, "WorkflowId", WorkflowId);
            Set(domainEvent, "WorkflowRevision", revision);
            Set(domainEvent, "CorrelationId", TriggerId);
            Set(domainEvent, "CausationId", Guid.NewGuid());
            Set(domainEvent, "Stage", stage);
            foreach (var value in values)
                Set(domainEvent, value.Name, value.Value);
            return domainEvent;
        }

        void Apply(IEvent domainEvent)
        {
            Set(domainEvent, nameof(IEvent.Id), domainEvent.Id == Guid.Empty ? Guid.NewGuid() : domainEvent.Id);
            Set(domainEvent, nameof(IEvent.EventId), ++_eventId);
            State.Apply(domainEvent, addEvent: false).Should().BeTrue(domainEvent.GetType().Name);
            Events.Add(domainEvent);
        }

        static void Set(object target, string property, object value)
            => EventInitHelper.SetProperty(target, property, value);

        static BoundedContextName Route(StrategyWorkflowStage stage) => stage switch
        {
            StrategyWorkflowStage.MarketCondition => BoundedContextName.MarketConditionPipelineBoundedContext,
            StrategyWorkflowStage.TradeSelection => BoundedContextName.TradeSelectionPipelineBoundedContext,
            StrategyWorkflowStage.OrderComposition => BoundedContextName.OrderCompositionPipelineBoundedContext,
            StrategyWorkflowStage.RiskManagement => BoundedContextName.RiskManagementPipelineBoundedContext,
            _ => BoundedContextName.RegimeDiscoveryPipelineBoundedContext
        };
    }
}
