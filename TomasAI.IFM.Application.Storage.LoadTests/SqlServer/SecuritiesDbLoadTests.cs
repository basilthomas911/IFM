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
        var futuresContractDataFromCsv = await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\futures_contract.csv"))
           .ReadAsync<FuturesContractV2ReadModel>(MapToFuturesContract);
        futuresContractDataFromCsv.Should().NotBeNull();
        futuresContractDataFromCsv.Count.Should().BeGreaterThan(0);
        await db.Use($"truncate futures_contract").ExecuteCommandAsync();
        await db.InsertFuturesContractsAsync(futuresContractDataFromCsv);

        var resultSet = await db.GetFuturesContractsAsync();
        resultSet.Should().NotBeNull();
        resultSet.Count.Should().Be(futuresContractDataFromCsv.Count);
        return;

        static FuturesContractV2ReadModel MapToFuturesContract(IObjectMapReader<FuturesContractV2ReadModel> o)
             => new(
                o.Get(e => e.ContractId),
                o.Get(e => e.Description),
                o.Get(e => e.Symbol),
                o.Get(e => e.LocalSymbol),
                o.Get(e => e.SecurityType),
                o.Get(e => e.Currency),
                o.Get(e => e.Exchange),
                o.Get(e => e.Multiplier),
                o.Get(e => e.LastTradeDate),
                o.Get(e => e.CurrentlyTraded));
    }

    [Fact]
    [Trait("read futures option contract from CSV file and insert into database", "FundDb")]
    public async Task GetFuturesOptionContractsFromCsvFileOk()
    {
        var db = _testFixture.Db;
        var futuresOptionContractDataFromCsv = await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\futures_option_contract.csv"))
           .ReadAsync<FuturesOptionContractReadModel>(MapToFuturesOptionContract);
        futuresOptionContractDataFromCsv.Should().NotBeNull();
        futuresOptionContractDataFromCsv.Count.Should().BeGreaterThan(0);
        await db.Use($"truncate futures_option_contract").ExecuteCommandAsync();
        await db.InsertFuturesOptionContractsAsync(futuresOptionContractDataFromCsv);

        var resultSet = await db.GetFuturesOptionContractsAsync();
        resultSet.Should().NotBeNull();
        resultSet.Count.Should().Be(futuresOptionContractDataFromCsv.Count);
        return;

        static FuturesOptionContractReadModel MapToFuturesOptionContract(IObjectMapReader<FuturesOptionContractReadModel> o)
             => new(
                o.Get(e => e.ContractId),
                o.Get(e => e.Description),
                o.Get(e => e.Symbol),
                o.Get(e => e.LocalSymbol),
                o.Get(e => e.SecurityType),
                o.Get(e => e.Currency),
                o.Get(e => e.Exchange),
                o.Get(e => e.Multiplier),
                o.Get(e => e.ContractMonth),
                o.Get(e => e.StrikePrice),
                o.Get(e => e.OptionType));
    }
}
