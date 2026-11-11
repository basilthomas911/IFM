using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionReferenceIdentityTests
{
    static FuturesOptionContractReadModel Option(uint instrument, string underlying = "ES20261218", uint underlyingInstrument = 10252) =>
        new("ES20261218C7500", "ES option", "ES", "raw", "FOP", "USD", "XCME", "50", new DateOnly(2026, 12, 18), 7500, "Call")
        { Dataset = "GLBX.MDP3", PublisherId = 1, InstrumentId = instrument, StrikePriceDecimal = 7500,
            UnderlyingContractId = underlying, UnderlyingInstrumentId = underlyingInstrument, UnderlyingPublisherId = 1 };

    [Fact]
    public void Distinct_underlyings_keep_recorded_identity_and_receive_a_separate_provider_qualified_identity()
    {
        var december = Option(42739786);
        var march = Option(42325531, "ES20270319", 42140860);
        var stored = OptionReferenceIdentity.Serialize(december);
        var bindings = new Dictionary<string, string> { ["ifm:" + december.ContractId] = stored, [OptionReferenceIdentity.ProviderKey(december)] = stored };
        var result = OptionReferenceIdentity.Resolve([december, march], bindings);
        Assert.Equal(december, result[0]);
        Assert.Equal("ES20261218C7500~1-42325531", result[1].ContractId);
        Assert.Equal("ES20270319", result[1].UnderlyingContractId);
        Assert.Equal(2, result.Select(x => x.ContractId).Distinct().Count());
        Assert.Equal(stored, bindings["ifm:" + december.ContractId]);
    }

    [Fact]
    public void Unclaimed_ambiguous_names_are_resolved_independently_of_input_order()
    {
        var a = Option(10); var b = Option(20, "ES20270319", 200);
        var first = OptionReferenceIdentity.Resolve([a, b], new Dictionary<string, string>()).ToDictionary(x => x.InstrumentId, x => x.ContractId);
        var reverse = OptionReferenceIdentity.Resolve([b, a], new Dictionary<string, string>()).ToDictionary(x => x.InstrumentId, x => x.ContractId);
        Assert.Equal(first[10], reverse[10]); Assert.Equal(first[20], reverse[20]);
        Assert.NotEqual(first[10], first[20]);
        Assert.All(first.Values, id => Assert.Contains('~', id));
    }

    [Fact]
    public void Unambiguous_unclaimed_option_keeps_its_short_identity()
    {
        var value = Option(10);
        Assert.Equal(value, Assert.Single(OptionReferenceIdentity.Resolve([value], new Dictionary<string, string>())));
    }

    [Fact]
    public void Existing_provider_identity_cannot_be_rebound_to_a_different_underlying()
    {
        var original = Option(10);
        var bindings = new Dictionary<string, string> { [OptionReferenceIdentity.ProviderKey(original)] = OptionReferenceIdentity.Serialize(original) };
        Assert.Throws<InvalidDataException>(() => OptionReferenceIdentity.Resolve([original with { UnderlyingContractId = "ES20270319" }], bindings));
    }

    [Fact]
    public void Previously_qualified_identity_remains_stable_on_every_refresh()
    {
        var candidate = Option(10); var saved = candidate with { ContractId = "ES20261218C7500~1-10" };
        var identity = OptionReferenceIdentity.Serialize(saved);
        var bindings = new Dictionary<string, string> { [OptionReferenceIdentity.ProviderKey(saved)] = identity, ["ifm:" + saved.ContractId] = identity };
        Assert.Equal(saved, Assert.Single(OptionReferenceIdentity.Resolve([candidate], bindings)));
    }

    [Fact]
    public void Expired_provider_id_can_be_reused_only_with_a_later_authoritative_definition()
    {
        var old = Option(10) with { ContractId = "ES20261008C7475", ContractMonth = new DateOnly(2026, 10, 8), StrikePrice = 7475, StrikePriceDecimal = 7475 };
        var current = Option(10) with { DefinitionTimestampUtc = new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero),
            ExpirationUtc = new DateTimeOffset(2026, 12, 18, 21, 0, 0, TimeSpan.Zero),
            DefinitionDigest = new string('a', 64), RawDefinitionReference = "verified-snapshot/10" };
        var originalBinding = OptionReferenceIdentity.Serialize(old);
        var bindings = new Dictionary<string, string> { [OptionReferenceIdentity.LegacyProviderKey(old)] = originalBinding };
        Assert.Equal(current, Assert.Single(OptionReferenceIdentity.Resolve([current], bindings)));
        Assert.Equal(originalBinding, bindings[OptionReferenceIdentity.LegacyProviderKey(old)]);
        Assert.NotEqual(OptionReferenceIdentity.ProviderKey(old), OptionReferenceIdentity.ProviderKey(current));
        Assert.Throws<InvalidDataException>(() => OptionReferenceIdentity.Resolve([current with { DefinitionTimestampUtc = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero) }], bindings));
        Assert.Throws<InvalidDataException>(() => OptionReferenceIdentity.Resolve([current with { DefinitionDigest = null }], bindings));
    }

    [Fact]
    public void Legacy_same_lifecycle_cannot_change_even_when_definition_is_newer()
    {
        var old = Option(10);
        var bindings = new Dictionary<string, string> { [OptionReferenceIdentity.LegacyProviderKey(old)] = OptionReferenceIdentity.Serialize(old) };
        Assert.Throws<InvalidDataException>(() => OptionReferenceIdentity.Resolve([old with { UnderlyingContractId = "ES20270319" }], bindings));
    }

    [Theory]
    [InlineData("ES20261218C7500~1-42325531", 7500, OptionType.Call)]
    [InlineData("ES20261218P7500.25~1-42325531", 7500.25, OptionType.Put)]
    public void Qualified_identity_preserves_legacy_parsed_fields(string id, decimal strike, OptionType right)
    {
        var value = new FuturesOptionContractId(id);
        Assert.Equal("ES", value.Symbol); Assert.Equal(strike, value.StrikePrice); Assert.Equal(right, value.OptionType);
        Assert.Equal(new DateTime(2026, 12, 18), value.MaturityDate); Assert.Equal(id, value.Format());
    }

    [Theory]
    [InlineData("ES20261218C7500~0-1")]
    [InlineData("ES20261218C7500~1-0")]
    [InlineData("ES20261218C7500~1-1~2-2")]
    public void Malformed_provider_suffix_is_rejected(string id) => Assert.Throws<InvalidOperationException>(() => new FuturesOptionContractId(id));
}
