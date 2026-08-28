using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Verifies the shared identity and enum contracts introduced by ITSW-1.</summary>
public sealed class IntrinsicTimeStrategyWorkflowIdentityTests
{
    /// <summary>Confirms the stable workflow definition contract.</summary>
    [Fact]
    public void Definition_has_stable_identity_and_version()
    {
        IntrinsicTimeStrategyWorkflowDefinition.Id.Should().Be("IntrinsicTimeStrategy");
        IntrinsicTimeStrategyWorkflowDefinition.Version.Should().Be(1);
    }

    /// <summary>Confirms Daily, Weekly, and Monthly identities format using the complete ITI identity.</summary>
    [Theory]
    [InlineData(TimeFrameType.Daily, 2026, 8, 24, "IntrinsicTimeStrategy.ES-202609.20260824.Daily")]
    [InlineData(TimeFrameType.Weekly, 2026, 8, 18, "IntrinsicTimeStrategy.ES-202609.20260818.Weekly")]
    [InlineData(TimeFrameType.Monthly, 2026, 8, 1, "IntrinsicTimeStrategy.ES-202609.20260801.Monthly")]
    public void Entity_identity_formats_stable_routing_key(
        TimeFrameType timePeriod,
        int year,
        int month,
        int day,
        string expected)
    {
        var itiEntityId = new FuturesItiSignalEntityId(
            "ES-202609",
            new DateOnly(year, month, day),
            timePeriod);

        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(itiEntityId);

        entityId.Format().Should().Be(expected);
        entityId.ToString().Should().Be(expected);
    }

    /// <summary>Confirms the workflow identity survives MessagePack serialization unchanged.</summary>
    [Fact]
    public void Entity_identity_round_trips_through_message_pack()
    {
        var expected = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("VX-202609", new DateOnly(2026, 8, 24), TimeFrameType.Daily));

        var actual = MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowEntityId>(
            MessagePackSerializer.Serialize(expected));

        actual.Should().Be(expected);
        actual.Format().Should().Be(expected.Format());
    }

    /// <summary>Confirms a valid workflow identity has no validation failures.</summary>
    [Fact]
    public void Entity_identity_validation_accepts_supported_timeframe()
    {
        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 24), TimeFrameType.Daily));

        var errors = new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(entityId);

        errors.Should().BeEmpty();
    }

    /// <summary>Confirms validation rejects an unsupported workflow definition.</summary>
    [Fact]
    public void Entity_identity_validation_rejects_wrong_definition()
    {
        var entityId = new IntrinsicTimeStrategyWorkflowEntityId(
            "OtherWorkflow",
            new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 24), TimeFrameType.Daily));

        var errors = new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(entityId);

        errors.Should().Contain(error =>
            error.ErrorMessage == IntrinsicTimeStrategyWorkflowEntityIdValidationRules.WorkflowDefinitionErrorMessage);
    }

    /// <summary>Confirms only Daily, Weekly, and Monthly ITI periods can route this workflow.</summary>
    [Theory]
    [InlineData(TimeFrameType.None)]
    [InlineData(TimeFrameType.OneMinute)]
    [InlineData(TimeFrameType.Quarterly)]
    public void Entity_identity_validation_rejects_ineligible_timeframe(TimeFrameType timePeriod)
    {
        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 24), timePeriod));

        var errors = new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(entityId);

        errors.Should().Contain(error =>
            error.ErrorMessage == IntrinsicTimeStrategyWorkflowEntityIdValidationRules.TimePeriodErrorMessage);
    }

    /// <summary>Confirms required ITI identity fields are validated.</summary>
    [Fact]
    public void Entity_identity_validation_rejects_missing_contract_and_date()
    {
        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId(string.Empty, DateOnly.MinValue, TimeFrameType.Daily));

        var errors = new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(entityId);

        errors.Should().Contain(error =>
            error.ErrorMessage == IntrinsicTimeStrategyWorkflowEntityIdValidationRules.ContractIdErrorMessage);
        errors.Should().Contain(error =>
            error.ErrorMessage == IntrinsicTimeStrategyWorkflowEntityIdValidationRules.ValueDateErrorMessage);
    }

    /// <summary>Confirms a missing nested ITI identity produces a validation error instead of an exception.</summary>
    [Fact]
    public void Entity_identity_validation_rejects_missing_iti_identity()
    {
        var entityId = new IntrinsicTimeStrategyWorkflowEntityId
        {
            WorkflowDefinitionId = IntrinsicTimeStrategyWorkflowDefinition.Id,
            ItiSignalEntityId = null!
        };

        var errors = new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(entityId);

        errors.Should().Contain(error =>
            error.ErrorMessage == IntrinsicTimeStrategyWorkflowEntityIdValidationRules.ItiSignalEntityErrorMessage);
    }

    /// <summary>Confirms the default record-struct value is rejected before actor routing.</summary>
    [Fact]
    public void Entity_identity_validation_rejects_default_struct_value()
    {
        var errors = new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(default);

        errors.Should().Contain(error =>
            error.ErrorMessage == IntrinsicTimeStrategyWorkflowEntityIdValidationRules.WorkflowDefinitionErrorMessage);
        errors.Should().Contain(error =>
            error.ErrorMessage == IntrinsicTimeStrategyWorkflowEntityIdValidationRules.ItiSignalEntityErrorMessage);
    }

    /// <summary>Confirms generated workflow execution IDs are UUIDv7 and time ordered.</summary>
    [Fact]
    public void Execution_identity_is_non_empty_uuid_v7_and_time_ordered()
    {
        var first = StrategyWorkflowId.New(new FixedTimeProvider(new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)));
        var second = StrategyWorkflowId.New(new FixedTimeProvider(new DateTimeOffset(2026, 8, 25, 12, 0, 1, TimeSpan.Zero)));

        first.Value.Should().NotBeEmpty();
        first.Value.Version.Should().Be(7);
        string.CompareOrdinal(first.ToString(), second.ToString()).Should().BeLessThan(0);
        new StrategyWorkflowIdValidationRules().Execute(first).Should().BeEmpty();
    }

    /// <summary>Confirms execution identities parse and round-trip through MessagePack.</summary>
    [Fact]
    public void Execution_identity_parses_and_round_trips_through_message_pack()
    {
        var expected = StrategyWorkflowId.New(
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero)));

        StrategyWorkflowId.TryParse(expected.ToString(), out var parsed).Should().BeTrue();
        parsed.Should().Be(expected);
        StrategyWorkflowId.Parse(expected.ToString()).Should().Be(expected);

        var actual = MessagePackSerializer.Deserialize<StrategyWorkflowId>(
            MessagePackSerializer.Serialize(expected));
        actual.Should().Be(expected);
    }

    /// <summary>Confirms malformed text cannot create a workflow execution identity.</summary>
    [Fact]
    public void Execution_identity_try_parse_rejects_invalid_text()
    {
        StrategyWorkflowId.TryParse("not-a-guid", out var workflowId).Should().BeFalse();
        workflowId.Should().Be(default(StrategyWorkflowId));
    }

    /// <summary>Confirms execution identity validation rejects empty and non-version-7 GUIDs.</summary>
    [Fact]
    public void Execution_identity_validation_rejects_empty_and_non_uuid_v7_values()
    {
        var rules = new StrategyWorkflowIdValidationRules();

        rules.Execute(default).Should().Contain(error =>
            error.ErrorMessage == StrategyWorkflowIdValidationRules.EmptyErrorMessage);
        rules.Execute(new StrategyWorkflowId(Guid.NewGuid())).Should().Contain(error =>
            error.ErrorMessage == StrategyWorkflowIdValidationRules.VersionErrorMessage);
    }

    /// <summary>Confirms Regime execution identity combines the stable entity with exactly one workflow execution.</summary>
    [Fact]
    public void Regime_execution_identity_is_composite_stable_and_serializable()
    {
        var workflowEntityId = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 24), TimeFrameType.Daily));
        var workflowId = new StrategyWorkflowId(
            Guid.Parse("0198E212-3C00-7000-8000-000000000012"));
        var expected = RegimeDiscoveryExecutionEntityId.Create(workflowEntityId, workflowId);

        expected.Format().Should().Be(
            "IntrinsicTimeStrategy.ES-202609.20260824.Daily.RegimeDiscovery.0198e2123c0070008000000000000012");
        expected.ToString().Should().Be(expected.Format());
        new RegimeDiscoveryExecutionEntityIdValidationRules().Execute(expected).Should().BeEmpty();

        var actual = MessagePackSerializer.Deserialize<RegimeDiscoveryExecutionEntityId>(
            MessagePackSerializer.Serialize(expected));
        actual.Should().Be(expected);
        actual.Format().Should().Be(expected.Format());
    }

    /// <summary>Confirms consecutive workflows for one strategy entity cannot share a Regime private stream.</summary>
    [Fact]
    public void Regime_execution_identity_isolated_by_workflow_id()
    {
        var workflowEntityId = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 24), TimeFrameType.Daily));
        var first = RegimeDiscoveryExecutionEntityId.Create(workflowEntityId,
            new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000012")));
        var second = RegimeDiscoveryExecutionEntityId.Create(workflowEntityId,
            new StrategyWorkflowId(Guid.Parse("0198E212-3C01-7000-8000-000000000012")));
        var firstSubject = new ActorSubject(
            ActorType.Command,
            ExecuteRegimeDiscoveryPipelineCommand.Actor,
            ExecuteRegimeDiscoveryPipelineCommand.Verb,
            first.Format());
        var secondSubject = new ActorSubject(
            ActorType.Command,
            ExecuteRegimeDiscoveryPipelineCommand.Actor,
            ExecuteRegimeDiscoveryPipelineCommand.Verb,
            second.Format());

        second.Should().NotBe(first);
        second.Format().Should().NotBe(first.Format());
        secondSubject.EntityId.Should().NotBe(firstSubject.EntityId);
        secondSubject.StreamId.Should().NotBe(firstSubject.StreamId);
    }

    /// <summary>Confirms an invalid component makes the complete Regime execution identity invalid.</summary>
    [Fact]
    public void Regime_execution_identity_validation_rejects_invalid_components()
    {
        var errors = new RegimeDiscoveryExecutionEntityIdValidationRules().Execute(default);

        errors.Should().Contain(error =>
            error.ErrorMessage == IntrinsicTimeStrategyWorkflowEntityIdValidationRules.WorkflowDefinitionErrorMessage);
        errors.Should().Contain(error =>
            error.ErrorMessage == StrategyWorkflowIdValidationRules.EmptyErrorMessage);
    }

    /// <summary>Locks the serialized numeric values of all workflow enums.</summary>
    [Fact]
    public void Workflow_enum_values_are_stable()
    {
        ((int)StrategyWorkflowStartDecision.None).Should().Be(0);
        ((int)StrategyWorkflowStartDecision.Accepted).Should().Be(1);
        ((int)StrategyWorkflowStartDecision.Rejected).Should().Be(2);

        ((int)StrategyWorkflowStage.None).Should().Be(0);
        ((int)StrategyWorkflowStage.RegimeDiscovery).Should().Be(1);
        ((int)StrategyWorkflowStage.MarketCondition).Should().Be(2);
        ((int)StrategyWorkflowStage.TradeSelection).Should().Be(3);
        ((int)StrategyWorkflowStage.OrderComposition).Should().Be(4);
        ((int)StrategyWorkflowStage.RiskManagement).Should().Be(5);

        ((int)StrategyWorkflowStatus.None).Should().Be(0);
        ((int)StrategyWorkflowStatus.Running).Should().Be(1);
        ((int)StrategyWorkflowStatus.Completed).Should().Be(2);
        ((int)StrategyWorkflowStatus.Stopped).Should().Be(3);

        ((int)WorkflowStrategyMachineStatus.Empty).Should().Be(0);
        ((int)WorkflowStrategyMachineStatus.Started).Should().Be(1);
        ((int)WorkflowStrategyMachineStatus.Completed).Should().Be(2);
        ((int)WorkflowStrategyMachineStatus.Failed).Should().Be(3);
        ((int)WorkflowStrategyMachineStatus.TimedOut).Should().Be(4);
        ((int)WorkflowStrategyMachineStatus.Cancelled).Should().Be(5);

        ((int)StrategyWorkflowOutcome.None).Should().Be(0);
        ((int)StrategyWorkflowOutcome.Completed).Should().Be(1);
        ((int)StrategyWorkflowOutcome.PipelineFailed).Should().Be(2);
        ((int)StrategyWorkflowOutcome.InvalidResult).Should().Be(3);
        ((int)StrategyWorkflowOutcome.TimedOut).Should().Be(4);
        ((int)StrategyWorkflowOutcome.Cancelled).Should().Be(5);
        ((int)StrategyWorkflowOutcome.ConsistencyFault).Should().Be(6);

        ((int)StrategyActorProcessingStatus.NotStarted).Should().Be(0);
        ((int)StrategyActorProcessingStatus.Processing).Should().Be(1);
        ((int)StrategyActorProcessingStatus.Completed).Should().Be(2);
        ((int)StrategyActorProcessingStatus.Failed).Should().Be(3);
        ((int)StrategyActorProcessingStatus.TimedOut).Should().Be(4);
        ((int)StrategyActorProcessingStatus.Cancelled).Should().Be(5);

        ((int)StrategyWorkflowContinuationDecision.None).Should().Be(0);
        ((int)StrategyWorkflowContinuationDecision.Proceed).Should().Be(1);
        ((int)StrategyWorkflowContinuationDecision.Stop).Should().Be(2);
    }

    /// <summary>Confirms every workflow enum round-trips through MessagePack using its stable numeric value.</summary>
    [Fact]
    public void Workflow_enums_round_trip_through_message_pack()
    {
        RoundTrip(StrategyWorkflowStartDecision.Rejected).Should().Be(StrategyWorkflowStartDecision.Rejected);
        RoundTrip(StrategyWorkflowStage.RiskManagement).Should().Be(StrategyWorkflowStage.RiskManagement);
        RoundTrip(StrategyWorkflowStatus.Stopped).Should().Be(StrategyWorkflowStatus.Stopped);
        RoundTrip(WorkflowStrategyMachineStatus.TimedOut).Should().Be(WorkflowStrategyMachineStatus.TimedOut);
        RoundTrip(StrategyWorkflowOutcome.ConsistencyFault).Should().Be(StrategyWorkflowOutcome.ConsistencyFault);
        RoundTrip(StrategyActorProcessingStatus.Cancelled).Should().Be(StrategyActorProcessingStatus.Cancelled);
        RoundTrip(StrategyWorkflowContinuationDecision.Stop).Should().Be(StrategyWorkflowContinuationDecision.Stop);
    }

    static T RoundTrip<T>(T value) where T : struct, Enum
        => MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value));

    sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
