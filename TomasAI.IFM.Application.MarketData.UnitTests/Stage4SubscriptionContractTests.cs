using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class Stage4SubscriptionContractTests
{
    [Fact]
    public void Application_enablement_is_rejected_until_real_adapters_and_acceptance_are_complete()
    {
        new Stage4SubscriptionOptions().ValidateForApplicationStartup().Enabled.Should().BeFalse();
        Action enabled = () => new Stage4SubscriptionOptions { Enabled = true }.ValidateForApplicationStartup();
        enabled.Should().Throw<InvalidOperationException>().WithMessage("*Application enablement is prohibited*");
        Action badBound = () => new TickerLeasePolicy { MaximumOptions = 2_049 }.Validate();
        badBound.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Chain_is_canonical_and_does_not_retain_mutable_caller_collection()
    {
        var options = new[] { Ticker("call", true), Ticker("put", true) };
        var chain = Chain(options);
        var reverse = Chain(options.Reverse());
        options[0] = Ticker("other", true);
        chain.Should().Be(reverse);
        chain.ContractSetDigest.Should().Be(reverse.ContractSetDigest);
        chain.Options[0].ContractId.Should().Be("call");
        Chain(options).Should().NotBe(chain);
    }

    [Fact]
    public void Chain_rejects_duplicate_oversized_and_cross_dataset_universes()
    {
        var option = Ticker("call", true);
        Action duplicates = () => Chain([option, option]);
        Action oversized = () => Chain(Enumerable.Range(0, 513).Select(i => Ticker(i.ToString(), true)));
        Action foreign = () => Chain([new("databento", "OTHER", "call", "mbp-1", SubscriptionAssetKind.FuturesOption)]);
        duplicates.Should().Throw<ArgumentException>();
        oversized.Should().Throw<ArgumentException>();
        foreign.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ES")]
    [InlineData("ES\n")]
    public void Owner_and_contract_identities_reject_noncanonical_input(string value)
    {
        Action owner = () => new SubscriptionOwnerKey(value, new TickerStreamOwner("workflow", "1", "leg"));
        Action ticker = () => Ticker(value);
        owner.Should().Throw<ArgumentException>();
        ticker.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Typed_default_is_disabled_and_never_implies_active_or_durable_acceptance()
    {
        var operation = Guid.NewGuid();
        var result = SubscriptionLeaseResult.Disabled(operation);
        result.OperationId.Should().Be(operation);
        result.Code.Should().Be(SubscriptionResultCode.Disabled);
        result.Lease.Should().BeNull();
        result.RealizedRevision.Should().Be(0);
    }

    static SubscriptionTickerKey Ticker(string contract, bool option = false) => new(
        "databento", "GLBX.MDP3", contract, "mbp-1",
        option ? SubscriptionAssetKind.FuturesOption : SubscriptionAssetKind.Futures);
    static SubscriptionChainKey Chain(IEnumerable<SubscriptionTickerKey> options) => new(
        Ticker("ES"), new(2026, 9, 18), new(2026, 9, 4), options);
}
