namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DatabentoSmokeCollection
    : ICollectionFixture<DatabentoSmokeFixture>
{
    public const string Name = "Databento live smoke tests";
}

public sealed class DatabentoSmokeFixture
{
    public DatabentoFeedOptions Options { get; } = LiveTestGate.CreateOptions();

    public IDatabentoMarketDataQueries Queries { get; }

    public DatabentoSmokeFixture()
    {
        Queries = new DatabentoFeedFactory().CreateMarketDataQueries(Options);
    }
}
