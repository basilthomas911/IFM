using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

[Collection(DatabentoSmokeCollection.Name)]
public sealed class ContractMappingSmokeTests
{
    private readonly DatabentoSmokeFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ContractMappingSmokeTests(
        DatabentoSmokeFixture fixture,
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void CurrentEsFutureAndOptionMappingsRoundTrip()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var queries = _fixture.Queries;
        var definitions = queries
            .GetContractDetails("ES.FUT", TimeSpan.FromSeconds(90))
            .Concat(queries.GetContractDetails("ES.OPT", TimeSpan.FromSeconds(90)))
            .ToArray();
        var now = LiveTestGate.UtcNowNanoseconds();
        var current = new[]
        {
            definitions.First(detail =>
                detail.ContractKind == ContractKind.Future
                && detail.ExpirationTimestampNanoseconds > now),
            definitions.First(detail =>
                detail.ContractKind is ContractKind.CallOption or ContractKind.PutOption
                && detail.ExpirationTimestampNanoseconds > now)
        };

        foreach (var definition in current)
        {
            var contractId = queries.InstrumentIdToContractId(
                definition.Instrument.InstrumentId,
                TimeSpan.FromSeconds(90));
            var instrumentId = queries.ContractIdToInstrumentId(
                contractId,
                TimeSpan.FromSeconds(90));

            Assert.Equal(definition.Instrument.InstrumentId, instrumentId);
            _output.WriteLine(
                "{0} <-> {1} ({2})",
                contractId,
                instrumentId,
                definition.RawSymbol);
        }
    }
}
