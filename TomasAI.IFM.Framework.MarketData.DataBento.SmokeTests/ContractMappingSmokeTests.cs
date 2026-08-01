using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

public sealed class ContractMappingSmokeTests
{
    private readonly ITestOutputHelper _output;

    public ContractMappingSmokeTests(ITestOutputHelper output)
    {
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
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(
            LiveTestGate.CreateOptions());
        var definitions = queries.GetContractDetails("ES", TimeSpan.FromSeconds(90));
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
