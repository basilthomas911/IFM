using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

[Collection(DatabentoSmokeCollection.Name)]
public sealed class ContractDetailsSmokeTests
{
    private readonly DatabentoSmokeFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ContractDetailsSmokeTests(
        DatabentoSmokeFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
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
        var queries = _fixture.Queries;
        var allEsContracts = queries
            .GetContractDetails("ES.FUT", TimeSpan.FromSeconds(90))
            .Concat(queries.GetContractDetails("ES.OPT", TimeSpan.FromSeconds(90)))
            .ToArray();
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
