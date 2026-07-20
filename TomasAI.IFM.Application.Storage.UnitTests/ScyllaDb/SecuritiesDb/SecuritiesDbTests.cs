using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Trade.ViewModels;
using Xunit;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.SecuritiesDb;

public class SecuritiesDatabaseFixture : IDisposable
{

    public SecuritiesDatabaseFixture()
    {
        var dbConn = new DbConnectionSettings()
                         .Add("SecuritiesDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=securities_test_db", "System.Data.ScyllaDb");

        var diContainer = new Dictionary<Type, SecuritiesDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var redisCache = Substitute.For<IRedisCache>();
        var redisCacheMap = new Dictionary<string, string>();
        redisCache.Get(Arg.Any<string>()).Returns(callInfo => redisCacheMap[callInfo.Arg<string>()]);
        redisCache.When(_ => _.Set(Arg.Any<string>(), Arg.Any<string>())).Do(_ => { redisCacheMap.Add(_.ArgAt<string>(0), _.ArgAt<string>(1)); });
        var blackboardServce = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        DbFactory = dbFactory;
        diContainer.Add(typeof(IObjectRepository<SecuritiesDbContext>), new SecuritiesDbContext(dbConn, DbFactory, logger));
        Db = DbFactory.SecuritiesDb as SecuritiesDbContext;
    }

    public SecuritiesDbContext Db { get; }

    public IDbContextFactory DbFactory { get; }

    public void Dispose()
    {
    }
}

public class SecuritiesDbTests(SecuritiesDatabaseFixture testFixture) : IClassFixture<SecuritiesDatabaseFixture>
{
    readonly SecuritiesDatabaseFixture _testFixture = testFixture;

    [Fact]
    public async Task InsertFuturesContractAsync_ShouldInsertContract()
    {
        // Arrange
        var contractId = "ES40251111";
        var futuresContract = SampleData.FuturesContract;
        await _testFixture.Db.DeleteFuturesContractAsync(contractId); 
        
        // Act  
        await _testFixture.Db.InsertFuturesContractAsync(futuresContract with { ContractId = contractId});

        // Assert
        var result = await _testFixture.Db.GetFuturesContractAsync(contractId);
        result.Should().NotBeNull();
        result.ContractId.Should().Be(contractId);
    }

    [Fact]
    public async Task UpdateFuturesContractAsync_ShouldUpdateContract()
    {
        // Arrange
        var futuresContract = SampleData.FuturesContract;
        await _testFixture.Db.InsertFuturesContractAsync(futuresContract);

        var updatedContract = futuresContract with { Description = "Updated Description" };

        // Act
        await _testFixture.Db.UpdateFuturesContractAsync(futuresContract.Id, updatedContract);

        // Assert
        var result = await _testFixture.Db.GetFuturesContractAsync(updatedContract.ContractId);
        result.Should().NotBeNull();
        result.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task DeleteFuturesContractAsync_ShouldDeleteContract()
    {
        // Arrange
        var futuresContract = SampleData.FuturesContract;
        await _testFixture.Db.InsertFuturesContractAsync(futuresContract);

        // Act
        await _testFixture.Db.DeleteFuturesContractAsync(futuresContract.ContractId);

        // Assert
        var result = await _testFixture.Db.GetFuturesContractAsync(futuresContract.ContractId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFuturesContractAsync_ShouldReturnContract()
    {
        // Arrange
        var contractId = "ES30251111";
        var futuresContract = SampleData.FuturesContract;
        await _testFixture.Db.InsertFuturesContractAsync(futuresContract with { ContractId = contractId });

        // Act
        var result = await _testFixture.Db.GetFuturesContractAsync(contractId);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(contractId);
    }

    [Fact]
    public async Task GetFuturesContractsAsync_ShouldReturnAllContracts()
    {
        // Arrange
        var futuresContract1 = SampleData.FuturesContract with { ContractId = "ES20251111", Description = "Test Description 1" };
        var futuresContract2 = SampleData.FuturesContract with { ContractId = "ES20251212", Description = "Test Description 2" };

        await _testFixture.Db.InsertFuturesContractAsync(futuresContract1);
        await _testFixture.Db.InsertFuturesContractAsync(futuresContract2);

        // Act
        var result = await _testFixture.Db.GetFuturesContractsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain(x => x.ContractId == futuresContract1.ContractId);
        result.Should().Contain(x => x.ContractId == futuresContract2.ContractId);
    }

    /*
     write unit tests for the following methods:
        - InsertFuturesOptionContractAsync
        - UpdateFuturesOptionContractAsync
        - DeleteFuturesOptionContractAsync
        - GetFuturesOptionContractAsync
        - GetFuturesOptionContractsAsync    
     */

    [Fact]
    public async Task InsertFuturesOptionContractAsync_ShouldInsertContract()
    {
        // Arrange
        var futuresOptionContract = SampleData.FuturesOptionContract;

        // Act
        await _testFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract);

        // Assert
        var result = await _testFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
        result.Should().NotBeNull();
        result.ContractId.Should().Be(futuresOptionContract.ContractId);
    }

    [Fact]
    public async Task UpdateFuturesOptionContractAsync_ShouldUpdateContract()
    {
        // Arrange
        var futuresOptionContract = SampleData.FuturesOptionContract;
        await _testFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract);

        var updatedContract = futuresOptionContract with { Description = "Updated Description" };

        // Act
        await _testFixture.Db.UpdateFuturesOptionContractAsync(futuresOptionContract.ContractId, updatedContract);

        // Assert
        var result = await _testFixture.Db.GetFuturesOptionContractAsync(updatedContract.ContractId);
        result.Should().NotBeNull();
        result.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task DeleteFuturesOptionContractAsync_ShouldDeleteContract()
    {
        // Arrange
        var futuresOptionContract = SampleData.FuturesOptionContract;
        await _testFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract);

        // Act
        await _testFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract.ContractId);

        // Assert
        var result = await _testFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFuturesOptionContractAsync_ShouldReturnContract()
    {
        // Arrange
        var futuresOptionContract = SampleData.FuturesOptionContract;
        await _testFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract);

        // Act
        var result = await _testFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(futuresOptionContract.ContractId);
    }

    [Fact]
    public async Task GetFuturesOptionContractsAsync_ShouldReturnAllContracts()
    {
        // Arrange
        var futuresOptionContract1 = SampleData.FuturesOptionContract with { ContractId = "ES20251111C2535", Description = "Test Description 1" };
        var futuresOptionContract2 = SampleData.FuturesOptionContract with { ContractId = "ES20251212P2515", Description = "Test Description 2" };

        await _testFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract1);
        await _testFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract2);

        // Act
        var result = await _testFixture.Db.GetFuturesOptionContractsAsync(futuresOptionContract1.Symbol);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain(x => x.ContractId == futuresOptionContract1.ContractId);
        result.Should().Contain(x => x.ContractId == futuresOptionContract2.ContractId);
    }

    /// <summary>
    /// Unit test for GetFuturesContractsBySymbolAsync method
    /// </summary>
    [Fact]
    public async Task GetFuturesContractsBySymbolAsync_ReturnsExpectedResults()
    {
        // Arrange
        var symbol = "ES";
        var futuresContract1 = SampleData.FuturesContract with { ContractId = "ES20251111", Symbol = symbol, Description = "Test Description 1" };
        var futuresContract2 = SampleData.FuturesContract with { ContractId = "ES20251212", Symbol = symbol, Description = "Test Description 2" };

        await _testFixture.Db.DeleteFuturesContractAsync(futuresContract1.ContractId);
        await _testFixture.Db.DeleteFuturesContractAsync(futuresContract2.ContractId);

        await _testFixture.Db.InsertFuturesContractAsync(futuresContract1);
        await _testFixture.Db.InsertFuturesContractAsync(futuresContract2);

        // Act
        var result = await _testFixture.Db.GetFuturesContractsBySymbolAsync(symbol);

        // Assert
        result.Should().NotBeNull();
       // result.Should().HaveCount(2);
        result.Should().Contain(x => x.ContractId == futuresContract1.ContractId);
        result.Should().Contain(x => x.ContractId == futuresContract2.ContractId);
    }

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
