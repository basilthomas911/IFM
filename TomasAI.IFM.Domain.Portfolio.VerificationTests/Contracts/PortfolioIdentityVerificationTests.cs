using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Contracts;

public sealed class PortfolioIdentityVerificationTests
{
    [Fact]
    [Trait("Gate", "PF-01")]
    [Trait("Category", "Portfolio")]
    public void All_business_identities_survive_production_serialization_unchanged()
    {
        var serializer = new MessagePackBinarySerializer();

        RoundTrip(serializer, new PortfolioId(101)).Should().Be(new PortfolioId(101));
        RoundTrip(serializer, new PortfolioFundId(101, 205)).Should().Be(new PortfolioFundId(101, 205));
        RoundTrip(serializer, new PortfolioFundOrderId(101, 205, 3001)).Should().Be(new PortfolioFundOrderId(101, 205, 3001));
        RoundTrip(serializer, new PortfolioFundOrderTradeId(101, 205, 3001, 4001))
            .Should().Be(new PortfolioFundOrderTradeId(101, 205, 3001, 4001));
    }

    private static T RoundTrip<T>(MessagePackBinarySerializer serializer, T source) where T : class =>
        serializer.Deserialize<T>(serializer.Serialize(source)!)!;
}
