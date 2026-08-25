using System.Runtime.InteropServices;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Verifies the immutable workflow state and opaque result contracts introduced by ITSW-2.</summary>
public sealed class IntrinsicTimeStrategyWorkflowStateTests
{
    static readonly DateTime MarketDataAsOfUtc = new(2026, 8, 25, 14, 30, 0, DateTimeKind.Utc);
    static readonly DateTime ProducedAtUtc = new(2026, 8, 25, 14, 30, 1, DateTimeKind.Utc);

    /// <summary>Confirms envelope creation calculates the digest over the exact payload bytes.</summary>
    [Fact]
    public void Result_envelope_calculates_exact_payload_digest()
    {
        byte[] payload = [0x01, 0x10, 0x7F, 0x80, 0xFF];

        var envelope = CreateEnvelope(payload);

        envelope.PayloadSha256.Should().Be(StrategyStageResultEnvelope.ComputePayloadSha256(payload));
        envelope.HasValidPayloadSha256().Should().BeTrue();
        new StrategyStageResultEnvelopeValidationRules().Execute(envelope).Should().BeEmpty();
    }

    /// <summary>Confirms source and exposed payload buffers cannot mutate the stored opaque result.</summary>
    [Fact]
    public void Result_envelope_defensively_copies_payload_buffers()
    {
        byte[] source = [1, 2, 3];
        var envelope = CreateEnvelope(source);

        source[0] = 90;
        var exposed = envelope.Payload;
        MemoryMarshal.TryGetArray(exposed, out var exposedSegment).Should().BeTrue();
        exposedSegment.Array![exposedSegment.Offset] = 80;

        envelope.Payload.ToArray().Should().Equal(1, 2, 3);
        envelope.HasValidPayloadSha256().Should().BeTrue();
    }

    /// <summary>Confirms the default and configurable payload limits are enforced.</summary>
    [Fact]
    public void Result_envelope_enforces_payload_limits()
    {
        var oversizedDefaultPayload = new byte[StrategyStageResultEnvelope.DefaultMaximumPayloadBytes + 1];

        var defaultLimitAction = () => CreateEnvelope(oversizedDefaultPayload);
        var stageLimitAction = () => CreateEnvelope(new byte[] { 1, 2, 3, 4 }, maximumPayloadBytes: 3);

        defaultLimitAction.Should().Throw<ArgumentOutOfRangeException>();
        stageLimitAction.Should().Throw<ArgumentOutOfRangeException>();

        var envelope = CreateEnvelope(new byte[] { 1, 2, 3, 4 });
        new StrategyStageResultEnvelopeValidationRules(3).Execute(envelope).Should().Contain(error =>
            error.ErrorMessage == StrategyStageResultEnvelopeValidationRules.PayloadLimitErrorMessage);
    }

    /// <summary>Confirms an empty result payload is rejected until a stage contract explicitly permits one.</summary>
    [Fact]
    public void Result_envelope_rejects_empty_payload()
    {
        var action = () => CreateEnvelope(Array.Empty<byte>());

        action.Should().Throw<ArgumentException>();

        var invalid = new StrategyStageResultEnvelope
        {
            ResultId = Guid.NewGuid(),
            ResultType = "RegimeDiscovery.Result",
            SchemaVersion = 1,
            Payload = ReadOnlyMemory<byte>.Empty,
            PayloadSha256 = StrategyStageResultEnvelope.ComputePayloadSha256([])
        };
        new StrategyStageResultEnvelopeValidationRules().Execute(invalid).Should().Contain(error =>
            error.ErrorMessage == StrategyStageResultEnvelopeValidationRules.PayloadRequiredErrorMessage);
    }

    /// <summary>Confirms validation detects a digest that does not represent the stored payload.</summary>
    [Fact]
    public void Result_envelope_validation_rejects_tampered_digest()
    {
        var envelope = CreateEnvelope(new byte[] { 1, 2, 3 }) with { PayloadSha256 = new string('0', 64) };

        envelope.HasValidPayloadSha256().Should().BeFalse();
        new StrategyStageResultEnvelopeValidationRules().Execute(envelope).Should().Contain(error =>
            error.ErrorMessage == StrategyStageResultEnvelopeValidationRules.PayloadHashErrorMessage);
    }

    /// <summary>Confirms required result metadata and a positive schema version are validated.</summary>
    [Fact]
    public void Result_envelope_validation_rejects_invalid_metadata()
    {
        byte[] payload = [1];
        var invalid = new StrategyStageResultEnvelope
        {
            ResultId = Guid.Empty,
            ResultType = string.Empty,
            SchemaVersion = 0,
            ContentType = string.Empty,
            Payload = payload,
            PayloadSha256 = StrategyStageResultEnvelope.ComputePayloadSha256(payload)
        };

        var errors = new StrategyStageResultEnvelopeValidationRules().Execute(invalid);

        errors.Should().Contain(error =>
            error.ErrorMessage == StrategyStageResultEnvelopeValidationRules.ResultIdErrorMessage);
        errors.Should().Contain(error =>
            error.ErrorMessage == StrategyStageResultEnvelopeValidationRules.ResultTypeErrorMessage);
        errors.Should().Contain(error =>
            error.ErrorMessage == StrategyStageResultEnvelopeValidationRules.SchemaVersionErrorMessage);
        errors.Should().Contain(error =>
            error.ErrorMessage == StrategyStageResultEnvelopeValidationRules.ContentTypeErrorMessage);
    }

    /// <summary>Confirms the opaque result envelope survives MessagePack serialization unchanged.</summary>
    [Fact]
    public void Result_envelope_round_trips_through_message_pack()
    {
        var expected = CreateEnvelope(new byte[] { 1, 3, 5, 7 });

        var actual = RoundTrip(expected);

        actual.ResultId.Should().Be(expected.ResultId);
        actual.ResultType.Should().Be(expected.ResultType);
        actual.SchemaVersion.Should().Be(expected.SchemaVersion);
        actual.ContentType.Should().Be(expected.ContentType);
        actual.Payload.ToArray().Should().Equal(expected.Payload.ToArray());
        actual.PayloadSha256.Should().Be(expected.PayloadSha256);
        actual.MarketDataAsOfUtc.Should().Be(expected.MarketDataAsOfUtc);
        actual.ProducedAtUtc.Should().Be(expected.ProducedAtUtc);
        actual.HasValidPayloadSha256().Should().BeTrue();
    }

    /// <summary>Confirms stage reason-code arrays cannot be mutated through a shared reference.</summary>
    [Fact]
    public void Stage_state_defensively_copies_continuation_reason_codes()
    {
        string[] source = ["TREND_CONFIRMED", "VOLATILITY_ACCEPTED"];
        var state = new StrategyWorkflowStageState { ContinuationReasonCodes = source };

        source[0] = "SOURCE_MUTATED";
        var exposed = state.ContinuationReasonCodes;
        exposed[1] = "EXPOSED_MUTATED";

        state.ContinuationReasonCodes.Should().Equal("TREND_CONFIRMED", "VOLATILITY_ACCEPTED");
    }

    /// <summary>Confirms standard pipeline failure metadata survives MessagePack serialization.</summary>
    [Fact]
    public void Pipeline_failure_round_trips_through_message_pack()
    {
        var expected = new StrategyPipelineFailure
        {
            ErrorCode = 4101,
            ErrorMessage = "Regime discovery failed.",
            ErrorType = "RegimeDiscoveryUnavailable",
            ErrorData = "provider=development",
            FailedAtUtc = ProducedAtUtc
        };

        RoundTrip(expected).Should().Be(expected);
    }

    /// <summary>Confirms a complete nested workflow snapshot survives MessagePack serialization.</summary>
    [Fact]
    public void Workflow_state_round_trips_through_message_pack()
    {
        var expected = CreateWorkflowState();

        var actual = RoundTrip(expected);

        actual.EntityId.Should().Be(expected.EntityId);
        actual.WorkflowId.Should().Be(expected.WorkflowId);
        actual.TriggerEventId.Should().Be(expected.TriggerEventId);
        actual.CorrelationId.Should().Be(expected.CorrelationId);
        actual.WorkflowDefinitionVersion.Should().Be(expected.WorkflowDefinitionVersion);
        actual.Status.Should().Be(expected.Status);
        actual.Outcome.Should().Be(expected.Outcome);
        actual.CurrentStage.Should().Be(expected.CurrentStage);
        actual.WorkflowRevision.Should().Be(expected.WorkflowRevision);
        actual.StartedAtUtc.Should().Be(expected.StartedAtUtc);
        actual.TerminalAtUtc.Should().BeNull();
        actual.RegimeDiscovery.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Completed);
        actual.RegimeDiscovery.Result!.Payload.ToArray().Should()
            .Equal(expected.RegimeDiscovery.Result!.Payload.ToArray());
        actual.RegimeDiscovery.ContinuationReasonCodes.Should().Equal("REGIME_SUPPORTED");
        actual.MarketCondition.ProcessingStatus.Should().Be(StrategyActorProcessingStatus.Processing);
        actual.TradeSelection.Should().Be(new StrategyWorkflowStageState());
        actual.OrderComposition.Should().Be(new StrategyWorkflowStageState());
        actual.RiskManagement.Should().Be(new StrategyWorkflowStageState());
        actual.StopReasonCode.Should().BeEmpty();
    }

    /// <summary>Confirms the public workflow snapshot excludes pipeline-private state and the original trigger event.</summary>
    [Fact]
    public void Workflow_state_exposes_only_workflow_owned_snapshot_data()
    {
        var propertyNames = typeof(IntrinsicTimeStrategyWorkflowState).GetProperties()
            .Select(property => property.Name)
            .ToArray();

        propertyNames.Should().NotContain("TriggerEvent");
        propertyNames.Should().NotContain("PipelineState");
        propertyNames.Should().NotContain("PrivateState");
    }

    static StrategyStageResultEnvelope CreateEnvelope(
        ReadOnlyMemory<byte> payload,
        int maximumPayloadBytes = StrategyStageResultEnvelope.DefaultMaximumPayloadBytes)
        => StrategyStageResultEnvelope.Create(
            Guid.Parse("0198E212-3C00-7000-8000-000000000001"),
            "RegimeDiscovery.Result",
            1,
            payload,
            MarketDataAsOfUtc,
            ProducedAtUtc,
            maximumPayloadBytes: maximumPayloadBytes);

    static IntrinsicTimeStrategyWorkflowState CreateWorkflowState()
    {
        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(
            new FuturesItiSignalEntityId("ES-202609", new DateOnly(2026, 8, 25), TimeFrameType.Daily));
        var workflowId = new StrategyWorkflowId(Guid.Parse("0198E212-3C00-7000-8000-000000000002"));
        var regimeResult = CreateEnvelope(new byte[] { 0x92, 0x01, 0x02 });

        return new IntrinsicTimeStrategyWorkflowState
        {
            EntityId = entityId,
            WorkflowId = workflowId,
            TriggerEventId = Guid.Parse("0198E212-3C00-7000-8000-000000000003"),
            CorrelationId = Guid.Parse("0198E212-3C00-7000-8000-000000000004"),
            WorkflowDefinitionVersion = IntrinsicTimeStrategyWorkflowDefinition.Version,
            Status = StrategyWorkflowStatus.Running,
            Outcome = StrategyWorkflowOutcome.None,
            CurrentStage = StrategyWorkflowStage.MarketCondition,
            WorkflowRevision = 2,
            StartedAtUtc = MarketDataAsOfUtc,
            RegimeDiscovery = new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = StrategyWorkflowContinuationDecision.Proceed,
                StartedAtUtc = MarketDataAsOfUtc,
                CompletedAtUtc = ProducedAtUtc,
                Result = regimeResult,
                ContinuationRuleSetId = "RegimeDiscoveryContinuation",
                ContinuationRuleSetVersion = 1,
                ContinuationReasonCodes = ["REGIME_SUPPORTED"]
            },
            MarketCondition = new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing,
                StartedAtUtc = ProducedAtUtc
            }
        };
    }

    static T RoundTrip<T>(T value) => MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value));
}
