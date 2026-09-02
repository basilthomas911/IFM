using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

/// <summary>Executable minimum behavior specification for FCR operational decisions.</summary>
public sealed class FuturesRolloverBddTests
{
    [Fact]
    public void GivenAnEsAssignment_WhenSelected_ThenItIsTheOnlyOnTheRunRolloverContract()
    {
        var assignment = new[] { Contract("ES", "ES20261218", true, true, new(2026, 12, 18)) };

        assignment.Should().ContainSingle(contract => contract.OnTheRun && contract.Rollover);
    }

    [Fact]
    public void GivenAVxPair_WhenSelected_ThenFrontIsOnTheRunAndBothAreInRolloverSet()
    {
        var assignment = new[]
        {
            Contract("VX", "VX20261021", true, true, new(2026, 10, 21)),
            Contract("VX", "VX20261118", false, true, new(2026, 11, 18))
        };

        assignment.Should().OnlyContain(contract => contract.Rollover);
        assignment.Should().ContainSingle(contract => contract.OnTheRun)
            .Which.ContractId.Should().Be("VX20261021");
    }

    [Fact]
    public void GivenAProviderCatalogCandidate_WhenItHasNotBeenSelected_ThenItIsInactiveReferenceData()
    {
        var candidate = Contract("ES", "ES20270319", false, false, new(2027, 3, 19));

        candidate.IsValid.Should().BeTrue();
        candidate.OnTheRun.Should().BeFalse();
        candidate.Rollover.Should().BeFalse();
    }

    [Fact]
    public void GivenOnTheRunOutsideRolloverSet_WhenValidated_ThenTheDecisionIsRejected()
    {
        var invalid = Contract("ES", "ES20261218", true, false, new(2026, 12, 18));

        invalid.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GivenAMondayEffectiveDate_WhenPreparationIsCalculated_ThenFridayIsUsed()
    {
        var calendar = new CmeFuturesMarketSessionCalendar();

        calendar.GetPreparationDate(new DateOnly(2026, 9, 14))
            .Should().Be(new DateOnly(2026, 9, 11));
    }

    private static FuturesContractV3ReadModel Contract(
        string symbol,
        string id,
        bool onTheRun,
        bool rollover,
        DateOnly maturity) => new(
            id,
            id,
            symbol,
            id,
            "FUT",
            "USD",
            symbol == "VX" ? "CFE" : "CME",
            symbol == "VX" ? "1000" : "50",
            maturity,
            onTheRun,
            rollover);
}
