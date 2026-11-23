using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using Xunit;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.OrderComposition;

public sealed class PortfolioEntryApprovalFingerprintTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(70000)]
    [InlineData(300000)]
    public void Streaming_preserves_the_exact_v1_digest_across_buffer_boundaries(int size)
    {
        var value = new PortfolioEntryApproval
        {
            DecisionSessionId = Guid.Parse("c7d62214-e727-4c6b-8874-8fad39441936"),
            ProducedAtUtc = new(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc),
            ValidUntilUtc = new(2026, 11, 20, 15, 0, 2, DateTimeKind.Utc),
            Funds = [new(1, false, new string('x', size), null)]
        };
        using var old = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        old.AppendData("IFM.PortfolioEntryApproval.v1\0"u8);
        old.AppendData(MessagePackSerializer.Serialize(value));
        var expected = Convert.ToHexString(old.GetHashAndReset());

        var sealedValue = value.Seal();

        Assert.Equal(expected, sealedValue.PayloadSha256);
        Assert.True(sealedValue.HasValidFingerprint());
        Assert.True(MessagePackSerializer.Deserialize<PortfolioEntryApproval>(MessagePackSerializer.Serialize(sealedValue)).HasValidFingerprint());
    }

    [Fact]
    public void Oversized_evidence_still_has_a_hard_bound()
    {
        var value = new PortfolioEntryApproval { Funds = [new(1, false, new string('x', 1024 * 1024 + 1), null)] };
        var error = Assert.ThrowsAny<Exception>(() => value.Seal());
        Assert.Contains("ENTRY.APPROVAL.TOO_LARGE", error.ToString());
    }
}
