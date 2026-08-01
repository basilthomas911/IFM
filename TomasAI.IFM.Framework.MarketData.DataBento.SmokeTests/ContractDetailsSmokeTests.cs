using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

public sealed class ContractDetailsSmokeTests
{
    private readonly ITestOutputHelper _output;

    public ContractDetailsSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CurrentEsFutureAndOptionDefinitionsResolveByRawSymbol()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(
            LiveTestGate.CreateOptions());
        var allEsContracts = queries.GetContractDetails("ES", TimeSpan.FromSeconds(90));
        var now = LiveTestGate.UtcNowNanoseconds();
        var future = allEsContracts.First(detail =>
            detail.ContractKind == ContractKind.Future
            && detail.ExpirationTimestampNanoseconds > now);
        var option = allEsContracts.First(detail =>
            detail.ContractKind is ContractKind.CallOption or ContractKind.PutOption
            && detail.ExpirationTimestampNanoseconds > now);

        var exact = queries.GetContractDetails(
            [future.RawSymbol, option.RawSymbol],
            TimeSpan.FromSeconds(45));

        Assert.Equal(future.RawSymbol, exact[0]!.RawSymbol);
        Assert.Equal(option.RawSymbol, exact[1]!.RawSymbol);
        _output.WriteLine("Current ES future: {0}; {1}", future.RawSymbol, future.Instrument);
        _output.WriteLine("Current ES option: {0}; {1}", option.RawSymbol, option.Instrument);
    }
}
