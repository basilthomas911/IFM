using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class FinancialModelingPrepReferenceDataApiTests
{
    [Fact]
    public void ExposesConfiguredProviderNeutralServices()
    {
        var treasuryCurve = Substitute.For<ITreasuryCurve>();
        var economicCalendar = Substitute.For<IEconomicCalendar>();

        IReferenceDataApi api = new FinancialModelingPrepReferenceDataApi(
            treasuryCurve,
            economicCalendar);

        api.TreasuryCurve.Should().BeSameAs(treasuryCurve);
        api.EconomicCalendar.Should().BeSameAs(economicCalendar);
    }

    [Fact]
    public void RejectsMissingTreasuryCurve()
    {
        var act = () => new FinancialModelingPrepReferenceDataApi(
            null!,
            Substitute.For<IEconomicCalendar>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("treasuryCurve");
    }

    [Fact]
    public void RejectsMissingEconomicCalendar()
    {
        var act = () => new FinancialModelingPrepReferenceDataApi(
            Substitute.For<ITreasuryCurve>(),
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("economicCalendar");
    }
}
