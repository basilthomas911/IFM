using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
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
            context.InsertFuturesContractsAsync(new[] { row, conflictingProjectionRow }));

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
            context.InsertFuturesOptionContractsAsync(new[] { row, conflictingProjectionRow }));

        Assert.Equal("contracts", error.ParamName);
    }

    static SecuritiesDbContext CreateContext()
    {
        var connectionSettings = Substitute.For<IDbConnectionSettings>();
        connectionSettings[Arg.Any<string>()].Returns((IDbConnectionSetting)null!);
        return new(
            connectionSettings,
            Substitute.For<IDbContextFactory>(),
            Substitute.For<ILogger<DbProvider>>());
    }
}
