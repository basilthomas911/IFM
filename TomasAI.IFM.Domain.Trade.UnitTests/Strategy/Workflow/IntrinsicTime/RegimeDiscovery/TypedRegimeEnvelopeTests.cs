using System.Reflection;
using FluentAssertions;
using MessagePack;
using Newtonsoft.Json;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Projector;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

public sealed class TypedRegimeEnvelopeTests
{
    [Fact]
    public void Outer_message_contains_a_typed_object_and_no_inner_result_bytes()
    {
        var result = Sample();
        var completed = new RegimeDiscoveryPipelineCompletedEvent { Result = StrategyStageResultEnvelope.CreateRegime(result) };
        var bytes = MessagePackSerializer.Serialize(completed);
        var reader = new MessagePackReader(bytes);
        reader.ReadArrayHeader().Should().BeGreaterThan(13);
        for (var i = 0; i < 13; i++) reader.Skip();
        reader.ReadArrayHeader().Should().Be(10);
        for (var i = 0; i < 4; i++) reader.Skip();
        reader.ReadBytes()!.Value.Length.Should().Be(0);
        for (var i = 0; i < 3; i++) reader.Skip();
        reader.NextMessagePackType.Should().Be(MessagePackType.Array);
        var restored = MessagePackSerializer.Deserialize<RegimeDiscoveryPipelineCompletedEvent>(bytes);
        restored.Result.ReadRegimeResult().Should().BeEquivalentTo(result);
        restored.Result.HasValidPayloadSha256().Should().BeTrue();
    }

    [Fact]
    public void Old_eight_field_envelopes_remain_readable_and_keep_their_original_hash()
    {
        var result = Sample();
        var payload = MessagePackSerializer.Serialize(result);
        var legacy = new LegacyEnvelope(result.ResultId, nameof(RegimeDiscoveryResult), result.SchemaVersion,
            "application/x-msgpack", payload, StrategyStageResultEnvelope.ComputePayloadSha256(payload),
            result.MarketDataAsOfUtc, result.ProducedAtUtc);
        var restored = MessagePackSerializer.Deserialize<StrategyStageResultEnvelope>(MessagePackSerializer.Serialize(legacy));
        restored.RegimeResult.Should().BeNull();
        restored.PayloadSha256.Should().Be(legacy.PayloadSha256);
        Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentHash.Serialize(restored)
            .Should().Be(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment.MarketConditionAssessmentHash.Serialize(legacy));
        restored.ReadRegimeResult().Should().BeEquivalentTo(result);
        new StrategyStageResultEnvelopeValidationRules().Execute(restored).Should().BeEmpty();
    }

    [Fact]
    public void Json_storage_roundtrip_preserves_content_identity_and_decimal_meaning()
    {
        var result = Sample();
        var envelope = StrategyStageResultEnvelope.CreateRegime(result);
        var restored = JsonConvert.DeserializeObject<StrategyStageResultEnvelope>(JsonConvert.SerializeObject(envelope))!;
        restored.HasSameContent(envelope).Should().BeTrue();
        var normalized = result with { OverallConfidence = 1.00m };
        RegimeDiscoveryResultContent.Fingerprint(normalized).Hash.Should().Be(RegimeDiscoveryResultContent.Fingerprint(result).Hash);
    }

    [Fact]
    public void Defensive_copies_protect_nested_arrays_and_changed_content_is_rejected()
    {
        var result = Sample();
        var envelope = StrategyStageResultEnvelope.CreateRegime(result);
        var expected = envelope.ReadRegimeResult();
        result.Decision.Restrictions[0] = RegimeRestriction.NoNewTrade;
        result.Trend.Evidence[0] = result.Trend.Evidence[0] with { Value = -999 };
        var exposed = envelope.RegimeResult!;
        exposed.Reasons[0] = exposed.Reasons[0] with { Code = "changed" };
        envelope.ReadRegimeResult().Should().BeEquivalentTo(expected);
        var altered = envelope with { RegimeResult = expected with { SummaryText = "changed" } };
        altered.HasValidPayloadSha256().Should().BeFalse();
        (envelope with { Payload = new byte[] { 1 } }).HasValidPayloadSha256().Should().BeFalse();
        (envelope with { ResultId = Guid.NewGuid() }).HasValidPayloadSha256().Should().BeFalse();
        Action oversized = () => StrategyStageResultEnvelope.CreateRegime(expected with { SummaryText = new string('x', 65536) });
        oversized.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Projector_encodes_only_at_storage_boundary_with_a_valid_blob_digest(bool legacy)
    {
        var result = Sample();
        var envelope = legacy
            ? StrategyStageResultEnvelope.Create(result.ResultId, nameof(RegimeDiscoveryResult), result.SchemaVersion,
                MessagePackSerializer.Serialize(result), result.MarketDataAsOfUtc, result.ProducedAtUtc)
            : StrategyStageResultEnvelope.CreateRegime(result);
        var factory = Substitute.For<IDbContextFactory>();
        RegimeDiscoveryReadModel? row = null;
        factory.TradeDb.UpsertRegimeDiscoveryAsync(Arg.Any<RegimeDiscoveryReadModel>(), Arg.Any<CancellationToken>())
            .Returns(call => { row = call.Arg<RegimeDiscoveryReadModel>(); return Task.CompletedTask; });
        await new RegimeDiscoveryFunctionProjector(factory).ProjectAsync(new() { Result = envelope });
        row.Should().NotBeNull();
        MessagePackSerializer.Deserialize<RegimeDiscoveryResult>(row!.ResultPayload).Should().BeEquivalentTo(result);
        row.ResultPayloadSha256.Should().Be(StrategyStageResultEnvelope.ComputePayloadSha256(row.ResultPayload.Span));
    }

    public static IEnumerable<object[]> KeyedFields() => Paths(typeof(RegimeDiscoveryResult)).Select(path => new object[] { path });

    [Theory, MemberData(nameof(KeyedFields))]
    public void Fingerprint_covers_every_serialized_field_including_nested_collections(string path)
    {
        var result = Sample();
        var original = RegimeDiscoveryResultContent.Fingerprint(result).Hash;
        Change(result, path.Split('.'), 0);
        RegimeDiscoveryResultContent.Fingerprint(result).Hash.Should().NotBe(original, path + " is serialized content");
    }

    static RegimeDiscoveryResult Sample() => (RegimeDiscoveryResult)Sample(typeof(RegimeDiscoveryResult));
    static object Sample(Type type)
    {
        if (type == typeof(string)) return "alpha";
        if (type == typeof(Guid)) return Guid.Parse("11111111-1111-1111-1111-111111111111");
        if (type == typeof(DateTime)) return new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        if (type == typeof(DateOnly)) return new DateOnly(2026, 9, 7);
        if (type == typeof(bool)) return true;
        if (type.IsEnum) return Enum.ToObject(type, 1);
        if (type.IsPrimitive || type == typeof(decimal)) return Convert.ChangeType(1, type);
        if (type.IsArray)
        {
            var array = Array.CreateInstance(type.GetElementType()!, 1);
            array.SetValue(Sample(type.GetElementType()!), 0);
            return array;
        }
        var value = Activator.CreateInstance(type)!;
        foreach (var property in Properties(type)) property.SetValue(value, Sample(property.PropertyType));
        return value;
    }
    static PropertyInfo[] Properties(Type type) => type.GetProperties().Where(p => p.GetCustomAttribute<KeyAttribute>() is not null).ToArray();
    static IEnumerable<string> Paths(Type type)
    {
        foreach (var property in Properties(type))
        {
            yield return property.Name;
            var child = property.PropertyType;
            if (child.IsArray)
            {
                yield return property.Name + ".0";
                foreach (var nested in Paths(child.GetElementType()!)) yield return property.Name + ".0." + nested;
            }
            else foreach (var nested in Paths(child)) yield return property.Name + "." + nested;
        }
    }
    static object? Change(object? value, string[] path, int depth)
    {
        if (depth == path.Length)
        {
            return value switch
            {
                string s => s + "changed", Guid => Guid.NewGuid(), DateTime d => d.AddTicks(1), DateOnly d => d.AddDays(1),
                bool b => !b, decimal d => d + 1, Enum e => Enum.ToObject(e.GetType(), Convert.ToInt64(e) + 1),
                _ when value!.GetType().IsPrimitive => Convert.ChangeType(2, value.GetType()),
                _ when value!.GetType().IsValueType => Activator.CreateInstance(value.GetType()),
                _ => null
            };
        }
        if (value is Array array) array.SetValue(Change(array.GetValue(0), path, depth + 1), 0);
        else
        {
            var property = value!.GetType().GetProperty(path[depth])!;
            property.SetValue(value, Change(property.GetValue(value), path, depth + 1));
        }
        return value;
    }

    [MessagePackObject]
    public sealed record LegacyEnvelope(
        [property: Key(0)] Guid ResultId, [property: Key(1)] string ResultType, [property: Key(2)] int SchemaVersion,
        [property: Key(3)] string ContentType, [property: Key(4)] byte[] Payload, [property: Key(5)] string PayloadSha256,
        [property: Key(6)] DateTime MarketDataAsOfUtc, [property: Key(7)] DateTime ProducedAtUtc);
}
