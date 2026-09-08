using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using System.Buffers;
using System.Reflection;
using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Validation;
using static TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection.TradeSelectionFunctionTests;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

public sealed class TradeSelectionAlignmentTests
{
    [Fact]
    public async Task Shared_compressed_serializer_round_trips_the_typed_completed_event()
    {
        var c = await TradeSelectionFixture.Command();
        var completed = (await new FunctionFixture(c).Execute()).Completed!;
        completed.Result.Payload.IsEmpty.Should().BeTrue();
        completed.Result.SelectionResult.Should().NotBeNull();
        var encoded = MessagePackBinarySerializer.Shared.Serialize(completed)!;
        var restored = MessagePackBinarySerializer.Shared.Deserialize<TradeSelectionFunctionCompletedEvent>(encoded)!;
        restored.Result.HasSameContent(completed.Result).Should().BeTrue();
        TradeSelectionContracts.ReadResult(restored.Result).Should().BeEquivalentTo(TradeSelectionContracts.ReadResult(completed.Result));
        MessagePackBinarySerializer.MeasureEncoded(completed).Should().Be(encoded.Length);
        MessagePackBinarySerializer.MeasureContent(completed).Should().BeGreaterThan(encoded.Length);
        var bytes = MessagePackBinarySerializer.SerializeHistoricalContent(completed.Result);
        var reader = new MessagePackReader(bytes);
        reader.ReadArrayHeader().Should().Be(11);
        for (var i = 0; i < 4; i++) reader.Skip();
        reader.ReadBytes()!.Value.Length.Should().Be(0);
        for (var i = 0; i < 5; i++) reader.Skip();
        reader.NextMessagePackType.Should().Be(MessagePackType.Array);
    }

    [Fact]
    public async Task Old_eight_field_envelopes_and_uncompressed_storage_events_remain_readable()
    {
        var c = await TradeSelectionFixture.Command();
        var completed = (await new FunctionFixture(c).Execute()).Completed!;
        var result = completed.Result.SelectionResult!;
        var payload = MessagePackSerializer.Serialize(result);
        var legacy = StrategyStageResultEnvelope.Create(result.ResultId, nameof(TradeSelectionResult), 1, payload,
            completed.Result.MarketDataAsOfUtc, completed.Result.ProducedAtUtc, maximumPayloadBytes: 524288);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(8);
        MessagePackSerializer.Serialize(ref writer, legacy.ResultId);
        writer.Write(legacy.ResultType); writer.Write(legacy.SchemaVersion); writer.Write(legacy.ContentType);
        writer.Write(payload); writer.Write(legacy.PayloadSha256);
        writer.Write(legacy.MarketDataAsOfUtc); writer.Write(legacy.ProducedAtUtc); writer.Flush();
        var old = MessagePackBinarySerializer.Shared.Deserialize<StrategyStageResultEnvelope>(buffer.WrittenMemory)!;
        TradeSelectionContracts.ReadResult(old).Should().BeEquivalentTo(result);
        TradeSelectionContracts.SameCompletion(completed, completed with { Result = old, EventId = 77 }).Should().BeTrue();
        TradeSelectionContracts.SameCompletion(completed, completed with { Result = old, CausationId = Guid.NewGuid() }).Should().BeFalse();
        var stored = MessagePackSerializer.Serialize(completed with { Result = old });
        var loaded = MessagePackBinarySerializer.Shared.Deserialize<TradeSelectionFunctionCompletedEvent>(stored)!;
        TradeSelectionContracts.ReadResult(loaded.Result).Should().BeEquivalentTo(result);
    }

    [Fact]
    public async Task Explicit_normalization_preserves_historical_request_and_binding_fingerprints()
    {
        var c = await TradeSelectionFixture.Command();
        var wire = MessagePackSerializer.Deserialize<ExecuteTradeSelectionPipelineCommand>(MessagePackSerializer.Serialize(c));
        c.Fingerprint().Should().Be(MarketConditionAssessmentHash.Compute(wire));
        var binding = c.SelectionBinding with { PayloadSha256 = "" };
        var historical = MessagePackSerializer.Deserialize<TradeSelectionBinding>(MessagePackSerializer.Serialize(binding));
        TradeSelectionContracts.BindingHash(c.SelectionBinding).Should().Be(MarketConditionAssessmentHash.Compute(historical).ToLowerInvariant());
        var received = MessagePackBinarySerializer.Shared.Deserialize<ExecuteTradeSelectionPipelineCommand>(MessagePackBinarySerializer.Shared.Serialize(c)!)!;
        received.Fingerprint().Should().Be(c.Fingerprint());
        TradeSelectionContracts.ValidateRequest(received);
    }

    [Fact]
    public async Task Typed_content_rejects_tampering_mixed_representations_and_protects_owned_collections()
    {
        var result = TradeSelectionEvaluator.Evaluate(await TradeSelectionFixture.Command());
        var envelope = StrategyStageResultEnvelope.CreateSelection(result);
        var original = envelope.PayloadSha256;
        var returned = envelope.SelectionResult!;
        var rows = returned.CandidateDecisions; rows[0] = rows[0] with { CandidateHash = "changed" };
        envelope.PayloadSha256.Should().Be(original);
        envelope.HasValidPayloadSha256().Should().BeTrue();
        (envelope with { SelectionResult = result with { SummaryText = "tampered" } }).HasValidPayloadSha256().Should().BeFalse();
        (envelope with { Payload = new byte[] { 1 } }).HasValidPayloadSha256().Should().BeFalse();
        (envelope with { AssessmentResult = result.DecisionContext.AssessmentResultEnvelope.AssessmentResult }).HasValidPayloadSha256().Should().BeFalse();
        (envelope with { ProducedAtUtc = envelope.ProducedAtUtc.AddSeconds(1) }).HasValidPayloadSha256().Should().BeFalse();
    }

    [Fact]
    public async Task Compressible_oversized_model_result_fails_before_projection_or_persistence()
    {
        var c = await TradeSelectionFixture.Command();
        var f = new FunctionFixture(c);
        var oversized = TradeSelectionEvaluator.Evaluate(c) with { SummaryText = new string('X', 300000) };
        MessagePackBinarySerializer.MeasureEncoded(oversized).Should().BeLessThan(262144);
        MessagePackBinarySerializer.MeasureContent(oversized).Should().BeGreaterThan(262144);
        var model = Substitute.For<ITradeSelectionCalculator>();
        model.Calculate(Arg.Any<ExecuteTradeSelectionPipelineCommand>()).Returns(oversized);
        f.Context.CalculationModel.Returns(model);
        var failed = await f.Execute();
        failed.Failed!.ReasonCode.Should().Be("TS.CONTRACT.PAYLOAD_SIZE");
        f.Order.Should().Equal("load"); f.Committed.Should().BeNull();
    }

    [Theory]
    [InlineData(FunctionFailureStage.Parsing, "TS.CONTRACT.INVALID")]
    [InlineData(FunctionFailureStage.Validation, "TS.CONTRACT.INVALID")]
    [InlineData(FunctionFailureStage.Loading, "TS.PERSISTENCE.FAILED")]
    [InlineData(FunctionFailureStage.Execution, "TS.CALCULATION.FAILED")]
    [InlineData(FunctionFailureStage.Projection, "TS.PROJECTION.FAILED")]
    [InlineData(FunctionFailureStage.Persistence, "TS.PERSISTENCE.FAILED")]
    public async Task Failure_map_preserves_lifecycle_reason_codes(FunctionFailureStage stage, string reason)
    {
        var c = await TradeSelectionFixture.Command();
        var failed = TradeSelectionFunctionActor.MapEvent(new(typeof(TradeSelectionFunctionFailedEvent), c,
            Exception: new InvalidOperationException("injected"), Stage: stage), new Clock(c.RequestedAtUtc)).Failed!;
        failed.ReasonCode.Should().Be(reason); failed.CorrelationId.Should().Be(c.CorrelationId);
    }

    [Fact]
    public void Event_map_rejects_unknown_types_and_missing_outcomes_and_handles_null_ingress()
    {
        FluentActions.Invoking(() => TradeSelectionFunctionActor.MapEvent(new(typeof(string), null), TimeProvider.System)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => TradeSelectionFunctionActor.MapEvent(new(typeof(TradeSelectionFunctionCompletedEvent), null), TimeProvider.System)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => TradeSelectionFunctionActor.MapEvent(new(typeof(TradeSelectionFunctionFailedEvent), null), TimeProvider.System)).Should().Throw<ArgumentException>();
        TradeSelectionFunctionActor.MapEvent(new(typeof(TradeSelectionFunctionFailedEvent), null, Exception: new ArgumentException()), TimeProvider.System).IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Mapped_validation_aggregates_required_fields_without_loading_state()
    {
        var c = await TradeSelectionFixture.Command();
        var invalid = c with { CommandId = Guid.Empty, CorrelationId = Guid.Empty, CausationId = Guid.Empty, WorkflowView = null!, TriggerEvent = null!, SelectionBinding = null!, RequestedAtUtc = default };
        var errors = new List<ValidationError>().ValidateSelectionFields(invalid).ValidateSelectionConsistency(invalid);
        errors.Count.Should().BeGreaterThanOrEqualTo(6);
        var f = new FunctionFixture(c);
        (await f.Execute(invalid)).IsFailed.Should().BeTrue(); f.Order.Should().BeEmpty();
    }

    [Fact]
    public async Task Model_failure_and_exact_deadline_cannot_project_or_append()
    {
        var c = await TradeSelectionFixture.Command(); var f = new FunctionFixture(c);
        var model = Substitute.For<ITradeSelectionCalculator>();
        model.Calculate(Arg.Any<ExecuteTradeSelectionPipelineCommand>()).Returns(_ => throw new InvalidOperationException("model fault"));
        f.Context.CalculationModel.Returns(model);
        (await f.Execute()).Failed!.ReasonCode.Should().Be("TS.CALCULATION.FAILED");
        f.Order.Should().Equal("load");
        model.Calculate(Arg.Any<ExecuteTradeSelectionPipelineCommand>()).Returns(_ => { f.Clock.Now = c.ExpiresAtUtc; return TradeSelectionEvaluator.Evaluate(c); });
        (await f.Execute()).Failed!.ReasonCode.Should().Be("TS.TIME.EXPIRED");
        f.Order.Should().Equal("load", "load"); f.Committed.Should().BeNull();
    }

    [Fact]
    public void Paging_reader_accepts_historical_and_standard_compressed_tokens()
    {
        var date = new DateOnly(2026, 9, 7);
        var state = Enumerable.Repeat((byte)1, 1000).ToArray();
        var old = Convert.ToBase64String(MessagePackSerializer.Serialize(new TradeSelectionPaging.Token(1, 1, 2, date, 50, state)));
        TradeSelectionPaging.Decode(1, 2, date, 50, old).Should().Equal(state);
        TradeSelectionPaging.Decode(1, 2, date, 50, TradeSelectionPaging.Encode(1, 2, date, 50, state)).Should().Equal(state);
    }
}

