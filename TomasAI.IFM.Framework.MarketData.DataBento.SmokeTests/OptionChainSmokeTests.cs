using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

public sealed class OptionChainSmokeTests
{
    private readonly ITestOutputHelper _output;

    public OptionChainSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CurrentEsOptionChainDefinitionsResolveWhenMarketIsClosed()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(
            LiveTestGate.CreateOptions());
        var maturity = FindCurrentEsOptionMaturity(queries);

        var chain = queries.GetChainDefinitions(
            new OptionChainDefinitionRequest
            {
                Dataset = "GLBX.MDP3",
                Underlying = "ES",
                MaturityDate = maturity,
                UniversePolicy = OptionUniversePolicy.ParentOptionSymbol,
                Rights = OptionRightSelection.Both
            },
            TimeSpan.FromSeconds(90));

        Assert.NotEmpty(chain.Contracts);
        Assert.All(chain.Contracts, contract =>
        {
            Assert.Equal("GLBX.MDP3", contract.Dataset);
            Assert.Equal(maturity, contract.MaturityDate);
            Assert.True(contract.Instrument.InstrumentId > 0);
        });
        Assert.Equal(
            chain.Contracts.OrderBy(contract => contract.StrikePrice)
                .ThenBy(contract => contract.Right)
                .ThenBy(contract => contract.RawSymbol, StringComparer.Ordinal),
            chain.Contracts);
        _output.WriteLine(
            "Resolved {0} current ES option definitions for {1:yyyy-MM-dd}.",
            chain.Contracts.Count,
            maturity);
    }

    [Fact]
    public void CurrentResolvedEsOptionChainAuthenticatesAndStartsOneLiveSession()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(
            LiveTestGate.CreateOptions());
        var maturity = FindCurrentEsOptionMaturity(queries);
        var chain = queries.GetChainDefinitions(
            new OptionChainDefinitionRequest
            {
                Dataset = "GLBX.MDP3",
                Underlying = "ES",
                MaturityDate = maturity,
                Rights = OptionRightSelection.Both
            },
            TimeSpan.FromSeconds(90));
        var contracts = chain.Contracts
            .Where(contract => !string.IsNullOrWhiteSpace(contract.Underlying))
            .GroupBy(contract => contract.Underlying, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .First()
            .Take(2)
            .ToArray();
        var rights = contracts.Aggregate(
            OptionRightSelection.None,
            (current, contract) => current | contract.Right);

        using var feed = new DatabentoFeedFactory().CreateOptionChainFeed(
            LiveTestGate.CreateLiveOptions());
        feed.Subscribe(
            new OptionChainSubscription
            {
                Underlying = contracts[0].Underlying,
                MaturityDate = maturity,
                Strikes = contracts.Select(contract => contract.StrikePrice).Distinct().ToArray(),
                Rights = rights,
                ResolvedContracts = contracts,
                DataKinds = MarketDataKinds.Quote
            },
            TimeSpan.FromSeconds(5));
        feed.Start(TimeSpan.FromSeconds(45));
        try
        {
            Assert.NotNull(feed.Reader);
            Assert.Equal(FeedState.Running, feed.GetHealth().State);
        }
        finally
        {
            feed.Stop(TimeSpan.FromSeconds(10));
        }
    }

    private static DateOnly FindCurrentEsOptionMaturity(
        IDatabentoMarketDataQueries queries)
    {
        var now = LiveTestGate.UtcNowNanoseconds();
        return queries.GetContractDetails("ES", TimeSpan.FromSeconds(90))
            .Where(detail => detail.ContractKind is
                ContractKind.CallOption or ContractKind.PutOption)
            .Where(detail => detail.ExpirationTimestampNanoseconds > now)
            .Select(detail => detail.MaturityDate)
            .First(maturity => maturity is not null)!.Value;
    }
}
