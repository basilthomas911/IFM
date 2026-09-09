using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using System.Reflection;
using FluentAssertions;
using MessagePack;
using Newtonsoft.Json;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Validation;
using static TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition.MarketConditionAssessmentCalculationTests;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class MarketConditionAlignmentTests
{
    [Fact]
    public void Completion_map_emits_typed_content_in_the_outer_message()
    {
        var c = AssessmentFixture.Command();
        var snapshot = Snapshot(c).Seal();
        var result = Calculate(c, snapshot);
        var terminal = MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentCompletedEvent), c,
            new MarketConditionExecutionCompleted(result, snapshot)), new Clock(c.RequestedAtUtc));
        var completed = terminal.Completed!;
        completed.Result.Payload.IsEmpty.Should().BeTrue();
        completed.Result.AssessmentResult.Should().BeEquivalentTo(result);
        var bytes = MessagePackSerializer.Serialize(completed);
        var reader = new MessagePackReader(bytes);
        var resultKey = typeof(MarketConditionAssessmentCompletedEvent).GetProperty(nameof(completed.Result))!.GetCustomAttribute<KeyAttribute>()!.IntKey!.Value;
        reader.ReadArrayHeader().Should().BeGreaterThan(resultKey);
        for (var i=0; i<resultKey; i++) reader.Skip();
        reader.ReadArrayHeader().Should().Be(13);
        for (var i=0; i<4; i++) reader.Skip();
        reader.ReadBytes()!.Value.Length.Should().Be(0);
        for (var i=0; i<4; i++) reader.Skip();
        reader.NextMessagePackType.Should().Be(MessagePackType.Array);
        var restored = MessagePackSerializer.Deserialize<MarketConditionAssessmentCompletedEvent>(bytes);
        MarketConditionAssessmentContracts.ReadResult(restored.Result).Should().BeEquivalentTo(result);
        restored.Result.HasSameContent(completed.Result).Should().BeTrue();
    }

    [Theory]
    [InlineData(FunctionFailureStage.Parsing, MarketConditionFailureCategory.ContractInvalid)]
    [InlineData(FunctionFailureStage.Validation, MarketConditionFailureCategory.ContractInvalid)]
    [InlineData(FunctionFailureStage.Loading, MarketConditionFailureCategory.PersistenceFailed)]
    [InlineData(FunctionFailureStage.Execution, MarketConditionFailureCategory.CalculationFailed)]
    [InlineData(FunctionFailureStage.Projection, MarketConditionFailureCategory.ProjectionFailed)]
    [InlineData(FunctionFailureStage.Persistence, MarketConditionFailureCategory.PersistenceFailed)]
    public void Failure_map_preserves_lifecycle_categories(FunctionFailureStage stage, MarketConditionFailureCategory category)
    {
        var c = AssessmentFixture.Command();
        var result = MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentFailedEvent), c,
            Exception:new InvalidOperationException("injected"), Stage:stage), new Clock(c.RequestedAtUtc));
        result.Failed!.FailureCategory.Should().Be(category);
        result.Failed.WorkflowId.Should().Be(c.WorkflowId);
        result.Failed.CorrelationId.Should().Be(c.CorrelationId);
        result.Failed.ErrorData.Should().Be($"MC.ASSESSMENT.{category.ToString().ToUpperInvariant()}");
    }

    [Fact]
    public void Event_map_rejects_unknown_types_and_missing_outcomes()
    {
        var clock = TimeProvider.System;
        FluentActions.Invoking(() => MarketConditionFunctionActor.MapEvent(new(typeof(string), null), clock)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentCompletedEvent), AssessmentFixture.Command()), clock)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentFailedEvent), AssessmentFixture.Command()), clock)).Should().Throw<ArgumentException>();
        var failed=MarketConditionFunctionActor.MapEvent(new(typeof(MarketConditionAssessmentFailedEvent), null, Exception:new ArgumentException(), Stage:FunctionFailureStage.Parsing), clock);
        failed.IsFailed.Should().BeTrue();
    }

    [Fact]
    public void Validation_aggregates_missing_fields_and_null_nested_payloads()
    {
        var context = Substitute.For<IMarketConditionFunctionContext>();
        context.Logger.Returns(Microsoft.Extensions.Logging.Abstractions.NullLogger<MarketConditionFunctionActor>.Instance);
        var actor = new MarketConditionFunctionActor(context);
        var request = AssessmentFixture.Command() with
        {
            CommandId=Guid.Empty, InputWorkflowRevision=0, CorrelationId=Guid.Empty, CausationId=Guid.Empty,
            WorkflowView=null!, TriggerEvent=null!, ParameterSet=null!, RegimeResultEnvelope=null!,
            RequestedAtUtc=default, ExpiresAtUtc=default, ParameterPayloadSha256="bad", RegimePayloadSha256="bad"
        };
        var invocation = FluentActions.Invoking(() => typeof(MarketConditionFunctionActor)
            .GetMethod("ValidateAsync", BindingFlags.Instance|BindingFlags.NonPublic)!
            .Invoke(actor, [context, default(ActorThreadId), request, CancellationToken.None]))
            .Should().Throw<TargetInvocationException>().Which;
        invocation.InnerException.Should().BeOfType<CommandValidationException>();
        foreach(var name in new[]{"CommandId","InputWorkflowRevision","WorkflowView","TriggerEvent","CorrelationId","CausationId","RequestedAtUtc","ExpiresAtUtc","ParameterSet","ParameterPayloadSha256","RegimePayloadSha256","RegimeResultEnvelope"})
            invocation.InnerException!.Message.Should().Contain(name);
        new List<ValidationError>().ValidateAssessmentParameters(AssessmentFixture.Command().ParameterSet with {HorizonProfile=null!}).Should().NotBeEmpty();
        new List<ValidationError>().ValidateAssessmentParameters(AssessmentFixture.Command().ParameterSet with {Sources=[null!]}).Should().NotBeEmpty();
    }

    [Fact]
    public void Legacy_payload_and_typed_json_roundtrips_keep_valid_content()
    {
        var c=AssessmentFixture.Command(); var result=Calculate(c,Snapshot(c).Seal());
        var payload=MessagePackSerializer.Serialize(result);
        var legacy=StrategyStageResultEnvelope.Create(result.ResultId,nameof(MarketConditionAssessmentResult),1,payload,result.EvaluatedAtUtc,result.EvaluatedAtUtc);
        var oldWire = new LegacyAssessmentEnvelope(legacy.ResultId, legacy.ResultType, legacy.SchemaVersion, legacy.ContentType,
            legacy.Payload.ToArray(), legacy.PayloadSha256, legacy.MarketDataAsOfUtc, legacy.ProducedAtUtc);
        var restored=MessagePackSerializer.Deserialize<StrategyStageResultEnvelope>(MessagePackSerializer.Serialize(oldWire));
        restored.AssessmentResult.Should().BeNull();
        MarketConditionAssessmentContracts.ReadResult(restored).Should().BeEquivalentTo(result);
        restored.PayloadSha256.Should().Be(legacy.PayloadSha256);
        var typed=StrategyStageResultEnvelope.CreateAssessment(result);
        var json=JsonConvert.DeserializeObject<StrategyStageResultEnvelope>(JsonConvert.SerializeObject(typed))!;
        json.HasSameContent(typed).Should().BeTrue();
        MarketConditionAssessmentContracts.ReadResult(json).Should().BeEquivalentTo(result);
        var numeric=result with {Assessment=result.Assessment with {AssessmentConfidence=.50000m}};
        numeric.ContentFingerprint().Should().Be((numeric with {Assessment=numeric.Assessment with {AssessmentConfidence=.5m}}).ContentFingerprint());
    }

    [Fact]
    public void Typed_content_rejects_tampering_mixed_representations_and_oversized_content()
    {
        var c=AssessmentFixture.Command(); var result=Calculate(c,Snapshot(c).Seal());
        var envelope=StrategyStageResultEnvelope.CreateAssessment(result);
        (envelope with {AssessmentResult=result with {SummaryText="tampered"}}).HasValidPayloadSha256().Should().BeFalse();
        (envelope with {Payload=new byte[]{1}}).HasValidPayloadSha256().Should().BeFalse();
        (envelope with {RegimeResult=c.RegimeResultEnvelope.ReadRegimeResult()}).HasValidPayloadSha256().Should().BeFalse();
        (envelope with {ProducedAtUtc=c.RequestedAtUtc.AddDays(1)}).HasValidPayloadSha256().Should().BeFalse();
        var exposed=envelope.AssessmentResult!;
        exposed.Assessment.EvidenceItems[0]=exposed.Assessment.EvidenceItems[0] with {Reason="changed"};
        envelope.HasValidPayloadSha256().Should().BeTrue();
        envelope.AssessmentResult.Should().BeEquivalentTo(result);
        FluentActions.Invoking(()=>StrategyStageResultEnvelope.CreateAssessment(result with {SummaryText=new string('x',70000)}))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Typed_request_fingerprint_matches_the_historical_wire_normalization()
    {
        var c=AssessmentFixture.Command();
        c.Fingerprint().Should().Be(MarketConditionAssessmentHash.Compute(MessagePackSerializer.Deserialize<ExecuteMarketConditionAssessmentCommand>(MessagePackSerializer.Serialize(c))));
    }

    [MessagePackObject]
    public sealed record LegacyAssessmentEnvelope(
        [property:Key(0)] Guid ResultId, [property:Key(1)] string ResultType, [property:Key(2)] int SchemaVersion,
        [property:Key(3)] string ContentType, [property:Key(4)] byte[] Payload, [property:Key(5)] string PayloadSha256,
        [property:Key(6)] DateTime MarketDataAsOfUtc, [property:Key(7)] DateTime ProducedAtUtc);

    sealed class Clock(DateTime at):TimeProvider {public override DateTimeOffset GetUtcNow()=>new(at);}
}
