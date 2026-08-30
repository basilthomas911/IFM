using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Contracts;

public sealed class PortfolioContractTests
{
    public static TheoryData<object, string> ValidIdentities => new()
    {
        { new PortfolioId(101), "101" },
        { new PortfolioFundId(101, 205), "101.205" },
        { new PortfolioFundOrderId(101, 205, 3001), "101.205.3001" },
        { new PortfolioFundOrderTradeId(101, 205, 3001, 4001), "101.205.3001.4001" },
    };

    [Theory]
    [MemberData(nameof(ValidIdentities))]
    [Trait("Gate", "PF-01")]
    [Trait("Category", "Portfolio")]
    public void Identity_contracts_round_trip_with_exact_format(object identity, string expectedFormat)
    {
        var runtimeType = identity.GetType();
        var bytes = MessagePackSerializer.Serialize(runtimeType, identity);
        var copy = MessagePackSerializer.Deserialize(runtimeType, bytes);

        copy.Should().Be(identity);
        ((TomasAI.IFM.Shared.EventModelActor.Contracts.IActorEntityId)copy).Format()
            .Should().Be(expectedFormat);
    }

    [Fact]
    [Trait("Gate", "PF-01")]
    [Trait("Category", "Portfolio")]
    public void Enum_numeric_contract_is_append_only()
    {
        ((int)PortfolioOperatingState.Retired).Should().Be(6);
        ((int)FundOperatingState.Retired).Should().Be(5);
        ((int)FundCapacityState.ReduceOnly).Should().Be(4);
        ((int)FundCompositionState.ExecutionFailed).Should().Be(15);
        ((int)CompositionOrigin.ApprovedImport).Should().Be(3);
    }

    [Fact]
    [Trait("Gate", "PF-01")]
    [Trait("Category", "Portfolio")]
    public void Reserved_error_codes_are_inside_the_audited_range()
    {
        PortfolioErrorCodes.IsReserved(PortfolioErrorCodes.InvalidIdentity).Should().BeTrue();
        PortfolioErrorCodes.IsReserved(PortfolioErrorCodes.ExecutionBoundaryViolation).Should().BeTrue();
        PortfolioErrorCodes.IsReserved(33999).Should().BeFalse();
        PortfolioErrorCodes.IsReserved(34300).Should().BeFalse();
    }
}
