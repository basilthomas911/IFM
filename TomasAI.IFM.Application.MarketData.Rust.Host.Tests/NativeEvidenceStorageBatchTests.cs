using MessagePack;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Persistence;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Tests;

public sealed class NativeEvidenceStorageBatchTests
{
    [Fact]
    public async Task Invalid_handoff_isolated_and_valid_receipts_keep_their_exact_identity()
    {
        var writer = new CapturingWriter();
        var storage = new NativeEvidenceStorage(writer);
        var first = Request(1);
        var invalid = Request(2) with { Digest = new string('f', 64) };
        var duplicate = Request(3);
        var outcomes = await storage.WriteBatchAsync([first, invalid, duplicate], CancellationToken.None);

        Assert.Equal(new long[] { 1, 3 }, writer.Received.Select(item => item.Source.Sequence));
        Assert.Equal(new[] { first.Receipt, invalid.Receipt, duplicate.Receipt }, outcomes.Select(item => item.Receipt.Receipt));
        Assert.Equal(new[] { first.Identity, invalid.Identity, duplicate.Identity }, outcomes.Select(item => item.Receipt.Identity));
        Assert.Equal(new[] { first.Digest, invalid.Digest, duplicate.Digest }, outcomes.Select(item => item.Receipt.Digest));
        Assert.True(outcomes[0].Receipt.Persisted);
        Assert.IsType<InvalidDataException>(outcomes[1].Error);
        Assert.False(outcomes[1].Receipt.Persisted);
        Assert.Equal("OPTION_EVIDENCE_ALREADY_SEEN", outcomes[2].Receipt.Error);
        Assert.Null(outcomes[2].Error);
        Assert.False(outcomes[2].Receipt.Persisted);
    }

    static NativeEvidenceWrite Request(ulong receipt)
    {
        var source = new OptionTradeSource("GLBX.MDP3", 1, 100, "OPTION-BATCH", new(2026, 11, 5),
            12.125m, 1, (long)receipt, 1000, 1001, Guid.NewGuid(), "raw-option");
        var evidence = new OptionTradeEvidence(source, null, null, new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero),
            null, new OptionPricingFailure("FixtureFailure", "Quote", source.ContractId, "Isolated persistence test"));
        return new(receipt, source.Identity, source.SourceDigest, MessagePackSerializer.Serialize(evidence));
    }

    sealed class CapturingWriter : IOptionTradeEvidenceWriter
    {
        public IReadOnlyList<OptionTradeEvidence> Received { get; private set; } = [];

        public ValueTask WriteAsync(OptionTradeEvidence evidence, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Expected grouped storage.");

        public ValueTask<bool[]> WriteFirstBatchAsync(IReadOnlyList<OptionTradeEvidence> evidence, CancellationToken cancellationToken)
        {
            Received = evidence;
            return ValueTask.FromResult(new[] { true, false });
        }
    }
}
