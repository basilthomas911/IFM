using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Verifies every workflow command and workflow-owned event-log contract introduced by ITSW-3.</summary>
public sealed class IntrinsicTimeStrategyWorkflowMessageContractTests
{
    const string CommandsNamespace =
        "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands";
    const string EventsNamespace =
        "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events";

    static readonly Assembly ContractAssembly = typeof(StartIntrinsicTimeStrategyWorkflowCommand).Assembly;
    static readonly Type[] CommandTypes = ContractAssembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && type.Namespace == CommandsNamespace)
        .OrderBy(type => type.Name, StringComparer.Ordinal)
        .ToArray();
    static readonly Type[] EventTypes = ContractAssembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && type.Namespace == EventsNamespace)
        .OrderBy(type => type.Name, StringComparer.Ordinal)
        .ToArray();

    static readonly string[] ExpectedCommandNames =
    [
        nameof(CancelIntrinsicTimeStrategyWorkflowCommand),
        nameof(CompleteMarketConditionCommand),
        nameof(CompleteOrderCompositionCommand),
        nameof(CompleteRegimeDiscoveryCommand),
        nameof(CompleteRiskManagementCommand),
        nameof(CompleteTradeSelectionCommand),
        nameof(FailMarketConditionCommand),
        nameof(FailOrderCompositionCommand),
        nameof(FailRegimeDiscoveryCommand),
        nameof(FailRiskManagementCommand),
        nameof(FailTradeSelectionCommand),
        nameof(RedispatchCurrentStrategyPipelineCommand),
        nameof(StartIntrinsicTimeStrategyWorkflowCommand),
        nameof(TimeoutMarketConditionCommand),
        nameof(TimeoutOrderCompositionCommand),
        nameof(TimeoutRegimeDiscoveryCommand),
        nameof(TimeoutRiskManagementCommand),
        nameof(TimeoutTradeSelectionCommand)
    ];

    static readonly string[] ExpectedEventNames =
    [
        nameof(IntrinsicTimeStrategyWorkflowCompletedEvent),
        nameof(IntrinsicTimeStrategyWorkflowContinuedEvent),
        nameof(IntrinsicTimeStrategyWorkflowStartedEvent),
        nameof(IntrinsicTimeStrategyWorkflowStoppedEvent),
        nameof(StrategyWorkflowMarketConditionContinuationEvaluatedEvent),
        nameof(StrategyWorkflowMarketConditionFailedEvent),
        nameof(StrategyWorkflowMarketConditionResultRecordedEvent),
        nameof(StrategyWorkflowMarketConditionTimedOutEvent),
        nameof(StrategyWorkflowOrderCompositionContinuationEvaluatedEvent),
        nameof(StrategyWorkflowOrderCompositionFailedEvent),
        nameof(StrategyWorkflowOrderCompositionResultRecordedEvent),
        nameof(StrategyWorkflowOrderCompositionTimedOutEvent),
        nameof(StrategyWorkflowRegimeDiscoveryContinuationEvaluatedEvent),
        nameof(StrategyWorkflowRegimeDiscoveryFailedEvent),
        nameof(StrategyWorkflowRegimeDiscoveryResultRecordedEvent),
        nameof(StrategyWorkflowRegimeDiscoveryTimedOutEvent),
        nameof(StrategyWorkflowRiskManagementContinuationEvaluatedEvent),
        nameof(StrategyWorkflowRiskManagementFailedEvent),
        nameof(StrategyWorkflowRiskManagementResultRecordedEvent),
        nameof(StrategyWorkflowRiskManagementTimedOutEvent),
        nameof(StrategyWorkflowStartAcceptedEvent),
        nameof(StrategyWorkflowStartRejectedEvent),
        nameof(StrategyWorkflowTradeSelectionContinuationEvaluatedEvent),
        nameof(StrategyWorkflowTradeSelectionFailedEvent),
        nameof(StrategyWorkflowTradeSelectionResultRecordedEvent),
        nameof(StrategyWorkflowTradeSelectionTimedOutEvent)
    ];

    /// <summary>Confirms the gate contains exactly the specified 18 commands and 29 workflow-owned events.</summary>
    [Fact]
    public void Contract_inventory_is_complete()
    {
        CommandTypes.Select(type => type.Name).Should().Equal(ExpectedCommandNames);
        EventTypes.Select(type => type.Name).Should().Equal(ExpectedEventNames);
    }

    /// <summary>Confirms command and event MessagePack integer keys are unique and sequential.</summary>
    [Fact]
    public void Message_pack_keys_are_sequential_and_follow_base_contracts()
    {
        foreach (var type in CommandTypes)
            AssertSequentialKeys(type, minimumCustomKey: 6);

        foreach (var type in EventTypes)
            AssertSequentialKeys(type, minimumCustomKey: 8);
    }

    /// <summary>Confirms every serialization constructor follows the exact keyed-property order.</summary>
    [Fact]
    public void Serialization_constructor_order_matches_message_pack_keys()
    {
        foreach (var type in CommandTypes.Concat(EventTypes))
        {
            var keyedProperties = GetKeyedProperties(type);
            var constructor = type.GetConstructors().Single(candidate =>
                candidate.GetCustomAttribute<SerializationConstructorAttribute>() is not null);

            constructor.GetParameters().Select(parameter => parameter.Name).Should().Equal(
                keyedProperties.Select(property => LowerFirst(property.Property.Name)));
            constructor.GetParameters().Select(parameter => parameter.ParameterType).Should().Equal(
                keyedProperties.Select(property => property.Property.PropertyType));
        }
    }

    /// <summary>Confirms default workflow commands select the workflow bounded context and event projection.</summary>
    [Fact]
    public void Default_commands_have_required_routing_metadata()
    {
        foreach (var type in CommandTypes)
        {
            var command = Activator.CreateInstance(type).Should().BeAssignableTo<ICommand>().Subject;

            command!.RouteTo.Should().Be(BoundedContextName.IntrinsicTimeStrategyWorkflowBoundedContext);
            command.ErrorCode.Should().BePositive();
            type.GetProperty("PostEvents")!.GetValue(command).Should().Be(true);
            command.EventSource.Should().Be("IntrinsicTimeStrategyWorkflowCommandActor");
        }
    }

    /// <summary>Confirms every populated workflow message survives a deterministic MessagePack round trip.</summary>
    [Fact]
    public void Every_workflow_message_round_trips_through_message_pack()
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

    /// <summary>Confirms workflow-owned events remain domain-log contracts rather than Event actors.</summary>
    [Fact]
    public void Workflow_events_are_domain_events_without_actor_implementation()
    {
        foreach (var type in EventTypes)
        {
            type.GetInterfaces().Should().Contain(contract =>
                contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEvent<>));
            type.GetInterfaces().Should().NotContain(contract => contract.Name.StartsWith("IActor", StringComparison.Ordinal));

            var @event = CreatePopulatedContract(type).Should().BeAssignableTo<IEvent>().Subject;
            @event!.EventType.Should().Be(EventType.DomainEvent);
        }
    }

    /// <summary>Confirms stage result commands cannot return continuation decisions or mutated workflow state.</summary>
    [Fact]
    public void Pipeline_result_commands_contain_no_continuation_or_workflow_state()
    {
        foreach (var type in CommandTypes.Where(type =>
                     type.Name.StartsWith("Complete", StringComparison.Ordinal) ||
                     type.Name.StartsWith("Fail", StringComparison.Ordinal)))
        {
            type.GetProperty("WorkflowState").Should().BeNull(type.Name);
            type.GetProperty("ContinuationDecision").Should().BeNull(type.Name);
            type.GetProperty("NextStage").Should().BeNull(type.Name);
        }
    }

    /// <summary>Confirms continuation reason-code arrays cannot mutate a persisted event through shared references.</summary>
    [Fact]
    public void Continuation_events_defensively_copy_reason_codes()
    {
        string[] source = ["VALID_RESULT", "PROCEED"];
        var @event = new StrategyWorkflowRegimeDiscoveryContinuationEvaluatedEvent
        {
            ReasonCodes = source
        };

        source[0] = "SOURCE_MUTATED";
        var exposed = @event.ReasonCodes;
        exposed[1] = "EXPOSED_MUTATED";

        @event.ReasonCodes.Should().Equal("VALID_RESULT", "PROCEED");
    }

    /// <summary>Confirms all six strategy bounded-context routes are present for this and the next gate.</summary>
    [Fact]
    public void Strategy_bounded_context_names_are_defined()
    {
        Enum.IsDefined(BoundedContextName.IntrinsicTimeStrategyWorkflowBoundedContext).Should().BeTrue();
        Enum.IsDefined(BoundedContextName.RegimeDiscoveryPipelineBoundedContext).Should().BeTrue();
        Enum.IsDefined(BoundedContextName.MarketConditionPipelineBoundedContext).Should().BeTrue();
        Enum.IsDefined(BoundedContextName.TradeSelectionPipelineBoundedContext).Should().BeTrue();
        Enum.IsDefined(BoundedContextName.OrderCompositionPipelineBoundedContext).Should().BeTrue();
        Enum.IsDefined(BoundedContextName.RiskManagementPipelineBoundedContext).Should().BeTrue();
    }

    static object CreatePopulatedContract(Type type)
    {
        var constructor = type.GetConstructors().Single(candidate =>
            candidate.GetCustomAttribute<SerializationConstructorAttribute>() is not null);
        var isCommand = typeof(ICommand).IsAssignableFrom(type);
        var arguments = constructor.GetParameters()
            .Select(parameter => CreateSampleValue(parameter.ParameterType, parameter.Name!, isCommand))
            .ToArray();
        return constructor.Invoke(arguments);
    }

    static object CreateSampleValue(Type type, string parameterName, bool isCommand)
    {
        if (type == typeof(Guid))
            return Guid.Parse("0198E212-3C00-7000-8000-000000000011");
        if (type == typeof(ActorSubject))
            return new ActorSubject(
                isCommand ? ActorType.Command : ActorType.Event,
                "IntrinsicTimeStrategyWorkflow",
                "ContractTest",
                CreateEntityId().Format());
        if (type == typeof(bool))
            return true;
        if (type == typeof(IntrinsicTimeStrategyWorkflowEntityId))
            return CreateEntityId();
        if (type == typeof(int))
            return parameterName == "errorCode" ? 21001 : 1;
        if (type == typeof(long))
            return 7L;
        if (type == typeof(string))
            return $"test-{parameterName}";
        if (type == typeof(DateTime))
            return new DateTime(2026, 8, 25, 15, 30, 0, DateTimeKind.Utc);
        if (type == typeof(BoundedContextName))
            return BoundedContextName.IntrinsicTimeStrategyWorkflowBoundedContext;
        if (type == typeof(ActorType))
            return ActorType.Command;
        if (type == typeof(StrategyWorkflowId))
            return new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000012"));
        if (type == typeof(RegimeDiscoveryParameterSet))
            return RegimeDiscoveryParameterSet.CreateDefault(
                Guid.Parse("0198E212-3C00-7000-8000-000000000016"),
                Guid.Parse("0198E212-3C00-7000-8000-000000000017"),
                TimeFrameType.Daily);
        if (type == typeof(FuturesItiSignalGeneratedEvent))
            return CreateTriggerEvent();
        if (type == typeof(StrategyStageResultEnvelope))
            return StrategyStageResultEnvelope.Create(
                Guid.Parse("0198E212-3C00-7000-8000-000000000013"),
                "ContractTest.Result",
                1,
                new byte[] { 1, 2, 3 },
                new DateTime(2026, 8, 25, 15, 29, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 25, 15, 30, 0, DateTimeKind.Utc));
        if (type == typeof(StrategyPipelineFailure))
            return new StrategyPipelineFailure
            {
                ErrorCode = 1,
                ErrorMessage = "contract-test-failure",
                ErrorType = "ContractTest",
                ErrorData = "none",
                FailedAtUtc = new DateTime(2026, 8, 25, 15, 30, 0, DateTimeKind.Utc)
            };
        if (type == typeof(StrategyWorkflowStage))
            return StrategyWorkflowStage.RegimeDiscovery;
        if (type == typeof(StrategyWorkflowContinuationDecision))
            return StrategyWorkflowContinuationDecision.Proceed;
        if (type == typeof(StrategyWorkflowOutcome))
            return StrategyWorkflowOutcome.PipelineFailed;
        if (type == typeof(IntrinsicTimeStrategyWorkflowState))
            return new IntrinsicTimeStrategyWorkflowState
            {
                EntityId = CreateEntityId(),
                WorkflowId = new StrategyWorkflowId(
                    Guid.Parse("0198E212-3C00-7000-8000-000000000012")),
                TriggerEventId = Guid.Parse("0198E212-3C00-7000-8000-000000000014"),
                CorrelationId = Guid.Parse("0198E212-3C00-7000-8000-000000000012"),
                WorkflowDefinitionVersion = IntrinsicTimeStrategyWorkflowDefinition.Version,
                Status = StrategyWorkflowStatus.Running,
                CurrentStage = StrategyWorkflowStage.RegimeDiscovery,
                WorkflowRevision = 1,
                StartedAtUtc = new DateTime(2026, 8, 25, 15, 30, 0, DateTimeKind.Utc)
            };
        if (type == typeof(DateTime?))
            return new DateTime(2026, 8, 25, 15, 35, 0, DateTimeKind.Utc);
        if (type == typeof(string[]))
            return new[] { "VALID_RESULT", "PROCEED" };

        throw new InvalidOperationException(
            $"No ITSW-3 contract-test value is defined for {type.FullName} ({parameterName}).");
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
            Id = Guid.Parse("0198E212-3C00-7000-8000-000000000014"),
            EntityId = entityId,
            EventId = 3,
            CommandId = Guid.Parse("0198E212-3C00-7000-8000-000000000015"),
            AggregateId = entityId.Format(),
            EventSource = "FuturesItiSignalCommandActor",
            ReceivedOn = new DateTime(2026, 8, 25, 15, 29, 0, DateTimeKind.Utc),
            CreatedOn = new DateTime(2026, 8, 25, 15, 29, 0, DateTimeKind.Utc),
            CreatedBy = "contract-test",
            VixFuturesPrice = 17.25
        };
    }

    static IntrinsicTimeStrategyWorkflowEntityId CreateEntityId()
        => IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 25), TimeFrameType.Daily));

    static (PropertyInfo Property, int Key)[] GetKeyedProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Attribute: property.GetCustomAttribute<KeyAttribute>()))
            .Where(value => value.Attribute is not null)
            .Select(value => (value.Property, IntKey: value.Attribute!.IntKey!.Value))
            .OrderBy(value => value.IntKey)
            .Select(value => (value.Property, Key: value.IntKey))
            .ToArray();

    static void AssertSequentialKeys(Type type, int minimumCustomKey)
    {
        type.GetCustomAttribute<MessagePackObjectAttribute>().Should().NotBeNull(type.Name);
        var keyedProperties = GetKeyedProperties(type);
        keyedProperties.Select(value => value.Key).Should().Equal(
            Enumerable.Range(0, keyedProperties.Length), type.Name);
        keyedProperties.Should().Contain(value => value.Key == minimumCustomKey, type.Name);
    }

    static string LowerFirst(string value) => char.ToLowerInvariant(value[0]) + value[1..];
}
