namespace TomasAI.IFM.Framework.MarketData.DataBento.UnitTests;

public sealed class ContractDetailsTests
{
    [Fact]
    public void FactoryCreatesDatasetBoundContractQueries()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");

        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(options);

        Assert.NotNull(queries);
    }

    [Fact]
    public void EmptyExactContractArrayReturnsEmptyWithoutNativeCall()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(options);

        var details = queries.GetContractDetails([]);

        Assert.Empty(details);
    }

    [Fact]
    public void ExactContractArrayRejectsBlankItems()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(options);

        Assert.Throws<ArgumentException>(() =>
            queries.GetContractDetails(["ESU6", ""]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("es20260918")]
    [InlineData("ES20260230")]
    [InlineData("ES20260918X6950")]
    [InlineData("ES20260918C0")]
    [InlineData("ES20260918C1.1234567891")]
    public void ContractIdMappingRejectsMalformedIdentifiersBeforeNativeCall(
        string contractId)
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(options);

        var exception = Assert.Throws<DatabentoContractMappingException>(() =>
            queries.ContractIdToInstrumentId(contractId));

        Assert.Equal(
            ContractMappingDirection.ContractIdToInstrumentId,
            exception.Direction);
        Assert.Equal(contractId, exception.ContractId);
        Assert.Contains("Contract ID", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReverseMappingRejectsZeroInstrumentIdBeforeNativeCall()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(options);

        var exception = Assert.Throws<DatabentoContractMappingException>(() =>
            queries.InstrumentIdToContractId(0));

        Assert.Equal(
            ContractMappingDirection.InstrumentIdToContractId,
            exception.Direction);
        Assert.Equal(0u, exception.InstrumentId);
        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
