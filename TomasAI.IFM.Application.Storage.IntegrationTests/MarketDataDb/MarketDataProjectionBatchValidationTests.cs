using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class MarketDataProjectionBatchValidationTests
{
    [Fact]
    public void TickBatch_DuplicateCanonicalKey_IsRejectedBeforeDatabaseAccess()
    {
        var row = SampleData.FuturesTickData;
        var conflictingProjectionRow = row with
        {
            TickTime = row.TickTime.Add(TimeSpan.FromSeconds(1)),
            Price = row.Price + 1m
        };

        var error = Assert.Throws<ArgumentException>(() =>
            RawTickRequestQualification.RequireDistinctFuturesRows(new[] { row, conflictingProjectionRow }));

        Assert.Equal("values", error.ParamName);
    }

    [Fact]
    public void TickBatch_DifferentCanonicalKeys_AreAcceptedByValidation()
    {
        var row = SampleData.FuturesTickData;

        RawTickRequestQualification.RequireDistinctFuturesRows(new[]
        {
            row,
            row with { TickId = row.TickId + 1 },
            row with { ValueDate = row.ValueDate.AddDays(1) },
            row with { ContractId = row.ContractId + "-other" }
        });
    }

    [Fact]
    public async Task EodBatch_DuplicateCanonicalKey_IsRejectedBeforeDatabaseAccess()
    {
        var (context, dbFactory) = CreateContext();
        var row = SampleData.FuturesEodData;
        var conflictingProjectionRow = row with { ClosePrice = row.ClosePrice + 1m };

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            context.InsertFuturesEodDataAsync(new FuturesEodBatch(new[] { row, conflictingProjectionRow })));

        Assert.Equal("rows", error.ParamName);
        _ = dbFactory.DidNotReceive().MarketDataDb;
    }

    [Fact]
    public void EodBatch_DifferentCanonicalKeys_AreAcceptedByValidation()
    {
        var row = SampleData.FuturesEodData;

        _ = new FuturesEodBatch(new[]
        {
            row,
            row with { Symbol = row.Symbol + "-other" },
            row with { ValueDate = row.ValueDate.AddDays(1) },
            row with { ContractId = row.ContractId + "-other" }
        });
    }

    static (MarketDataDbContext Context, IDbContextFactory DbFactory) CreateContext()
    {
        var connectionSettings = new DbConnectionSettings().Add(
            MarketDataDbContext.MarketDataDbConnection,
            "Host=127.0.0.1;Port=1;Database=validation_only;Timeout=1", "System.Data.Postgres");
        var dbFactory = Substitute.For<IDbContextFactory>();
        var context = new MarketDataDbContext(
            connectionSettings,
            dbFactory,
            Substitute.For<ISequenceIdGenerator>(),
            Substitute.For<ILogger<DbProvider>>());
        return (context, dbFactory);
    }
}
