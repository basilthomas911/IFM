using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Portfolio;

public sealed class MarketDecisionEvidenceTests
{
    [Fact]
    public void Large_frozen_inputs_use_lossless_compression_and_legacy_inputs_remain_readable()
    {
        var inputs = Enumerable.Range(0, 1000).Select(_ => "Exact immutable upstream evidence").ToArray();
        var bytes = MessagePackSerializer.Serialize(inputs);
        var evidence = MarketDecisionEvidence.Capture(inputs, DateTime.UtcNow);
        evidence.Encoding.Should().Be("messagepack-lz4/base64");
        Convert.FromBase64String(evidence.Payload).Length.Should().BeLessThan(bytes.Length / 2);
        evidence.Read<string[]>().Should().Equal(inputs);
        var legacy = evidence with { Encoding = "messagepack/base64", Payload = Convert.ToBase64String(bytes),
            PayloadSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) };
        legacy.Read<string[]>().Should().Equal(inputs);
        (evidence with { Payload = legacy.Payload }).IsValid.Should().BeFalse();
        Assert.Throws<InvalidDataException>(() => evidence.Read<int[]>());
    }

    [Fact]
    public void Exact_inputs_survive_transport_and_are_independent_of_later_market_updates()
    {
        var original = new FuturesEodDataV2ReadModel { ContractId = "ES", ValueDate = new(2026, 10, 5), ClosePrice = 6400.25m };
        var evidence = MarketDecisionEvidence.Capture(original, DateTime.UtcNow);
        var later = original with { ClosePrice = 6500m };
        var restored = MessagePackSerializer.Deserialize<MarketDecisionEvidence>(MessagePackSerializer.Serialize(evidence));
        restored.IsValid.Should().BeTrue();
        restored.Read<FuturesEodDataV2ReadModel>().Should().Be(original);
        restored.Read<FuturesEodDataV2ReadModel>().Should().NotBe(later);
        (restored with { Payload = Convert.ToBase64String(new byte[] { 1, 2, 3 }) }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Null_evidence_preserves_legacy_json_and_manual_evidence_detects_tampering()
    {
        var order = new TomasAI.IFM.Domain.Trade.Shared.TradeOrderDefinition();
        System.Text.Json.JsonSerializer.Serialize(order).Should().NotContain("DecisionEvidence");
        var evidence = MarketDecisionEvidence.CaptureJson("BrokerTradeScreen/v1", new { Bid = 10m, Ask = 11m, Quantity = 2 }, DateTime.UtcNow);
        evidence.IsValid.Should().BeTrue();
        (evidence with { Payload = evidence.Payload.Replace("10", "12") }).IsValid.Should().BeFalse();
    }
}
