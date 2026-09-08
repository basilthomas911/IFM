using System.Buffers;
using System.Reflection;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Framework.Serialization;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
public sealed class CompositionContractTests
{
    [Fact]
    public void Wire_manifest_has_explicit_contiguous_unique_keys_for_every_owned_dto()
    {
        var types = typeof(OrderCompositionResult).Assembly.GetTypes().Where(t => t.Namespace?.StartsWith(typeof(OrderCompositionResult).Namespace!, StringComparison.Ordinal) == true
            && t.GetCustomAttribute<MessagePackObjectAttribute>() is not null).ToArray();
        Assert.True(types.Length >= 25);
        foreach (var t in types)
        {
            var keys = t.GetProperties().Select(p => p.GetCustomAttribute<KeyAttribute>()?.IntKey).Where(x => x is not null).Select(x => x!.Value).Order().ToArray();
            Assert.Equal(Enumerable.Range(0, keys.Length), keys);
        }
        Assert.Equal(21, Keys(typeof(ExecuteOrderCompositionPipelineCommand)).Length);
        Assert.Equal("SchemaVersion|ResultId|WorkflowId|EntityId|InvocationId|InputWorkflowRevision|InputSha256|EvaluatedAtUtc|ProducedAtUtc|TargetHorizon|Outcome|Candidate|DecisionContext|ResolvedParameters|CandidateCounts|CandidateDiagnostics|Reasons|ValidUntilUtc|SummaryText|Ranking", string.Join("|", Keys(typeof(OrderCompositionResult))));
        Assert.Equal("InstrumentId|RawSymbol|UnderlyingInstrumentId|InstrumentClass|Side|Ratio|Right|Strike|ExpirationUtc|Multiplier|TickRuleId|Quote|Valuation|DefinitionHash", string.Join("|", Keys(typeof(CompositionLeg))));
        Assert.Equal(31, Keys(typeof(CompositionCandidate)).Length);
        Assert.Equal(25, Keys(typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.Pricing.OptionPricingConvention)).Length);
    }
    [Fact]
    public async Task Domain_adapter_preserves_all_pricing_evidence_and_shared_transport_preserves_request_hash()
    {
        var c = await CompositionFixture.Command("BullCallDebit");
        var round = CompositionSnapshotAdapter.From(CompositionSnapshotAdapter.To(c.MarketSnapshot));
        Assert.Equal(CompositionHash.Compute(c.MarketSnapshot), CompositionHash.Compute(round));
        var encoded = MessagePackBinarySerializer.Shared.Serialize(c);
        var restored = MessagePackBinarySerializer.Shared.Deserialize<ExecuteOrderCompositionPipelineCommand>(encoded);
        Assert.Equal(c.InputSha256, restored.Fingerprint());
        Assert.True(MessagePackBinarySerializer.MeasureContent(c) <= 1048576);
        var reservation = c.Reservation; reservation.Trades[0] = reservation.Trades[0] with { TradeId = 999 };
        Assert.Equal(8001, c.Reservation.Trades[0].TradeId); Assert.Equal(c.InputSha256, c.Fingerprint());
    }
    [Fact]
    public void Eleven_field_historical_envelopes_remain_readable_but_cannot_be_new_composer_results()
    {
        var all = MessagePackBinarySerializer.SerializeHistoricalContent(new StrategyStageResultEnvelope());
        var reader = new MessagePackReader(all); Assert.Equal(12, reader.ReadArrayHeader());
        var buffer = new ArrayBufferWriter<byte>(); var writer = new MessagePackWriter(buffer); writer.WriteArrayHeader(11);
        for (int i = 0; i < 11; i++) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        var old = MessagePackBinarySerializer.Shared.Deserialize<StrategyStageResultEnvelope>(buffer.WrittenMemory.ToArray());
        Assert.Null(old.CompositionResult); Assert.ThrowsAny<Exception>(() => old.ReadCompositionResult());
    }
    [Fact]
    public void Semantic_hash_excludes_process_local_non_contract_diagnostics()
    {
        Assert.Equal(CompositionHash.Compute(new Diagnostic { Value = 12, LocalUser = "host-a" }),
            CompositionHash.Compute(new Diagnostic { Value = 12, LocalUser = "host-b" }));
        Assert.NotEqual(CompositionHash.Compute(new Diagnostic { Value = 12 }), CompositionHash.Compute(new Diagnostic { Value = 13 }));
    }
    sealed record Diagnostic { public int Value { get; init; } [IgnoreMember] public string LocalUser { get; init; } = ""; }
    static string[] Keys(Type type) => type.GetProperties().Select(p => (Property: p, Key: p.GetCustomAttribute<KeyAttribute>()?.IntKey))
        .Where(x => x.Key is not null).OrderBy(x => x.Key).Select(x => x.Property.Name).ToArray();
}
