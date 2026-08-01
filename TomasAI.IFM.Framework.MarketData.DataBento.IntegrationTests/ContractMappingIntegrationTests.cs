namespace TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests;

public sealed class ContractMappingIntegrationTests
{
    [Fact]
    public void ValidFormatButUnknownFutureThrowsDetailedMappingFailure()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var queries = LiveTestGate.CreateConnectedQueries();

        var exception = Assert.Throws<DatabentoContractMappingException>(() =>
            queries.ContractIdToInstrumentId(
                "IFM20300101",
                TimeSpan.FromSeconds(90)));

        Assert.Equal(
            ContractMappingDirection.ContractIdToInstrumentId,
            exception.Direction);
        Assert.Equal("IFM20300101", exception.ContractId);
        Assert.Contains("Databento rejected", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void UnknownInstrumentIdThrowsDetailedMappingFailure()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var queries = LiveTestGate.CreateConnectedQueries();

        var exception = Assert.Throws<DatabentoContractMappingException>(() =>
            queries.InstrumentIdToContractId(
                uint.MaxValue,
                TimeSpan.FromSeconds(90)));

        Assert.Equal(
            ContractMappingDirection.InstrumentIdToContractId,
            exception.Direction);
        Assert.Equal(uint.MaxValue, exception.InstrumentId);
        Assert.Contains("not present", exception.Message);
    }

    [Fact]
    public void InvalidCalendarDateThrowsAfterConnectionWasVerified()
    {
        if (!LiveTestGate.IsEnabled())
        {
            return;
        }
        LiveTestGate.AssertCredential();
        var queries = LiveTestGate.CreateConnectedQueries();

        var exception = Assert.Throws<DatabentoContractMappingException>(() =>
            queries.ContractIdToInstrumentId("ES20260230"));

        Assert.Contains("invalid yyyyMMdd", exception.Message);
    }
}
