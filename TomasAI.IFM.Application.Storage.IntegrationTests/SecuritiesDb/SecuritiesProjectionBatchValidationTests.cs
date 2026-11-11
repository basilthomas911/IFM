using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Securities.Persistence;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

public sealed class SecuritiesProjectionBatchValidationTests
{
    [Fact]
    public async Task FuturesBatch_DuplicateContractId_IsRejectedBeforeProjectionOperation()
    {
        var context = CreateContext();
        var row = SampleData.FuturesContract;
        var conflictingProjectionRow = row with
        {
            Symbol = row.Symbol + "-other",
            LastTradeDate = row.LastTradeDate.AddDays(1)
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            context.DbWriter.InsertQualifiedFuturesContractsAsync(new[] { row, conflictingProjectionRow }));

        Assert.Equal("contracts", error.ParamName);
    }

    [Fact]
    public async Task FuturesOptionBatch_DuplicateContractId_IsRejectedBeforeProjectionOperation()
    {
        var context = CreateContext();
        var row = SampleData.FuturesOptionContract;
        var conflictingProjectionRow = row with
        {
            Symbol = row.Symbol + "-other",
            ContractMonth = row.ContractMonth.AddMonths(1),
            StrikePrice = row.StrikePrice + 1d
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            context.DbWriter.InsertQualifiedOptionContractsAsync(new[] { row, conflictingProjectionRow }));

        Assert.Equal("contracts", error.ParamName);
    }

    static SecuritiesDbContext CreateContext()
    {
        // A supported provider with an unreachable endpoint proves validation happens before I/O.
        var connectionSettings = new DbConnectionSettings().Add(SecuritiesDbContext.SecuritiesDbConnection,
            "Host=127.0.0.1;Port=1;Database=ifm_securities_argument_boundary;Timeout=1;Command Timeout=1", "System.Data.Postgres");
        return new(
            connectionSettings,
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<DbProvider>>());
    }
}
