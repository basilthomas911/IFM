using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Routing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Verifies the strategy workflow-to-pipeline boundary contracts introduced by ITSW-4.</summary>
public sealed class IntrinsicTimeStrategyPipelineBoundaryContractTests
{
    const string CommandsNamespace =
        "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands";
    const string EventsNamespace =
        "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events";

    static readonly Assembly ContractAssembly = typeof(ExecuteRegimeDiscoveryPipelineCommand).Assembly;
    static readonly Type[] CommandTypes = GetConcreteTypes(CommandsNamespace);
    static readonly Type[] EventTypes = GetConcreteTypes(EventsNamespace);

    static readonly string[] ExpectedCommandNames =
    [
        nameof(ExecuteMarketConditionAssessmentCommand),
        nameof(ExecuteMarketConditionPipelineCommand),
        nameof(ExecuteRegimeDiscoveryPipelineCommand),
        nameof(StartOrderCompositionPipelineCommand),
        nameof(StartRiskManagementPipelineCommand),
        nameof(StartTradeSelectionPipelineCommand)
    ];

    static readonly string[] ExpectedEventNames =
    [
        nameof(MarketConditionAssessmentCompletedEvent),
        nameof(MarketConditionAssessmentFailedEvent),
        nameof(MarketConditionPipelineCompletedEvent),
        nameof(MarketConditionPipelineFailedEvent),
        nameof(OrderCompositionPipelineCompletedEvent),
        nameof(OrderCompositionPipelineFailedEvent),
        nameof(OrderCompositionPipelineProcessingEvent),
        nameof(RegimeDiscoveryPipelineCompletedEvent),
        nameof(RegimeDiscoveryPipelineFailedEvent),
        nameof(RiskManagementPipelineCompletedEvent),
        nameof(RiskManagementPipelineFailedEvent),
        nameof(RiskManagementPipelineProcessingEvent),
        nameof(TradeSelectionPipelineCompletedEvent),
        nameof(TradeSelectionPipelineFailedEvent),
        nameof(TradeSelectionPipelineProcessingEvent)
    ];

    /// <summary>Confirms the boundary contains two Function Execute and three legacy Start pipeline commands.</summary>
    [Fact]
    public void Pipeline_boundary_inventory_is_complete()
    {
        CommandTypes.Select(type => type.Name).Should().Equal(ExpectedCommandNames);
        EventTypes.Select(type => type.Name).Should().Equal(ExpectedEventNames);
    }

    /// <summary>Confirms all boundary contracts have sequential keys and matching serialization constructors.</summary>
    [Fact]
    public void Pipeline_contract_keys_and_constructors_are_stable()
    {
        foreach (var type in CommandTypes.Concat(EventTypes))
        {
            type.GetCustomAttribute<MessagePackObjectAttribute>().Should().NotBeNull(type.Name);
            var keyedProperties = GetKeyedProperties(type);
            keyedProperties.Select(value => value.Key).Should().Equal(
                Enumerable.Range(0, keyedProperties.Length), type.Name);

            var constructor = type.GetConstructors().SingleOrDefault(candidate =>
                candidate.GetCustomAttribute<SerializationConstructorAttribute>() is not null);
            if (constructor is null)
            {
                type.GetConstructor(Type.EmptyTypes).Should().NotBeNull(type.Name);
                continue;
            }
            constructor.GetParameters().Select(parameter => parameter.Name).Should().Equal(
                keyedProperties.Select(value => LowerFirst(value.Property.Name)), type.Name);
            constructor.GetParameters().Select(parameter => parameter.ParameterType).Should().Equal(
                keyedProperties.Select(value => value.Property.PropertyType), type.Name);
        }
    }

    /// <summary>Confirms every populated pipeline boundary contract survives MessagePack serialization.</summary>
    [Fact]
    public void Every_pipeline_boundary_contract_round_trips_through_message_pack()
    {
        foreach (var type in CommandTypes.Concat(EventTypes))
        {
            var expected = CreatePopulatedContract(type);
            var serialized = MessagePackSerializer.Serialize(type, expected);

            var actual = MessagePackSerializer.Deserialize(type, serialized);

            actual.Should().NotBeNull(type.Name);
            MessagePackSerializer.Serialize(type, actual).Should().Equal(serialized, type.Name);
        }
    }

    /// <summary>Confirms pipeline event names and interfaces represent Processing, Completed, or Failed only.</summary>
    [Fact]
    public void Pipeline_events_have_only_the_approved_lifecycle_shapes()
    {
        EventTypes.Should().OnlyContain(type => (type.Name.Contains("Pipeline", StringComparison.Ordinal) || type.Name.StartsWith("MarketConditionAssessment", StringComparison.Ordinal)));
        EventTypes.Where(type => type.Name.EndsWith("ProcessingEvent", StringComparison.Ordinal)).Should()
            .OnlyContain(type => typeof(IEvent).IsAssignableFrom(type) && !typeof(ICompleteEvent).IsAssignableFrom(type));
        EventTypes.Where(type => type.Name.EndsWith("CompletedEvent", StringComparison.Ordinal)).Should()
            .OnlyContain(type => typeof(ICompleteEvent).IsAssignableFrom(type));
        EventTypes.Where(type => type.Name.EndsWith("FailedEvent", StringComparison.Ordinal)).Should()
            .OnlyContain(type => typeof(IErrorEvent).IsAssignableFrom(type));
        EventTypes.Should().NotContain(type =>
            type.Name.StartsWith("Start", StringComparison.Ordinal) &&
            (type.Name.Contains("Complete", StringComparison.Ordinal) ||
             type.Name.Contains("Fail", StringComparison.Ordinal)));
    }

    /// <summary>Confirms pipeline commands contain immutable inputs without next-stage or private pipeline state.</summary>
    [Fact]
    public void Pipeline_commands_preserve_pipeline_ownership_boundaries()
    {
        foreach (var type in CommandTypes)
        {
            if (type == typeof(ExecuteRegimeDiscoveryPipelineCommand) ||
                type == typeof(ExecuteMarketConditionPipelineCommand) || type == typeof(ExecuteMarketConditionAssessmentCommand))
            {
                type.GetProperty(nameof(ExecuteRegimeDiscoveryPipelineCommand.WorkflowView)).Should().NotBeNull();
                type.GetProperty(nameof(ExecuteRegimeDiscoveryPipelineCommand.ExpiresAtUtc)).Should().NotBeNull();
                type.GetProperty("WorkflowState").Should().BeNull();
            }
            else
                type.GetProperty("WorkflowState").Should().NotBeNull(type.Name);
            type.GetProperty("TriggerEvent").Should().NotBeNull(type.Name);
            type.GetProperty("NextPipelineStage").Should().BeNull(type.Name);
            type.GetProperty("NextPipelineActorName").Should().BeNull(type.Name);
            type.GetProperty("PipelinePrivateState").Should().BeNull(type.Name);

            var command = Activator.CreateInstance(type).Should().BeAssignableTo<ICommand>().Subject;
            command!.RouteTo.Should().NotBe(BoundedContextName.Undefined, type.Name);
            type.GetProperty("PostEvents")!.GetValue(command).Should().Be(true, type.Name);
        }
    }

    /// <summary>Confirms the route catalog is complete, ordered, and consistent with contract actor names.</summary>
    [Fact]
    public void Pipeline_route_catalog_has_one_consistent_route_per_stage()
    {
        IntrinsicTimeStrategyPipelineRoutes.All.Select(route => route.Stage).Should().Equal(
            StrategyWorkflowStage.RegimeDiscovery,
            StrategyWorkflowStage.MarketCondition,
            StrategyWorkflowStage.TradeSelection,
            StrategyWorkflowStage.OrderComposition,
            StrategyWorkflowStage.RiskManagement);

        var regime = IntrinsicTimeStrategyPipelineRoutes.Get(StrategyWorkflowStage.RegimeDiscovery);
        regime.CommandActor.Should().Be(
            new ActorMailboxId(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor));
        regime.RealtimeActor.Should().Be(
            new ActorMailboxId(ActorType.Unknown, ExecuteRegimeDiscoveryPipelineCommand.Actor));
        regime.BoundedContext.Should().Be(BoundedContextName.RegimeDiscoveryPipelineBoundedContext);

        var marketCondition = IntrinsicTimeStrategyPipelineRoutes.Get(StrategyWorkflowStage.MarketCondition);
        marketCondition.CommandActor.Should().Be(
            new ActorMailboxId(ActorType.Function, ExecuteMarketConditionPipelineCommand.Actor));
        marketCondition.RealtimeActor.ActorType.Should().Be(ActorType.Unknown);
        marketCondition.BoundedContext.Should().Be(BoundedContextName.MarketConditionPipelineBoundedContext);

        var action = () => IntrinsicTimeStrategyPipelineRoutes.Get(StrategyWorkflowStage.None);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>Confirms the old Regime Start contract has no executable shared type or route.</summary>
    [Fact]
    public void Regime_boundary_exposes_execute_only()
    {
        ExecuteRegimeDiscoveryPipelineCommand.Verb.Should().Be("Execute");
        ContractAssembly.GetType(
            $"{CommandsNamespace}.StartRegimeDiscoveryPipelineCommand").Should().BeNull();
        CommandTypes.Should().ContainSingle(type => type == typeof(ExecuteRegimeDiscoveryPipelineCommand));
    }

    /// <summary>Confirms Started and Continued lifecycle events carry deterministic pipeline dispatch data.</summary>
    [Fact]
    public void Workflow_lifecycle_events_replace_stage_started_events()
    {
        typeof(RegimeDiscoveryPipelineCompletedEvent).Assembly.GetType(
            "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.RegimeDiscoveryStartedEvent")
            .Should().BeNull();

        foreach (var type in new[]
                 {
                     typeof(IntrinsicTimeStrategyWorkflowStartedEvent),
                     typeof(IntrinsicTimeStrategyWorkflowContinuedEvent)
                 })
        {
            type.GetProperty("NextPipelineStage").Should().NotBeNull();
            type.GetProperty("NextPipelineActorType").Should().NotBeNull();
            type.GetProperty("NextPipelineActorName").Should().NotBeNull();
            type.GetProperty("NextPipelineBoundedContext").Should().NotBeNull();
            type.GetProperty("NextPipelineCommandId").Should().NotBeNull();
            type.GetProperty("WorkflowState").Should().NotBeNull();
            type.GetProperty("TriggerEvent").Should().NotBeNull();
        }
    }

    static object CreatePopulatedContract(Type type)
    {
        if (type == typeof(ExecuteMarketConditionAssessmentCommand)) return MessagePackSerializer.Deserialize<ExecuteMarketConditionAssessmentCommand>(MessagePackSerializer.Serialize(MarketCondition.AssessmentFixture.Command()));
        if (type == typeof(MarketConditionAssessmentCompletedEvent)) return new MarketConditionAssessmentCompletedEvent
        { Id = Guid.NewGuid(), WorkflowId = StrategyWorkflowId.New(TimeProvider.System), InputWorkflowRevision = 2 };
        if (type == typeof(MarketConditionAssessmentFailedEvent)) return new MarketConditionAssessmentFailedEvent
        { Id = Guid.NewGuid(), ErrorCode = 1, ErrorMessage = "Qualification failure" };
        var constructor = type.GetConstructors().Single(candidate =>
            candidate.GetCustomAttribute<SerializationConstructorAttribute>() is not null);
        var isCommand = typeof(ICommand).IsAssignableFrom(type);
        return constructor.Invoke(constructor.GetParameters()
            .Select(parameter => CreateSampleValue(parameter.ParameterType, parameter.Name!, isCommand))
            .ToArray());
    }

    static object CreateSampleValue(Type type, string parameterName, bool isCommand)
    {
        if (type == typeof(Guid))
            return Guid.Parse("0198E212-3C00-7000-8000-000000000021");
        if (type == typeof(ActorSubject))
            return new ActorSubject(
                isCommand ? ActorType.Command : ActorType.Realtime,
                "RegimeDiscoveryPipeline",
                "ContractTest",
                CreateEntityId().Format());
        if (type == typeof(bool))
            return true;
        if (type == typeof(IntrinsicTimeStrategyWorkflowEntityId))
            return CreateEntityId();
        if (type == typeof(RegimeDiscoveryExecutionEntityId))
            return RegimeDiscoveryExecutionEntityId.Create(
                CreateEntityId(),
                new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000022")));
        if (type == typeof(MarketConditionExecutionEntityId))
            return MarketConditionExecutionEntityId.Create(
                CreateEntityId(),
                new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000022")));
        if (type == typeof(int))
            return parameterName == "errorCode" ? 24001 : 1;
        if (type == typeof(long))
            return 4L;
        if (type == typeof(string))
            return $"test-{parameterName}";
        if (type == typeof(DateTime))
            return new DateTime(2026, 8, 25, 16,
                parameterName == "expiresAtUtc" ? 5 : 0, 0, DateTimeKind.Utc);
        if (type == typeof(DateTime?))
            return new DateTime(2026, 8, 25, 16, 5, 0, DateTimeKind.Utc);
        if (type == typeof(BoundedContextName))
            return BoundedContextName.RegimeDiscoveryPipelineBoundedContext;
        if (type == typeof(StrategyWorkflowId))
            return new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000022"));
        if (type == typeof(StrategyWorkflowStage))
            return StrategyWorkflowStage.RegimeDiscovery;
        if (type == typeof(TimeFrameType))
            return TimeFrameType.Daily;
        if (type == typeof(RegimeDiscoveryParameterSet))
            return RegimeDiscoveryParameterSet.CreateDefault(
                Guid.Parse("0198E212-3C00-7000-8000-000000000026"),
                Guid.Parse("0198E212-3C00-7000-8000-000000000027"),
                TimeFrameType.Daily);
        if (type == typeof(MarketConditionParameterSet))
            return MarketConditionParameterSet.CreateDefault(
                Guid.Parse("0198E212-3C00-7000-8000-000000000028"),
                Guid.Parse("0198E212-3C00-7000-8000-000000000027"),
                1,
                TimeFrameType.Daily);
        if (type == typeof(MarketConditionFailureCategory))
            return MarketConditionFailureCategory.RequiredInputInvalid;
        if (type == typeof(ErrorType))
            return ErrorType.Command;
        if (type == typeof(IntrinsicTimeStrategyWorkflowState))
            return CreateWorkflowState();
        if (type == typeof(IntrinsicTimeStrategyWorkflowView))
            return CreateWorkflowView();
        if (type == typeof(FuturesItiSignalGeneratedEvent))
            return CreateTriggerEvent();
        if (type == typeof(StrategyStageResultEnvelope))
            return StrategyStageResultEnvelope.Create(
                Guid.Parse("0198E212-3C00-7000-8000-000000000023"),
                "RegimeDiscovery.Result",
                1,
                new byte[] { 4, 5, 6 },
                new DateTime(2026, 8, 25, 15, 59, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 25, 16, 0, 0, DateTimeKind.Utc));

        throw new InvalidOperationException(
            $"No ITSW-4 contract-test value is defined for {type.FullName} ({parameterName}).");
    }

    static IntrinsicTimeStrategyWorkflowState CreateWorkflowState()
        => new()
        {
            EntityId = CreateEntityId(),
            WorkflowId = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000022")),
            TriggerEventId = Guid.Parse("0198E212-3C00-7000-8000-000000000024"),
            CorrelationId = Guid.Parse("0198E212-3C00-7000-8000-000000000022"),
            WorkflowDefinitionVersion = IntrinsicTimeStrategyWorkflowDefinition.Version,
            Status = StrategyWorkflowStatus.Running,
            CurrentStage = StrategyWorkflowStage.RegimeDiscovery,
            WorkflowRevision = 1,
            StartedAtUtc = new DateTime(2026, 8, 25, 15, 58, 0, DateTimeKind.Utc)
        };

    static IntrinsicTimeStrategyWorkflowView CreateWorkflowView()
    {
        var state = CreateWorkflowState();
        return new IntrinsicTimeStrategyWorkflowView
        {
            EntityId = state.EntityId,
            WorkflowId = state.WorkflowId,
            TriggerEventId = state.TriggerEventId,
            CorrelationId = state.CorrelationId,
            CausationId = Guid.Parse("0198E212-3C00-7000-8000-000000000025"),
            WorkflowDefinitionVersion = state.WorkflowDefinitionVersion,
            Status = WorkflowStrategyMachineStatus.Started,
            CurrentStage = state.CurrentStage,
            WorkflowRevision = state.WorkflowRevision,
            StartedAtUtc = state.StartedAtUtc,
            UpdatedAtUtc = new DateTime(2026, 8, 25, 16, 0, 0, DateTimeKind.Utc),
            ExpiresAtUtc = new DateTime(2026, 8, 25, 16, 5, 0, DateTimeKind.Utc),
            RegimeDiscovery = state.RegimeDiscovery,
            MarketCondition = state.MarketCondition,
            TradeSelection = state.TradeSelection,
            OrderComposition = state.OrderComposition,
            RiskManagement = state.RiskManagement,
            RegimeDiscoveryParameterSet = state.RegimeDiscoveryParameterSet,
            RegimeDiscoveryParameterPayloadSha256 = state.RegimeDiscoveryParameterPayloadSha256,
            TriggerEvent = CreateTriggerEvent()
        };
    }

    static FuturesItiSignalGeneratedEvent CreateTriggerEvent()
    {
        var entityId = new FuturesItiSignalEntityId(
            "ES-202609",
            new DateOnly(2026, 8, 25),
            TimeFrameType.Daily);
        return new FuturesItiSignalGeneratedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesItiSignalGeneratedEvent.Actor,
                FuturesItiSignalGeneratedEvent.Verb,
                entityId.Format()),
            Id = Guid.Parse("0198E212-3C00-7000-8000-000000000024"),
            EntityId = entityId,
            EventId = 3,
            CommandId = Guid.Parse("0198E212-3C00-7000-8000-000000000025"),
            AggregateId = entityId.Format(),
            EventSource = "FuturesItiSignalCommandActor",
            ReceivedOn = new DateTime(2026, 8, 25, 15, 58, 0, DateTimeKind.Utc),
            CreatedOn = new DateTime(2026, 8, 25, 15, 58, 0, DateTimeKind.Utc),
            CreatedBy = "contract-test",
            VixFuturesPrice = 17.25
        };
    }

    static IntrinsicTimeStrategyWorkflowEntityId CreateEntityId()
        => IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 25), TimeFrameType.Daily));

    static Type[] GetConcreteTypes(string @namespace)
        => ContractAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.Namespace == @namespace)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

    static (PropertyInfo Property, int Key)[] GetKeyedProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Attribute: property.GetCustomAttribute<KeyAttribute>()))
            .Where(value => value.Attribute is not null)
            .Select(value => (value.Property, IntKey: value.Attribute!.IntKey!.Value))
            .OrderBy(value => value.IntKey)
            .Select(value => (value.Property, Key: value.IntKey))
            .ToArray();

    static string LowerFirst(string value) => char.ToLowerInvariant(value[0]) + value[1..];
}
