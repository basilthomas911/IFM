namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class OptionChainDefinitionTests
{
    private static readonly DateOnly Maturity = new(2026, 9, 18);

    [Fact]
    public void FiltersExactMaturityAndRightsThenSortsAndDeduplicates()
    {
        var request = Request(OptionRightSelection.Call);
        var details = new[]
        {
            Detail("ESU6 C5000", 5, ContractKind.CallOption, 5_000_000_000_000),
            Detail("ESU6 C-2", 2, ContractKind.CallOption, -2_000_000_000),
            Detail("ESU6 CMAX", 9, ContractKind.CallOption, long.MaxValue),
            Detail("ESU6 C5000-DUP", 5, ContractKind.CallOption, 5_000_000_000_000),
            Detail("ESU6 P5000", 6, ContractKind.PutOption, 5_000_000_000_000),
            Detail("ESZ6 C5000", 7, ContractKind.CallOption, 5_000_000_000_000,
                maturity: new DateOnly(2026, 12, 18)),
            Detail("ESU6", 8, ContractKind.Future, null)
        };

        var result = OptionChainDefinitionFilter.Create(
            "GLBX.MDP3",
            request,
            null,
            details);

        Assert.Equal(3, result.Contracts.Count);
        Assert.Equal(
            [-2m, 5000m, long.MaxValue / 1_000_000_000m],
            result.Contracts.Select(contract => contract.StrikePrice));
        Assert.All(result.Contracts, contract =>
        {
            Assert.Equal(Maturity, contract.MaturityDate);
            Assert.Equal(OptionRightSelection.Call, contract.Right);
        });
    }

    [Fact]
    public void UnderlyingFuturePolicyRequiresMatchingRawSymbolOrInstrument()
    {
        var underlying = Detail("ESU6", 100, ContractKind.Future, null) with
        {
            Underlying = string.Empty
        };
        var request = Request(OptionRightSelection.Both) with
        {
            Underlying = "ESU6",
            UniversePolicy = OptionUniversePolicy.UnderlyingFuture
        };
        var matchingBySymbol = Detail(
            "ESU6 C5000", 1, ContractKind.CallOption, 5_000_000_000_000);
        var matchingById = Detail(
            "ESU6 P5000", 2, ContractKind.PutOption, 5_000_000_000_000) with
        {
            Underlying = "provider-alias",
            UnderlyingInstrumentId = 100
        };
        var other = Detail(
            "ESZ6 C5000", 3, ContractKind.CallOption, 5_000_000_000_000) with
        {
            Underlying = "ESZ6"
        };

        var result = OptionChainDefinitionFilter.Create(
            "GLBX.MDP3",
            request,
            underlying,
            [matchingBySymbol, matchingById, other]);

        Assert.Equal(2, result.Contracts.Count);
        Assert.DoesNotContain(result.Contracts, contract => contract.RawSymbol == other.RawSymbol);
    }

    [Fact]
    public void QueryRejectsCrossDatasetRequestBeforeProviderAccess()
    {
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(
            DatabentoFeedOptions.ForProfile(
                FeedDeploymentProfile.Development,
                "GLBX.MDP3"));

        var exception = Assert.Throws<ArgumentException>(() => queries.GetChainDefinitions(
            Request(OptionRightSelection.Both) with { Dataset = "OPRA.PILLAR" }));

        Assert.Contains("does not match", exception.Message);
    }

    private static OptionChainDefinitionRequest Request(OptionRightSelection rights) => new()
    {
        Dataset = "GLBX.MDP3",
        Underlying = "ES",
        MaturityDate = Maturity,
        Rights = rights
    };

    private static ContractDetail Detail(
        string rawSymbol,
        uint instrumentId,
        ContractKind kind,
        long? strike,
        DateOnly? maturity = null) => new()
        {
            Dataset = "GLBX.MDP3",
            RawSymbol = rawSymbol,
            Ticker = "ES",
            Underlying = "ESU6",
            Instrument = new InstrumentKey(1, instrumentId),
            ContractKind = kind,
            StrikePrice = strike,
            MaturityDate = maturity ?? Maturity,
            Currency = "USD",
            SettlementCurrency = "USD",
            Exchange = "XCME",
            SecurityType = "",
            Cfi = "",
            UnitOfMeasure = ""
        };
}
