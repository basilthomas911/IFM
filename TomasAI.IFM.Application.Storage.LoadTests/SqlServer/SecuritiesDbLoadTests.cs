using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using Xunit;
using TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

namespace TomasAI.IFM.Application.Storage.LoadTests.SqlServer;

public class SecuritiesDbLoadTests(SecuritiesDatabaseFixture testFixture) : IClassFixture<SecuritiesDatabaseFixture>
{
    readonly SecuritiesDatabaseFixture _testFixture = testFixture;

    [Fact]
    [Trait("read futures contract from CSV file and insert into database", "FundDb")]
    public async Task GetFuturesContractsFromCsvFileOk()
    {
        var db = _testFixture.Db;
        var futuresContractDataFromCsv = await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\futures_contract.csv"))
           .ReadAsync<FuturesContractV3ReadModel>(MapToFuturesContract);
        futuresContractDataFromCsv.Should().NotBeNull();
        futuresContractDataFromCsv.Count.Should().BeGreaterThan(0);
        await db.UseTest($"truncate futures_contract").ExecuteCommandAsync();
        await db.InsertFuturesContractsAsync(futuresContractDataFromCsv);

        var resultSet = await db.GetFuturesContractsAsync();
        resultSet.Should().NotBeNull();
        resultSet.Count.Should().Be(futuresContractDataFromCsv.Count);
        return;

        static FuturesContractV3ReadModel MapToFuturesContract(IObjectDataRecord o)
             => new(
                o.GetString(0),
                o.GetString(1),
                o.GetString(2),
                o.GetString(3),
                o.GetString(4),
                o.GetString(5),
                o.GetString(6),
                o.GetString(7),
                o.GetDateOnly(8),
                o.GetBool(9));
    }

    [Fact]
    [Trait("read futures option contract from CSV file and insert into database", "FundDb")]
    public async Task GetFuturesOptionContractsFromCsvFileOk()
    {
        var db = _testFixture.Db;
        var futuresOptionContractDataFromCsv = await db.UseTest(new Uri("C:\\TomasAI\\data\\SqlServer\\futures_option_contract.csv"))
           .ReadAsync<FuturesOptionContractReadModel>(MapToFuturesOptionContract);
        futuresOptionContractDataFromCsv.Should().NotBeNull();
        futuresOptionContractDataFromCsv.Count.Should().BeGreaterThan(0);
        await db.UseTest($"truncate futures_option_contract").ExecuteCommandAsync();
        await db.InsertFuturesOptionContractsAsync(futuresOptionContractDataFromCsv);

        var resultSet = await db.GetFuturesOptionContractsAsync();
        resultSet.Should().NotBeNull();
        resultSet.Count.Should().Be(futuresOptionContractDataFromCsv.Count);
        return;

        static FuturesOptionContractReadModel MapToFuturesOptionContract(IObjectDataRecord o)
             => new(
                o.GetString(0),
                o.GetString(1),
                o.GetString(2),
                o.GetString(3),
                o.GetString(4),
                o.GetString(5),
                o.GetString(6),
                o.GetString(7),
                o.GetDateOnly(8),
                o.GetDouble(9),
                o.GetString(10));
    }
}
