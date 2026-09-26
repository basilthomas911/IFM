using System;
using System.Threading;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class MarketDataDbContextConventionTests
{
    [Fact]
    public void Context_contract_exposes_repository_read_and_write_capabilities()
    {
        Assert.True(typeof(IObjectRepository<MarketDataDbContext>)
            .IsAssignableFrom(typeof(IMarketDataDbContext)));
        Assert.True(typeof(IMarketDataDbReadContext)
            .IsAssignableFrom(typeof(IMarketDataDbContext)));
        Assert.True(typeof(IMarketDataDbWriteContext)
            .IsAssignableFrom(typeof(IMarketDataDbContext)));
        Assert.True(typeof(IMarketDataDbContext)
            .IsAssignableFrom(typeof(MarketDataDbContext)));
    }

    [Fact]
    public void Factory_exposes_the_typed_market_data_context()
    {
        var property = typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.MarketDataDb));

        Assert.NotNull(property);
        Assert.Equal(typeof(IMarketDataDbContext), property.PropertyType);
    }

    [Fact]
    public void Download_log_contracts_are_separated_by_behavior()
    {
        Assert.NotNull(typeof(IMarketDataDbReadContext)
            .GetMethod(nameof(IMarketDataDbReadContext.GetMarketDataDownloadLogAsync)));
        Assert.Null(typeof(IMarketDataDbReadContext)
            .GetMethod(nameof(IMarketDataDbWriteContext.InsertMarketDataDownloadLogAsync)));
        Assert.NotNull(typeof(IMarketDataDbWriteContext)
            .GetMethod(nameof(IMarketDataDbWriteContext.InsertMarketDataDownloadLogAsync),
            [typeof(MarketDataDownloadOutcome), typeof(Guid), typeof(string), typeof(CancellationToken)]));
        Assert.Null(typeof(IMarketDataDbWriteContext)
            .GetMethod(nameof(IMarketDataDbReadContext.GetMarketDataDownloadLogAsync)));
    }
}
