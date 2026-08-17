using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests;

public sealed class ContractDetailsIntegrationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("GLBX.MDP3", "ES.FUT")]
    [InlineData("XCBF.PITCH", "VX.FUT")]
    public void ParentResolvedFutureCanBeHydratedByItsRawSymbol(
        string dataset,
        string parentSymbol)
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            dataset);
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(options);
        var parentContracts = queries.GetContractDetails(
            parentSymbol,
            TimeSpan.FromSeconds(90));

        Assert.NotEmpty(parentContracts);
        foreach (var contract in parentContracts)
        {
            output.WriteLine(
                "{0}: raw={1}, maturity={2}",
                dataset,
                contract.RawSymbol,
                contract.MaturityDate);
        }
        var selected = parentContracts
            .Where(contract => contract.ContractKind == ContractKind.Future)
            .Where(contract => contract.MaturityDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            .OrderBy(contract => contract.MaturityDate)
            .First();

        var hydrated = queries.GetContractDetail(
            selected.RawSymbol,
            TimeSpan.FromSeconds(90));

        Assert.NotNull(hydrated);
        Assert.Equal(selected.RawSymbol, hydrated.RawSymbol);
        Assert.Equal(selected.Instrument, hydrated.Instrument);
    }
}
