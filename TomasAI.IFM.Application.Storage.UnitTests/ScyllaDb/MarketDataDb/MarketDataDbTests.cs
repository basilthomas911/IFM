using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Trade.ViewModels;
using Xunit;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.MarketDataDb;

public class MarketDataFixture : IDisposable
{
    public MarketDataFixture()
    {
        SetSeqIdDatabase();
        SetSecDatabase();
        SetDevDatabase();
        SetPMDatabase();
        SetProdDatabase();
    }

    public void Dispose()
    {
        // Do "global" teardown here; Only called once.
    }

    public Storage.ScyllaDb.MarketDataDb.MarketDataDbContext DevDatabase { get; private set; }
    public Storage.ScyllaDb.SecuritiesDb.SecuritiesDbContext SecDatabase { get; private set; }
    public Storage.ScyllaDb.PredictiveModelDb.PredictiveModelDbContext PMDatabase { get; private set; }
    public Storage.ScyllaDb.MarketDataDb.MarketDataDbContext ProdDatabase { get; private set; }
    public Storage.Postgres.SequenceIdDb.SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }

    void SetDevDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("MarketDataDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=market_data_test_db", "System.Data.ScyllaDb");
        var diContainer = new Dictionary<Type, IObjectRepository>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var redisCache = Substitute.For<IRedisCache>();
        var redisCacheMap = new Dictionary<string, string>();
        redisCache.Get(Arg.Any<string>()).Returns(callInfo =>
        {
            if (!redisCacheMap.ContainsKey(callInfo.Arg<string>()))
            {
                return null;
            }
            else
            {
                return redisCacheMap[callInfo.Arg<string>()];
            }
        });
        redisCache.When(_ => _.Set(Arg.Any<string>(), Arg.Any<string>())).Do(_ => { redisCacheMap.Add(_.ArgAt<string>(0), _.ArgAt<string>(1)); });
        var blackboardService = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        diContainer.Add(typeof(IObjectRepository<Storage.ScyllaDb.MarketDataDb.MarketDataDbContext>), new Storage.ScyllaDb.MarketDataDb.MarketDataDbContext(dbConn, dbFactory, blackboardService, SequenceIdGenerator, logger));
        diContainer.Add(typeof(IObjectRepository<SecuritiesDbContext>), SecDatabase );

        DevDatabase = dbFactory.MarketDataDb as Storage.ScyllaDb.MarketDataDb.MarketDataDbContext;
    }

    void SetPMDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("PredictiveModelDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=predictive_model_test_db", "System.Data.ScyllaDb");
        var diContainer = new Dictionary<Type, Storage.ScyllaDb.PredictiveModelDb.PredictiveModelDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        diContainer.Add(typeof(IObjectRepository<Storage.ScyllaDb.PredictiveModelDb.PredictiveModelDbContext>), new Storage.ScyllaDb.PredictiveModelDb.PredictiveModelDbContext(dbConn, dbFactory, logger));
        PMDatabase = dbFactory.PredictiveModelDb as Storage.ScyllaDb.PredictiveModelDb.PredictiveModelDbContext;
    }

    void SetProdDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("MarketDataDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True", "System.Data.SqlClient");
        var diContainer = new Dictionary<Type, Storage.ScyllaDb.MarketDataDb.MarketDataDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var redisCache = Substitute.For<IRedisCache>();
        redisCache.When(_ => { }).Do(_ => { });
        var blackboardService = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        diContainer.Add(typeof(IObjectRepository<Storage.ScyllaDb.MarketDataDb.MarketDataDbContext>), new Storage.ScyllaDb.MarketDataDb.MarketDataDbContext(dbConn, dbFactory, blackboardService, SequenceIdGenerator, logger));
        ProdDatabase = dbFactory.MarketDataDb as Storage.ScyllaDb.MarketDataDb.MarketDataDbContext;
    }

    void SetSeqIdDatabase()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection", "Host=localhost;Port=5432;Username=postgres;Password=monkey35907;Database=sequence-id-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, SequenceIdDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<SequenceIdDbContext>), new SequenceIdDbContext(dbConn, dbFactory, logger));
        SeqIdDatabase  = dbFactory.SequenceIdDb as SequenceIdDbContext;
        SequenceIdGenerator = new PostgresSequenceIdGenerator(dbFactory.SequenceIdDb as SequenceIdDbContext);
        
    }

    void SetSecDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("SecuritiesDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=securities_test_db", "System.Data.ScyllaDb");
        var diContainer = new Dictionary<Type, SecuritiesDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var dbFactory  = new DbContextFactory(dbResolver);
        diContainer.Add(typeof(IObjectRepository<SecuritiesDbContext>), new SecuritiesDbContext(dbConn, dbFactory, logger));
        SecDatabase = dbFactory.SecuritiesDb as SecuritiesDbContext;
    }

}

public class MarketDataDbTests(MarketDataFixture testFixture) : IClassFixture<MarketDataFixture>
{
    MarketDataFixture TestFixture { get; } = testFixture;

    public async Task InsertFuturesTickDataFromProdToDev_Ok()
    {
        var db = TestFixture.ProdDatabase;
        var tickData = await db.Use($"select ContractId, ValueDate, TickDate, TickTime, Price, Size from dbo.futures_tick_data")
            .ExecuteQueryAsync<FuturesTickDataV2ReadModel>(MapToFuturesTickData);
        var counter = 0;
        var v2TickDataList = new LinkedList<FuturesTickDataV2ReadModel>();
        foreach (var e in tickData)
        {
            var v2TickData = new FuturesTickDataV2ReadModel
           (
               contractId: e.ContractId,
               valueDate: e.ValueDate,
               tickId: await TestFixture.SequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesTickData_TickId),
               tickTime: e.TickTime,
               price: e.Price,
               size: e.Size
           );
            v2TickDataList.AddLast(v2TickData);
        }
        var insertMap = new Dictionary<(string, DateOnly), LinkedList<FuturesTickDataV2ReadModel>>();
        foreach (var e in v2TickDataList)
        {
            var key = (e.ContractId, e.ValueDate);
            if (!insertMap.TryGetValue(key, out LinkedList<FuturesTickDataV2ReadModel> value))
            {
                value = new LinkedList<FuturesTickDataV2ReadModel>();
                insertMap.Add(key, value);
            }
            if (value.Count >= 65535)
                continue;
            value.AddLast(e);
        }
        foreach (var e in insertMap)
        {
            foreach (int o in Enumerable.Range(1, 10))
            {
                try
                {
                    await TestFixture.DevDatabase.InsertFuturesTickDataAsync(e.Value);
                    break;
                }
                catch (StorageTimoutException)
                {
                    await Task.Delay(1000);
                    if (o == 10)
                        break;
                }
            }
        }
        //CsvWriter.WriteToCsv(v2TickDataList, "C:\\Users\\basil\\OneDrive\\TomasAI\\Data\\Csv\\futures_tick_data.csv");
        Assert.NotNull(tickData);

        static FuturesTickDataV2ReadModel MapToFuturesTickData(IObjectDataRecord e)
            => new(
                contractId: e.GetString(0),
                valueDate: e.GetDateOnly(1),
                tickId: e.GetLong(2),
                tickTime: e.GetTimeOnly(3),
                price: e.GetDecimal(4),
                size: e.GetInt(5)
            );
    }

    public async Task InsertFuturesOptionTickDataFromProdToDev_Ok()
    {
        var db = TestFixture.ProdDatabase;
        var tickData = await db.Use($"SELECT OptionTickId, ContractId, TickDate, TickTime, OptionPrice, BidPrice, AskPrice, BidSize, AskSize, ImpliedVolatility, Delta, Gamma, Vega, Theta, Rho,UnderlyingPrice  FROM marketdatadb.dbo.futures_option_tick_data")
            .ExecuteQueryAsync<FuturesOptionTickDataV2ReadModel>(MapToFuturesOptionTickData);
        var v2TickDataList = new LinkedList<FuturesOptionTickDataV2ReadModel>();
        foreach (var e in tickData)
        {
            var v2TickData = new FuturesOptionTickDataV2ReadModel
            (
                contractId: e.ContractId,
                valueDate: e.ValueDate,
                tickId: await TestFixture.SequenceIdGenerator.GetSequenceIdAsync(SequenceName.FuturesOptionTickData_TickId),
                tickTime: e.TickTime,
                optionPrice: e.OptionPrice,
                bidPrice: e.BidPrice,
                askPrice: e.AskPrice,
                bidSize: e.BidSize,
                askSize: e.AskSize,
                impliedVolatility: e.ImpliedVolatility,
                underlyingPrice: e.UnderlyingPrice,
                delta: e.Delta,
                gamma: e.Gamma,
                vega: e.Vega,
                theta: e.Theta,
                rho: e.Rho
            );
            v2TickDataList.AddLast(v2TickData);
        }
        var insertMap = new Dictionary<(string, DateOnly), LinkedList<FuturesOptionTickDataV2ReadModel>>();
        foreach (var e in v2TickDataList)
        {
            var key = (e.ContractId, e.ValueDate);
            if (!insertMap.TryGetValue(key, out LinkedList<FuturesOptionTickDataV2ReadModel> value))
            {
                value = new LinkedList<FuturesOptionTickDataV2ReadModel>();
                insertMap.Add(key, value);
            }
            if (value.Count >= 65535)
                continue;
            value.AddLast(e);
        }
        foreach (var e in insertMap)
        {
            foreach (int o in Enumerable.Range(1, 10))
            {
                try
                {
                    await TestFixture.DevDatabase.InsertFuturesOptionTickDataAsync(e.Value);
                    break;
                }
                catch (StorageTimoutException)
                {
                    await Task.Delay(1000);
                    if (o == 10)
                        break;
                }
                catch (Exception ex)
                {
                    await Task.Delay(1000);
                    if (o == 10)
                        break;
                }
            }
        }
        //CsvWriter.WriteToCsv(v2TickDataList, "C:\\Users\\basil\\OneDrive\\TomasAI\\Data\\Csv\\futures_tick_data.csv");
        Assert.NotNull(tickData);

        static FuturesOptionTickDataV2ReadModel MapToFuturesOptionTickData(IObjectDataRecord e)
            => new(
                contractId: e.GetString(0),
                valueDate: e.GetDateOnly(1),
                tickId: e.GetLong(2),
                tickTime: e.GetTimeOnly(3),
                optionPrice: e.GetDouble(4),
                bidPrice: e.GetDouble(5),
                askPrice: e.GetDouble(6),
                bidSize: e.GetInt(7),
                askSize: e.GetInt(8),
                impliedVolatility: e.GetDouble(9),
                underlyingPrice: e.GetDouble(10),
                delta: e.GetDouble(11),
                gamma: e.GetDouble(12),
                vega: e.GetDouble(13),
                theta: e.GetDouble(14),
                rho: e.GetDouble(15)
            );
    }

    [Fact]
    public async Task InsertFuturesBarDataAsync_Ok()
    {
        // Arrange: Create a FuturesBarDataReadModel instance with sample data
        var e = SampleData.FuturesBarData;

        // Act: Insert the FuturesBarDataReadModel into the database
        await TestFixture.DevDatabase.DeleteFuturesBarDataAsync(e.Id);
        await TestFixture.DevDatabase.InsertFuturesBarDataAsync(e);

        // Assert: Verify that the data was inserted by checking the count of records with the same ID
        var count = await TestFixture.DevDatabase.GetFuturesBarDataCountAsync(e.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteFuturesBarDataAsync_Ok()
    {
        // Arrange: Insert a FuturesBarDataReadModel instance into the database
        var e = SampleData.FuturesBarData;
        await TestFixture.DevDatabase.DeleteFuturesBarDataAsync(e.Id);
        await TestFixture.DevDatabase.InsertFuturesBarDataAsync(e);

        // Act: Delete the FuturesBarDataReadModel from the database
        await TestFixture.DevDatabase.DeleteFuturesBarDataAsync(e.Id);

        // Assert: Verify that the data was deleted by checking the count of records with the same ID
        var count = await TestFixture.DevDatabase.GetFuturesBarDataCountAsync(e.Id);
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetFuturesBarDataAsync_Ok()
    {
        // Arrange: Insert a FuturesBarDataReadModel instance into the database
        var e = SampleData.FuturesBarData;
        await TestFixture.DevDatabase.DeleteFuturesBarDataAsync(e.Id);
        await TestFixture.DevDatabase.InsertFuturesBarDataAsync(e);

        // Act: Retrieve the FuturesBarDataReadModel from the database
        var startDate = e.BarDate.AddDays(-1);
        var endDate = e.BarDate.AddDays(1);
        var result = await TestFixture.DevDatabase.GetFuturesBarDataAsync(e.ContractId, e.Symbol, e.ValueDate, startDate, endDate);

        // Assert: Verify that the retrieved data matches the inserted data
        result.Should().ContainSingle();
        var resultData = result.First();
        resultData.ContractId.Should().Be(e.ContractId);
        resultData.Symbol.Should().Be(e.Symbol);
        resultData.ValueDate.Should().Be(e.ValueDate);
    }

    [Fact]
    public async Task InsertFuturesClosingPriceAsync_Ok()
    {
        // Arrange: Get a sample FuturesClosingPriceReadModel instance
        var futuresClosingPrice = SampleData.FuturesClosingPrice;

        // Act: Insert the FuturesClosingPriceReadModel into the database
        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{futuresClosingPrice.ContractId}' and valueDate = '{futuresClosingPrice.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(futuresClosingPrice);

        // Assert: Verify that the data was inserted by retrieving it and checking the values
        var retrievedData = await TestFixture.DevDatabase.GetFuturesClosingPriceAsync(futuresClosingPrice.Id);
        retrievedData.Should().NotBeNull();
        retrievedData.ContractId.Should().Be(futuresClosingPrice.ContractId);
        retrievedData.ValueDate.Should().Be(futuresClosingPrice.ValueDate);
        retrievedData.ClosingPrice.Should().Be(futuresClosingPrice.ClosingPrice);
        retrievedData.CreatedOn.Should().BeCloseTo(futuresClosingPrice.CreatedOn, TimeSpan.FromSeconds(1));
        retrievedData.CreatedBy.Should().Be(futuresClosingPrice.CreatedBy);
    }

    [Fact]
    public async Task InsertFuturesEodDataAsync_Ok()
    {
        // Arrange: Get a sample FuturesClosingPriceReadModel instance
        var futuresEodData = SampleData.FuturesEodData;
        var futuresDataId = FuturesDataId.Create(futuresEodData.ContractId, futuresEodData.ValueDate);
        var yesterdayClosingPrice = SampleData.YesterdaysFuturesClosingPrice;

        // Act: Insert the FuturesClosingPriceReadModel into the database
        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{yesterdayClosingPrice.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_eod_data where contractId = '{futuresDataId.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(yesterdayClosingPrice);

        // Assert: Verify that the data was inserted by retrieving it and checking the values
        await TestFixture.DevDatabase.InsertFuturesEodDataAsync(futuresEodData);
        var retrievedData = await TestFixture.DevDatabase.GetFuturesEodDataAsync(futuresEodData.ContractId, futuresEodData.ValueDate);
        retrievedData.Should().NotBeNull();
        retrievedData.ContractId.Should().Be(futuresEodData.ContractId);
        retrievedData.ValueDate.Should().Be(futuresEodData.ValueDate);
        retrievedData.Symbol.Should().Be(futuresEodData.Symbol);
        retrievedData.OpenPrice.Should().Be(yesterdayClosingPrice.ClosingPrice);
        retrievedData.HighPrice.Should().Be(futuresEodData.HighPrice);
        retrievedData.LowPrice.Should().Be(futuresEodData.LowPrice);
        retrievedData.ClosePrice.Should().Be(futuresEodData.ClosePrice);
    }

    [Fact]
    public async Task UpdateFuturesEodDataAsync_Ok()
    {
        // Arrange: Get a sample FuturesClosingPriceReadModel instance
        var futuresEodData = SampleData.FuturesEodData;
        var futuresDataId = FuturesDataId.Create(futuresEodData.ContractId, futuresEodData.ValueDate);
        var futuresClosingPrice = SampleData.FuturesClosingPrice;
        var yesterdayClosingPrice = SampleData.YesterdaysFuturesClosingPrice;

        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{yesterdayClosingPrice.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_eod_data where contractId = '{futuresDataId.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(futuresClosingPrice);
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(yesterdayClosingPrice);

        var futuresTickData = SampleData.FuturesTickData;
        await TestFixture.DevDatabase.Use($"delete from futures_tick_data where contractId = '{futuresTickData.ContractId}' and valueDate = '{futuresTickData.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.FuturesTickData);
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.FuturesTickDataHighPrice);
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.FuturesTickDataLowPrice);
        await TestFixture.DevDatabase.InsertFuturesEodDataAsync(futuresEodData);

        // Act: Insert the FuturesClosingPriceReadModel into the database
        await TestFixture.DevDatabase.InsertFuturesEodDataAsync(futuresEodData with { ClosePrice = 53.2m });

        // Assert: Verify that the data was inserted by retrieving it and checking the values
        var retrievedData = await TestFixture.DevDatabase.GetFuturesEodDataAsync(futuresEodData.ContractId, futuresEodData.ValueDate);
        retrievedData.Should().NotBeNull();
        retrievedData.ContractId.Should().Be(futuresEodData.ContractId);
        retrievedData.ValueDate.Should().Be(futuresEodData.ValueDate);
        retrievedData.Symbol.Should().Be(futuresEodData.Symbol);
        retrievedData.OpenPrice.Should().Be(yesterdayClosingPrice.ClosingPrice);
        retrievedData.HighPrice.Should().Be(futuresEodData.HighPrice);
        retrievedData.LowPrice.Should().Be(futuresEodData.LowPrice);
        retrievedData.ClosePrice.Should().Be(53.2m);
    }

    [Fact]
    public async Task UpsertVixFuturesEodDataAsync_NewValueDate()
    {
        // Arrange: Get a sample FuturesClosingPriceReadModel instance
        var futuresEodData = SampleData.VixFuturesEodData;

        var futuresTickData = SampleData.VixFuturesTickData;
        await TestFixture.DevDatabase.Use($"delete from vix_futures_eod_data where contractId = '{futuresEodData.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_tick_data where contractId = '{futuresTickData.ContractId}' and valueDate = '{futuresTickData.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.VixFuturesTickData);
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.VixFuturesTickDataHighPrice);
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.VixFuturesTickDataLowPrice);

        // Act: Insert the FuturesClosingPriceReadModel into the database
        await TestFixture.DevDatabase.InsertVixFuturesEodDataAsync(futuresTickData);

        // Assert: Verify that the data was inserted by retrieving it and checking the values
        var retrievedData = await TestFixture.DevDatabase.GetVixFuturesEodDataAsync(futuresEodData.EntityId.ContractId, futuresEodData.EntityId.ValueDate);
        retrievedData.Should().NotBeNull();
        retrievedData.ContractId.Should().Be(futuresTickData.ContractId);
        retrievedData.ValueDate.Should().Be(futuresTickData.ValueDate);
        retrievedData.OpenPrice.Should().Be(futuresTickData.Price);
        retrievedData.HighPrice.Should().Be(futuresTickData.Price);
        retrievedData.LowPrice.Should().Be(futuresTickData.Price);
        retrievedData.ClosePrice.Should().Be(futuresTickData.Price);
        retrievedData.Volume.Should().Be(futuresTickData.Size);
    }

    [Fact]
    public async Task UpsertVixFuturesEodDataAsync_ExistingValueDate()
    {
        // Arrange: Get a sample FuturesClosingPriceReadModel instance
        var futuresEodData = SampleData.VixFuturesEodData;

        var futuresTickData = SampleData.VixFuturesTickData;
        await TestFixture.DevDatabase.Use($"delete from vix_futures_eod_data where contractId = '{futuresEodData.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_tick_data where contractId = '{futuresTickData.ContractId}' and valueDate = '{futuresTickData.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.VixFuturesTickData);
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.VixFuturesTickDataHighPrice);
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.VixFuturesTickDataLowPrice);
        await TestFixture.DevDatabase.InsertVixFuturesEodDataAsync(futuresTickData);
        var totalVolume = SampleData.VixFuturesTickData.Size + SampleData.VixFuturesTickDataHighPrice.Size + SampleData.VixFuturesTickDataLowPrice.Size + 341;

        // Act: Insert the FuturesClosingPriceReadModel into the database
        await TestFixture.DevDatabase.InsertVixFuturesEodDataAsync(futuresTickData with { Price = 77.50m, Size = 341 });

        // Assert: Verify that the data was inserted by retrieving it and checking the values
        var retrievedData = await TestFixture.DevDatabase.GetVixFuturesEodDataAsync(futuresEodData.EntityId.ContractId, futuresEodData.EntityId.ValueDate);
        retrievedData.Should().NotBeNull();
        retrievedData.ContractId.Should().Be(futuresTickData.ContractId);
        retrievedData.ValueDate.Should().Be(futuresTickData.ValueDate);
        retrievedData.OpenPrice.Should().Be(futuresTickData.Price);
        retrievedData.HighPrice.Should().Be(SampleData.VixFuturesTickDataHighPrice.Price);
        retrievedData.LowPrice.Should().Be(SampleData.VixFuturesTickDataLowPrice.Price);
        retrievedData.ClosePrice.Should().Be(77.50m);
        retrievedData.Volume.Should().Be(totalVolume);
    }

    [Fact]
    public async Task GetYesterdaysFuturesClosingPriceAsync_Ok()
    {
        // Arrange: Get sample FuturesClosingPriceReadModel instances for today and yesterday
        var todayClosingPrice = SampleData.FuturesClosingPrice;
        var yesterdayClosingPrice = SampleData.YesterdaysFuturesClosingPrice;

        // Insert the sample data for today and yesterday into the database
        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{todayClosingPrice.ContractId}' and valueDate = '{todayClosingPrice.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{yesterdayClosingPrice.ContractId}' and valueDate = '{yesterdayClosingPrice.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(todayClosingPrice);
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(yesterdayClosingPrice);

        // Act: Retrieve yesterday's futures closing price from the database
        var retrievedData = await TestFixture.DevDatabase.GetYesterdaysFuturesClosingPriceAsync(todayClosingPrice.Id);

        // Assert: Verify that the retrieved data matches the inserted data for yesterday
        retrievedData.Should().NotBeNull();
        retrievedData.ContractId.Should().Be(yesterdayClosingPrice.ContractId);
        retrievedData.ValueDate.Should().Be(yesterdayClosingPrice.ValueDate);
        retrievedData.ClosingPrice.Should().Be(yesterdayClosingPrice.ClosingPrice);
        retrievedData.CreatedOn.Should().BeCloseTo(yesterdayClosingPrice.CreatedOn, TimeSpan.FromSeconds(1));
        retrievedData.CreatedBy.Should().Be(yesterdayClosingPrice.CreatedBy);
    }

    public async Task GetNextTickIdAsync_Ok()
    {
        // Arrange: Create a sample FuturesDataId
        var futuresDataId = new FuturesDataId("SampleContract", DateOnly.FromDateTime(DateTime.Today));

        // Act: Retrieve the next tick ID from the database
        var nextTickId = await TestFixture.DevDatabase.GetNextTickIdAsync(futuresDataId);

        // Assert: Verify that the next tick ID is greater than zero
        nextTickId.Should().BeGreaterThan(0);
    }

    public async Task GetNextTickIdAsync_IncrementsProperly()
    {
        // Arrange: Create a sample FuturesDataId
        var futuresDataId = new FuturesDataId("SampleContract", DateOnly.FromDateTime(DateTime.Today));

        // Act: Get the next tick ID multiple times
        var tickId1 = await TestFixture.DevDatabase.GetNextTickIdAsync(futuresDataId);
        var tickId2 = await TestFixture.DevDatabase.GetNextTickIdAsync(futuresDataId);
        var tickId3 = await TestFixture.DevDatabase.GetNextTickIdAsync(futuresDataId);

        // Assert: Verify that each subsequent tick ID increments by 1
        tickId2.Should().Be(tickId1 + 1);
        tickId3.Should().Be(tickId2 + 1);
    }

    [Fact]
    public async Task GetFuturesTickHLVDataAsync_ShouldReturnCorrectData()
    {
        // Arrange
        var futuresTickData = SampleData.FuturesTickData;
        var futuresDataId = new FuturesDataId(futuresTickData.ContractId, futuresTickData.ValueDate);
        var highPrice = SampleData.FuturesTickDataHighPrice.Price;
        var lowPrice = SampleData.FuturesTickDataLowPrice.Price;
        var volume = SampleData.FuturesTickData.Size + SampleData.FuturesTickDataHighPrice.Size + SampleData.FuturesTickDataLowPrice.Size;

        await TestFixture.DevDatabase.Use($"delete from futures_tick_data where contractId = '{futuresTickData.ContractId}' and valueDate = '{futuresTickData.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.FuturesTickData);
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.FuturesTickDataLowPrice);
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(SampleData.FuturesTickDataHighPrice);

        // Act
        var result = await TestFixture.DevDatabase.GetFuturesTickHLVDataAsync(futuresDataId);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(futuresTickData.ContractId);
        result.ValueDate.Should().Be(futuresTickData.ValueDate);
        result.HighPrice.Should().Be(highPrice);
        result.LowPrice.Should().Be(lowPrice);
        result.Volume.Should().Be(volume);
    }

    /// <summary>
    /// Tests the GetCurrentFuturesEodDataAsync method.
    /// </summary>
    [Fact]
    public async Task GetCurrentFuturesEodDataAsync_ShouldReturnCorrectData()
    {
        // Arrange
        var todayClosingPrice = SampleData.FuturesClosingPrice;
        var yesterdayClosingPrice = SampleData.YesterdaysFuturesClosingPrice;

        // Insert the sample data for today and yesterday into the database
        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{todayClosingPrice.ContractId}' and valueDate = '{todayClosingPrice.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{yesterdayClosingPrice.ContractId}' and valueDate = '{yesterdayClosingPrice.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(todayClosingPrice);
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(yesterdayClosingPrice);

        var expectedEodData = SampleData.FuturesEodData;
        var futuresDataId = SampleData.FuturesEodData.DataId;
        await TestFixture.DevDatabase.Use($"delete from futures_eod_data where contractId = '{futuresDataId.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesEodDataAsync(expectedEodData);
        await TestFixture.DevDatabase.InsertFuturesEodDataAsync(SampleData.YesterdaysFuturesEodData);

        // Act
        var result = await TestFixture.DevDatabase.GetCurrentFuturesEodDataAsync(futuresDataId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedEodData.ContractId);
        result.ValueDate.Should().Be(expectedEodData.ValueDate);
        result.Symbol.Should().Be(expectedEodData.Symbol);
        result.OpenPrice.Should().Be(yesterdayClosingPrice.ClosingPrice);
        result.HighPrice.Should().Be(expectedEodData.HighPrice);
        result.LowPrice.Should().Be(expectedEodData.LowPrice);
        result.ClosePrice.Should().Be(expectedEodData.ClosePrice);
        result.Volume.Should().Be(expectedEodData.Volume);
    }

    /// <summary>
    /// Tests the GetCurrentFuturesEodDataByDateRangeAsync method.
    /// </summary>
    [Fact]
    public async Task GetCurrentFuturesEodDataByDateRangeAsync_ShouldReturnCorrectData()
    {
        // Arrange
        var startDate = SampleData.YesterdaysFuturesEodData.ValueDate;
        var endDate = SampleData.FuturesEodData.ValueDate;
        var todayClosingPrice = SampleData.FuturesClosingPrice;
        var yesterdayClosingPrice = SampleData.YesterdaysFuturesClosingPrice;

        // Insert the sample data for today and yesterday into the database
        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{todayClosingPrice.ContractId}' and valueDate = '{todayClosingPrice.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{yesterdayClosingPrice.ContractId}' and valueDate = '{yesterdayClosingPrice.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(todayClosingPrice);
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(yesterdayClosingPrice);

        var futuresDataId = SampleData.FuturesEodData.DataId;
        await TestFixture.DevDatabase.Use($"delete from futures_eod_data where contractId = '{futuresDataId.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesEodDataAsync(SampleData.FuturesEodData);
        await TestFixture.DevDatabase.InsertFuturesEodDataAsync(SampleData.YesterdaysFuturesEodData);

        // Act
        var result = await TestFixture.DevDatabase.GetCurrentFuturesEodDataByDateRangeAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Count(e => e.ContractId == SampleData.FuturesEodData.ContractId).Should().Be(2);
    }

    /// <summary>
    /// Unit test for GetFuturesItiSignalsAsync method
    /// </summary>
    [Fact]
    public async Task GetFuturesItiSignalsAsync_ReturnsExpectedResults()
    {
        // Arrange
        var symbol = SampleData.FuturesContract1.Symbol;
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 31);
        await TestFixture.SecDatabase.Use($"delete from futures_contract where contractId in ('{SampleData.FuturesContract1.ContractId}','{SampleData.FuturesContract2.ContractId}')").ExecuteCommandAsync();
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract1);
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract2);

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{SampleData.FuturesItiSignal1.ContractId}' and valueDate = '{SampleData.FuturesItiSignal1.ValueDate}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{SampleData.FuturesItiSignal2.ContractId}' and valueDate = '{SampleData.FuturesItiSignal2.ValueDate}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal1);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal2);

        // Act
        var result = await TestFixture.DevDatabase.GetFuturesItiSignalsAsync(symbol, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().Contain(signal => signal.ContractId == $"{SampleData.FuturesContract1.ContractId}");
        result.Should().Contain(signal => signal.ContractId == $"{SampleData.FuturesContract2.ContractId}");
    }

    /// <summary>
    /// Unit test for GetFuturesItiSignalsAsync method
    /// </summary>
    [Fact]
    public async Task GetFuturesItiSignalsByIdAsync_ReturnsExpectedResults()
    {
        // Arrange
        var symbol = SampleData.FuturesContract1.Symbol;
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 31);
        await TestFixture.SecDatabase.Use($"delete from futures_contract where contractId in ('{SampleData.FuturesContract1.ContractId}','{SampleData.FuturesContract2.ContractId}')").ExecuteCommandAsync();
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract1);
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract2);

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{SampleData.FuturesItiSignal1.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal1);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal2 with { ContractId = SampleData.FuturesItiSignal1.ContractId, ValueDate = SampleData.FuturesItiSignal1.ValueDate });

        // Act
        var result = await TestFixture.DevDatabase.GetFuturesItiSignalsAsync(SampleData.FuturesItiSignal1.EntityId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().OnlyContain(signal => signal.ContractId == $"{SampleData.FuturesContract1.ContractId}");
    }

    /// <summary>
    /// Unit test for GetFuturesItiSignalMDIAsync method
    /// </summary>
    [Fact]
    public async Task GetFuturesItiSignalMDIAsync_ReturnsExpectedResults()
    {
        // Arrange
        var symbol = SampleData.FuturesContract1.Symbol;
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 31);
        await TestFixture.SecDatabase.Use($"delete from futures_contract where contractId in ('{SampleData.FuturesContract1.ContractId}','{SampleData.FuturesContract2.ContractId}')").ExecuteCommandAsync();
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract1);
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract2);
        var resultFuturesItiSignal = SampleData.FuturesItiSignal1 with { ValueDate = SampleData.FuturesItiSignal1.ValueDate.AddDays(-4) };

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{SampleData.FuturesItiSignal1.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal1);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(resultFuturesItiSignal);
        var entityId = resultFuturesItiSignal.EntityId;

        // Act
        var result = await TestFixture.DevDatabase.GetFuturesItiSignalMDIAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        var resultData = result.First();
        resultData.ContractId.Should().Be(resultFuturesItiSignal.ContractId);
        resultData.ValueDate.Should().Be(resultFuturesItiSignal.ValueDate);
        resultData.TrendType.Should().Be(resultFuturesItiSignal.IntrinsicTimeTrend);
    }

    /// <summary>
    /// Unit test for GetFuturesItiSignalMDIByTrendAsync method
    /// </summary>
    [Fact]
    public async Task GetFuturesItiSignalMDIByTrendAsync_ReturnsExpectedResults()
    {
        // Arrange
        var symbol = SampleData.FuturesContract1.Symbol;
        await TestFixture.SecDatabase.Use($"delete from futures_contract where contractId in ('{SampleData.FuturesContract1.ContractId}','{SampleData.FuturesContract2.ContractId}')").ExecuteCommandAsync();
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract1);
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract2);

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{SampleData.FuturesItiSignal1.ContractId}' and valueDate = '{SampleData.FuturesItiSignal1.ValueDate}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{SampleData.FuturesItiSignal2.ContractId}' and valueDate = '{SampleData.FuturesItiSignal2.ValueDate}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal1);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal2);

        var entityId = SampleData.FuturesItiSignal1.EntityId;


        // Act
        var result = await TestFixture.DevDatabase.GetFuturesItiSignalMDIByTrendAsync(entityId.ContractId, entityId.ValueDate, SampleData.FuturesItiSignal1.IntrinsicTimeTrend, SampleData.FuturesItiSignal1.IntrinsicTimeGroupId);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        var resultData = result.First();
        resultData.ContractId.Should().Be(SampleData.FuturesItiSignal1.EntityId.ContractId);
        resultData.ValueDate.Should().Be(SampleData.FuturesItiSignal1.EntityId.ValueDate);
        resultData.TrendType.Should().Be(SampleData.FuturesItiSignal1.IntrinsicTimeTrend);
    }

    /// <summary>
    /// Unit test for GetFuturesItiSignalTrendDeltaDataAsync method
    /// </summary>
    [Fact]
    public async Task GetFuturesItiSignalTrendDeltaDataAsync_ReturnsExpectedResults()
    {
        // Arrange
        var symbol = SampleData.FuturesContract1.Symbol;
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 31);
        await TestFixture.SecDatabase.Use($"delete from futures_contract where contractId in ('{SampleData.FuturesContract1.ContractId}','{SampleData.FuturesContract2.ContractId}')").ExecuteCommandAsync();
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract1);
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract2);

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{SampleData.FuturesItiSignal1.ContractId}' and valueDate = '{SampleData.FuturesItiSignal1.ValueDate}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{SampleData.FuturesItiSignal2.ContractId}' and valueDate = '{SampleData.FuturesItiSignal2.ValueDate}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal1);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal2);

        // Act
        var result = await TestFixture.DevDatabase.GetFuturesItiSignalTrendDeltaDataAsync(symbol, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThanOrEqualTo(2);
        result.Should().Contain(x => x.ContractId == SampleData.FuturesContract1.ContractId);
        result.Should().Contain(x => x.ContractId == SampleData.FuturesContract2.ContractId);
    }

    /// <summary>
    /// Unit test for LoadFuturesItiTrendClassDataAsync method
    /// </summary>
    [Fact]
    public async Task LoadFuturesItiTrendClassDataAsync_ReturnsExpectedResults()
    {
        // Arrange
        var symbol = "SYM";
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 31);
        var futuresItiSignal1 = SampleData.FuturesItiSignal1;
        var futuresItiSignal2 = SampleData.FuturesItiSignal2;

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{futuresItiSignal1.ContractId}' and valueDate = '{futuresItiSignal1.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{futuresItiSignal2.ContractId}' and valueDate = '{futuresItiSignal2.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"truncate futures_iti_trend_class_data").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(futuresItiSignal1);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(futuresItiSignal2);

        // Act
        var result = await TestFixture.DevDatabase.LoadFuturesItiTrendClassDataAsync(symbol, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThanOrEqualTo(2);
        result.Maximum.Should().Be(2);
        result.Minimum.Should().Be(1);
        result.Mean.Should().BeGreaterThan(1.0);
        result.Median.Should().BeGreaterThanOrEqualTo(0.0);
        result.Skewness.Should().BeGreaterThanOrEqualTo(0.0);
        result.StdDev.Should().NotBe(0);
        result.Variance.Should().NotBe(0);
    }

    [Fact]
    public async Task LoadFuturesItiTrendDeltaDataAsync_ReturnsExpectedResults()
    {
        // Arrange
        var symbol = "SYM";
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 31);
        var futuresContract1 = SampleData.FuturesContract1;
        var futuresContract2 = SampleData.FuturesContract2;
        var futuresItiSignal1 = SampleData.FuturesItiSignal1;
        var futuresItiSignal2 = SampleData.FuturesItiSignal2;

        await TestFixture.SecDatabase.Use($"delete from futures_contract where contractId = '{futuresContract1.ContractId}'").ExecuteCommandAsync();
        await TestFixture.SecDatabase.Use($"delete from futures_contract where contractId = '{futuresContract2.ContractId}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{futuresItiSignal1.ContractId}' and valueDate = '{futuresItiSignal1.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{futuresItiSignal2.ContractId}' and valueDate = '{futuresItiSignal2.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_iti_trend_delta_data where symbol = '{symbol}' ").ExecuteCommandAsync();
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(futuresContract1);
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(futuresContract2);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(futuresItiSignal1);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(futuresItiSignal2);

        // Act
        var result = await TestFixture.DevDatabase.LoadFuturesItiTrendDeltaDataAsync(symbol, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result.Maximum.Should().Be(2);
        result.Minimum.Should().Be(1);
        result.Mean.Should().Be(1.5);
        result.Median.Should().Be(1.5);
        result.Skewness.Should().Be(0);
        result.StdDev.Should().NotBe(0);
        result.Variance.Should().NotBe(0);
    }

    /// <summary>
    /// Unit test for GetFuturesItiTrendClassModelAsync method.
    /// </summary>
    [Fact]
    public async Task GetFuturesItiTrendClassModelAsync_ReturnsExpectedResults()
    {
        // Arrange
        var expectedModel = SampleData.FuturesItiTrendClassModel;
        var symbol = expectedModel.Symbol;
        var valueDate = expectedModel.ValueDate;

        await TestFixture.DevDatabase.Use($"delete from futures_iti_trend_class_model where symbol = '{symbol}' and valueDate = '{valueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiTrendClassModelAsync(expectedModel);

        // Act
        var result = await TestFixture.DevDatabase.GetFuturesItiTrendClassModelAsync(symbol, valueDate);

        // Assert
        result.Should().NotBeNull();
        result.Symbol.Should().Be(expectedModel.Symbol);
        result.ValueDate.Should().Be(expectedModel.ValueDate);
        result.StartDate.Should().Be(expectedModel.StartDate);
        result.EndDate.Should().Be(expectedModel.EndDate);
        result.Count.Should().Be(expectedModel.Count);
        result.Maximum.Should().Be(expectedModel.Maximum);
        result.Mean.Should().Be(expectedModel.Mean);
        result.Median.Should().Be(expectedModel.Median);
        result.Minimum.Should().Be(expectedModel.Minimum);
        result.Skewness.Should().Be(expectedModel.Skewness);
        result.StdDev.Should().Be(expectedModel.StdDev);
        result.Variance.Should().Be(expectedModel.Variance);
        result.Accuracy.Should().Be(expectedModel.Accuracy);
        result.AreaUnderPrecisionRecallCurve.Should().Be(expectedModel.AreaUnderPrecisionRecallCurve);
        result.AreaUnderRocCurve.Should().Be(expectedModel.AreaUnderRocCurve);
        result.Entropy.Should().Be(expectedModel.Entropy);
        result.F1Score.Should().Be(expectedModel.F1Score);
        result.ModelData.Should().BeEquivalentTo(expectedModel.ModelData);
    }

    /// <summary>
    /// Unit test for GetFuturesItiTrendDeltaModelAsync method.
    /// </summary>
    [Fact]
    public async Task GetFuturesItiTrendDeltaModelAsync_ReturnsExpectedResults()
    {
        // Arrange
        var expectedModel = SampleData.FuturesItiTrendDeltaModel;
        var symbol = expectedModel.Symbol;
        var valueDate = expectedModel.ValueDate;

        await TestFixture.DevDatabase.Use($"delete from futures_iti_trend_delta_model where symbol = '{symbol}' and valueDate = '{valueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiTrendDeltaModelAsync(expectedModel);

        // Act
        var result = await TestFixture.DevDatabase.GetFuturesItiTrendDeltaModelAsync(symbol, valueDate);

        // Assert
        result.Should().NotBeNull();
        result.Symbol.Should().Be(expectedModel.Symbol);
        result.ValueDate.Should().Be(expectedModel.ValueDate);
        result.StartDate.Should().Be(expectedModel.StartDate);
        result.EndDate.Should().Be(expectedModel.EndDate);
        result.Count.Should().Be(expectedModel.Count);
        result.Maximum.Should().Be(expectedModel.Maximum);
        result.Mean.Should().Be(expectedModel.Mean);
        result.Median.Should().Be(expectedModel.Median);
        result.Minimum.Should().Be(expectedModel.Minimum);
        result.Skewness.Should().Be(expectedModel.Skewness);
        result.StdDev.Should().Be(expectedModel.StdDev);
        result.Variance.Should().Be(expectedModel.Variance);
        result.MeanAbsoluteError.Should().Be(expectedModel.MeanAbsoluteError);
        result.MeanSquaredError.Should().Be(expectedModel.MeanSquaredError);
        result.RootMeanSquaredError.Should().Be(expectedModel.RootMeanSquaredError);
        result.LossFunction.Should().Be(expectedModel.LossFunction);
        result.RSquared.Should().Be(expectedModel.RSquared);
        result.ModelData.Should().BeEquivalentTo(expectedModel.ModelData);
    }

    /// <summary>
    /// Unit test for GetFuturesItiTrendDirectionChangedSignalsAsync method.
    /// </summary>
    [Fact]
    public async Task GetFuturesItiTrendDirectionChangedSignalsAsync_ReturnsExpectedResults()
    {
        // Arrange
        var entityId = SampleData.FuturesItiSignal1.EntityId;
        var expectedSignals = new List<FuturesItiSignalV2ReadModel> { SampleData.FuturesItiSignal1 };

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{SampleData.FuturesItiSignal1.ContractId}' and valueDate = '{SampleData.FuturesItiSignal1.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{SampleData.FuturesItiSignal2.ContractId}' and valueDate = '{SampleData.FuturesItiSignal2.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal1 with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendDirectionChanged });
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(SampleData.FuturesItiSignal2 with
        {
            ContractId = SampleData.FuturesItiSignal1.ContractId,
            ValueDate = SampleData.FuturesItiSignal1.ValueDate,
            IntrinsicTimeMode = IntrinsicTimeModeType.Trending
        });

        // Act
        var result = await TestFixture.DevDatabase.GetFuturesItiTrendDirectionChangedSignalsAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(expectedSignals.Count);
        result.Should().ContainSingle(x => x.ContractId == SampleData.FuturesItiSignal1.ContractId);
    }

    /// <summary>
    /// Unit test for GetLastFuturesItiSignalAsync method using sample data and asserting each expected value.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesItiSignalAsync_ReturnsExpectedResultWithCorrectValues()
    {
        // Arrange
        var entityId = SampleData.FuturesItiSignal1.EntityId;
        var expectedSignal = SampleData.FuturesItiSignal1 with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendReversalChanged };
        var trendingSignal = SampleData.FuturesItiSignal1 with { IntrinsicTimeMode = IntrinsicTimeModeType.Trending };
        var trendDirectionSignal = SampleData.FuturesItiSignal1;

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{expectedSignal.ContractId}' and valueDate = '{expectedSignal.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendDirectionSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendingSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(expectedSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesItiSignalAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.SequenceId.Should().BeGreaterThan(0);
        result.IntrinsicTime.ToLocalTime().Should().BeCloseTo(expectedSignal.IntrinsicTime, 10.Seconds());
        result.IntrinsicTimeGroupId.Should().Be(expectedSignal.IntrinsicTimeGroupId);
        result.IntrinsicTimeLength.Should().Be(expectedSignal.IntrinsicTimeLength);
        result.IntrinsicPrice.Should().Be(expectedSignal.IntrinsicPrice);
        result.IntrinsicTimeTrend.Should().Be(expectedSignal.IntrinsicTimeTrend);
        result.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
        result.TrendPrice.Should().Be(expectedSignal.TrendPrice);
        result.TrendExtreme.Should().Be(expectedSignal.TrendExtreme);
        result.TrendReversal.Should().Be(expectedSignal.TrendReversal);
        result.Lambda.Should().Be(expectedSignal.Lambda);
        result.TargetDelta.Should().Be(expectedSignal.TargetDelta);
        result.TrendDelta.Should().Be(expectedSignal.TrendDelta);
        result.UpTrendTrigger.Should().Be(expectedSignal.UpTrendTrigger);
        result.DownTrendTrigger.Should().Be(expectedSignal.DownTrendTrigger);
        result.TradeState.Should().Be(expectedSignal.TradeState);
    }

    /// <summary>
    /// Unit test for GetLastFuturesItiSignalTrendDirectionChangeAsync method.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesItiSignalTrendDirectionChangeAsync_ReturnsExpectedResult()
    {
        // Arrange
        var entityId = SampleData.FuturesItiSignal1.EntityId;
        var trendReversalChangedSignal = SampleData.FuturesItiSignal1 with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendReversalChanged };
        var trendingSignal = SampleData.FuturesItiSignal1 with { IntrinsicTimeMode = IntrinsicTimeModeType.Trending };
        var expectedSignal = SampleData.FuturesItiSignal1;

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{expectedSignal.ContractId}' and valueDate = '{expectedSignal.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendReversalChangedSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(expectedSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendingSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesItiSignalTrendDirectionChangeAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.SequenceId.Should().BeGreaterThan(0);
        result.IntrinsicTime.ToLocalTime().Should().BeCloseTo(expectedSignal.IntrinsicTime, 10.Seconds());
        result.IntrinsicTimeGroupId.Should().Be(expectedSignal.IntrinsicTimeGroupId);
        result.IntrinsicTimeLength.Should().Be(expectedSignal.IntrinsicTimeLength);
        result.IntrinsicPrice.Should().Be(expectedSignal.IntrinsicPrice);
        result.IntrinsicTimeTrend.Should().Be(expectedSignal.IntrinsicTimeTrend);
        result.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        result.TrendPrice.Should().Be(expectedSignal.TrendPrice);
        result.TrendExtreme.Should().Be(expectedSignal.TrendExtreme);
        result.TrendReversal.Should().Be(expectedSignal.TrendReversal);
        result.Lambda.Should().Be(expectedSignal.Lambda);
        result.TargetDelta.Should().Be(expectedSignal.TargetDelta);
        result.TrendDelta.Should().Be(expectedSignal.TrendDelta);
        result.UpTrendTrigger.Should().Be(expectedSignal.UpTrendTrigger);
        result.DownTrendTrigger.Should().Be(expectedSignal.DownTrendTrigger);
        result.TradeState.Should().Be(expectedSignal.TradeState);
    }

    /// <summary>
    /// Unit test for GetLastFuturesItiSignalTrendExtremeChangeAsync method.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesItiSignalTrendExtremeChangeAsync_ReturnsExpectedResult()
    {
        // Arrange
        var entityId = SampleData.FuturesItiSignal1.EntityId;
        var expectedSignal = SampleData.FuturesItiSignal1 with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendExtremeChanged };
        var trendingSignal = SampleData.FuturesItiSignal1 with { IntrinsicTimeMode = IntrinsicTimeModeType.Trending };
        var trendDirectionChangedSignal = SampleData.FuturesItiSignal1;

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{expectedSignal.ContractId}' and valueDate = '{expectedSignal.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendDirectionChangedSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(expectedSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendingSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesItiSignalTrendExtremeChangeAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.SequenceId.Should().BeGreaterThan(0);
        result.IntrinsicTime.ToLocalTime().Should().BeCloseTo(expectedSignal.IntrinsicTime, 10.Seconds());
        result.IntrinsicTimeGroupId.Should().Be(expectedSignal.IntrinsicTimeGroupId);
        result.IntrinsicTimeLength.Should().Be(expectedSignal.IntrinsicTimeLength);
        result.IntrinsicPrice.Should().Be(expectedSignal.IntrinsicPrice);
        result.IntrinsicTimeTrend.Should().Be(expectedSignal.IntrinsicTimeTrend);
        result.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendExtremeChanged);
        result.TrendPrice.Should().Be(expectedSignal.TrendPrice);
        result.TrendExtreme.Should().Be(expectedSignal.TrendExtreme);
        result.TrendReversal.Should().Be(expectedSignal.TrendReversal);
        result.Lambda.Should().Be(expectedSignal.Lambda);
        result.TargetDelta.Should().Be(expectedSignal.TargetDelta);
        result.TrendDelta.Should().Be(expectedSignal.TrendDelta);
        result.UpTrendTrigger.Should().Be(expectedSignal.UpTrendTrigger);
        result.DownTrendTrigger.Should().Be(expectedSignal.DownTrendTrigger);
        result.TradeState.Should().Be(expectedSignal.TradeState);
    }

    /// <summary>
    /// Unit test for GetLastFuturesItiSignalTrendReversalChangeAsync method.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesItiSignalTrendReversalChangeAsync_ReturnsExpectedResult()
    {
        // Arrange
        var entityId = SampleData.FuturesItiSignal1.EntityId;
        var expectedSignal = SampleData.FuturesItiSignal1 with { IntrinsicTimeMode = IntrinsicTimeModeType.TrendReversalChanged };
        var trendingSignal = SampleData.FuturesItiSignal1 with { IntrinsicTimeMode = IntrinsicTimeModeType.Trending };
        var trendDirectionChangedSignal = SampleData.FuturesItiSignal1;

        await TestFixture.DevDatabase.Use($"delete from futures_iti_signal where contractId = '{expectedSignal.ContractId}' and valueDate = '{expectedSignal.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendDirectionChangedSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(expectedSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendingSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesItiSignalTrendReversalChangeAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.SequenceId.Should().BeGreaterThan(0);
        result.IntrinsicTime.ToLocalTime().Should().BeCloseTo(expectedSignal.IntrinsicTime, 10.Seconds());
        result.IntrinsicTimeGroupId.Should().Be(expectedSignal.IntrinsicTimeGroupId);
        result.IntrinsicTimeLength.Should().Be(expectedSignal.IntrinsicTimeLength);
        result.IntrinsicPrice.Should().Be(expectedSignal.IntrinsicPrice);
        result.IntrinsicTimeTrend.Should().Be(expectedSignal.IntrinsicTimeTrend);
        result.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
        result.TrendPrice.Should().Be(expectedSignal.TrendPrice);
        result.TrendExtreme.Should().Be(expectedSignal.TrendExtreme);
        result.TrendReversal.Should().Be(expectedSignal.TrendReversal);
        result.Lambda.Should().Be(expectedSignal.Lambda);
        result.TargetDelta.Should().Be(expectedSignal.TargetDelta);
        result.TrendDelta.Should().Be(expectedSignal.TrendDelta);
        result.UpTrendTrigger.Should().Be(expectedSignal.UpTrendTrigger);
        result.DownTrendTrigger.Should().Be(expectedSignal.DownTrendTrigger);
        result.TradeState.Should().Be(expectedSignal.TradeState);
    }

    /// <summary>
    /// Unit test for GetLastFuturesOptionTickDataAsync method using sample data and asserting each expected value.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesOptionTickDataAsync_ReturnsExpectedResultWithCorrectValues()
    {
        // Arrange
        var entityId = SampleData.FuturesOptionTickData.EntityId;
        var expectedData = SampleData.FuturesOptionTickData;

        await TestFixture.DevDatabase.Use($"delete from futures_option_tick_data where contractId = '{expectedData.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesOptionTickDataAsync(expectedData);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesOptionTickDataAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedData.ContractId);
        result.ValueDate.Should().Be(expectedData.ValueDate);
        result.TickId.Should().BeGreaterThan(0);
        result.TickTime.Should().Be(expectedData.TickTime);
        result.OptionPrice.Should().Be(expectedData.OptionPrice);
        result.BidPrice.Should().Be(expectedData.BidPrice);
        result.AskPrice.Should().Be(expectedData.AskPrice);
        result.BidSize.Should().Be(expectedData.BidSize);
        result.AskSize.Should().Be(expectedData.AskSize);
        result.ImpliedVolatility.Should().Be(expectedData.ImpliedVolatility);
        result.UnderlyingPrice.Should().Be(expectedData.UnderlyingPrice);
        result.Delta.Should().Be(expectedData.Delta);
        result.Gamma.Should().Be(expectedData.Gamma);
        result.Vega.Should().Be(expectedData.Vega);
        result.Theta.Should().Be(expectedData.Theta);
        result.Rho.Should().Be(expectedData.Rho);
    }

    /// <summary>
    /// Unit test for GetLastFuturesRsiSignalAsync method using sample data and asserting each expected value.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesRsiSignalAsync_ReturnsExpectedResultWithCorrectValues()
    {
        // Arrange
        var entityId = SampleData.FuturesRsiSignal.EntityId;
        var signalType = SampleData.FuturesRsiSignal.TimePeriod;
        var expectedSignal = SampleData.FuturesRsiSignal;

        await TestFixture.DevDatabase.Use($"delete from futures_rsi_signal where contractId = '{expectedSignal.ContractId}' and valueDate = '{expectedSignal.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesRsiSignalAsync(expectedSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesRsiDailySignalAsync(entityId.ContractId, entityId.TimePeriod, entityId.PeriodLength);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.Timestamp.Should().Be(expectedSignal.Timestamp);
        result.TimePeriod.Should().Be(expectedSignal.TimePeriod);
        result.Price.Should().Be(expectedSignal.Price);
        result.PriceChange.Should().Be(expectedSignal.PriceChange);
        result.PriceGain.Should().Be(expectedSignal.PriceGain);
        result.PriceLoss.Should().Be(expectedSignal.PriceLoss);
        result.AveragePriceGain.Should().Be(expectedSignal.AveragePriceGain);
        result.AveragePriceLoss.Should().Be(expectedSignal.AveragePriceLoss);
        result.RS.Should().Be(expectedSignal.RS);
        result.RSI.Should().Be(expectedSignal.RSI);
        result.RSIAverage.Should().Be(expectedSignal.RSIAverage);
        result.RSISlope.Should().Be(expectedSignal.RSISlope);
    }

    /// <summary>
    /// Unit test for GetLastFuturesTdiSignalAsync method using sample data and asserting each expected value.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesTdiSignalAsync_ReturnsExpectedResultWithCorrectValues()
    {
        // Arrange
        var entityId = SampleData.FuturesTdiSignal.EntityId;
        var expectedSignal = SampleData.FuturesTdiSignal;

        await TestFixture.DevDatabase.Use($"delete from futures_tdi_signal where contractId = '{expectedSignal.ContractId}' and valueDate = '{expectedSignal.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesTdiSignalAsync(expectedSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesTdiSignalAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.Timestamp.Hour.Should().Be(expectedSignal.Timestamp.Hour);
        result.Timestamp.Minute.Should().Be(expectedSignal.Timestamp.Minute);
        result.UpTrendCount.Should().Be(expectedSignal.UpTrendCount);
        result.DownTrendCount.Should().Be(expectedSignal.DownTrendCount);
        result.TDI.Should().Be(expectedSignal.TDI);
        result.TDIStrength.Should().Be(expectedSignal.TDIStrength);
    }

    /// <summary>
    /// Unit test for GetLastFuturesTradeSignalAsync method using sample data and asserting each expected value.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesTradeSignalAsync_ReturnsExpectedResultWithCorrectValues()
    {
        // Arrange
        var entityId = SampleData.FuturesTradeSignal.EntityId;
        var expectedSignal = SampleData.FuturesTradeSignal;

        await TestFixture.DevDatabase.Use($"delete from futures_trade_signal where contractId = '{expectedSignal.ContractId}' and valueDate = '{expectedSignal.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesTradeSignalAsync(expectedSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesTradeSignalAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.SequenceId.Should().BeGreaterThan(0);
        result.Timestamp.Hour.Should().Be(expectedSignal.Timestamp.Hour);
        result.Timestamp.Minute.Should().Be(expectedSignal.Timestamp.Minute);
        result.Mean.Should().Be(expectedSignal.Mean);
        result.StdDev.Should().Be(expectedSignal.StdDev);
        result.FuturesPrice.Should().Be(expectedSignal.FuturesPrice);
        result.PriceChangePercent.Should().Be(expectedSignal.PriceChangePercent);
        result.FundRiskPercent.Should().Be(expectedSignal.FundRiskPercent);
        result.RSI.Should().Be(expectedSignal.RSI);
        result.RSISlope.Should().Be(expectedSignal.RSISlope);
        result.TrendType.Should().Be(expectedSignal.TrendType);
        result.TrendStrength.Should().Be(expectedSignal.TrendStrength);
        result.TradeSignal.Should().Be(expectedSignal.TradeSignal);
        result.TDI.Should().Be(expectedSignal.TDI);
        result.TDIStrength.Should().Be(expectedSignal.TDIStrength);
        result.MDI.Should().Be(expectedSignal.MDI);
        result.MDITrend.Should().Be(expectedSignal.MDITrend);
        result.MDIUpTrendLimit.Should().Be(expectedSignal.MDIUpTrendLimit);
        result.MDIDownTrendLimit.Should().Be(expectedSignal.MDIDownTrendLimit);
        result.UpTrendingTrigger.Should().Be(expectedSignal.UpTrendingTrigger);
        result.DownTrendingTrigger.Should().Be(expectedSignal.DownTrendingTrigger);
        result.EntryTrigger.Should().Be(expectedSignal.EntryTrigger);
        result.ExitTrigger.Should().Be(expectedSignal.ExitTrigger);
        result.TrendDelta.Should().Be(expectedSignal.TrendDelta);
        result.TrendExtreme.Should().Be(expectedSignal.TrendExtreme);
        result.TrendReversal.Should().Be(expectedSignal.TrendReversal);
        result.FiftyDMA.Should().Be(expectedSignal.FiftyDMA);
        result.TwoHundredDMA.Should().Be(expectedSignal.TwoHundredDMA);
        result.TradeExecuteState.Should().Be(expectedSignal.TradeExecuteState);
    }

    /// <summary>
    /// Unit test for GetLastFuturesTradeSignalBySymbolAsync method using sample data and asserting each expected value.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesTradeSignalBySymbolAsync_ReturnsExpectedResultWithCorrectValues()
    {
        // Arrange
        var symbol = SampleData.FuturesContract1.Symbol;
        var valueDate = SampleData.FuturesTradeSignal.ValueDate;
        var expectedSignal = SampleData.FuturesTradeSignal;

        await TestFixture.SecDatabase.Use($"delete from futures_contract where contractId in ('{SampleData.FuturesContract1.ContractId}','{SampleData.FuturesContract2.ContractId}')").ExecuteCommandAsync();
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract1);
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract2);

        await TestFixture.DevDatabase.Use($"delete from futures_trade_signal where contractId = '{expectedSignal.ContractId}' and valueDate = '{expectedSignal.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesTradeSignalAsync(expectedSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesTradeSignalBySymbolAsync(symbol, valueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.SequenceId.Should().BeGreaterThan(0);
        result.Timestamp.Hour.Should().Be(expectedSignal.Timestamp.Hour);
        result.Timestamp.Minute.Should().Be(expectedSignal.Timestamp.Minute);
        result.Mean.Should().Be(expectedSignal.Mean);
        result.StdDev.Should().Be(expectedSignal.StdDev);
        result.FuturesPrice.Should().Be(expectedSignal.FuturesPrice);
        result.PriceChangePercent.Should().Be(expectedSignal.PriceChangePercent);
        result.FundRiskPercent.Should().Be(expectedSignal.FundRiskPercent);
        result.RSI.Should().Be(expectedSignal.RSI);
        result.RSISlope.Should().Be(expectedSignal.RSISlope);
        result.TrendType.Should().Be(expectedSignal.TrendType);
        result.TrendStrength.Should().Be(expectedSignal.TrendStrength);
        result.TradeSignal.Should().Be(expectedSignal.TradeSignal);
        result.TDI.Should().Be(expectedSignal.TDI);
        result.TDIStrength.Should().Be(expectedSignal.TDIStrength);
        result.MDI.Should().Be(expectedSignal.MDI);
        result.MDITrend.Should().Be(expectedSignal.MDITrend);
        result.MDIUpTrendLimit.Should().Be(expectedSignal.MDIUpTrendLimit);
        result.MDIDownTrendLimit.Should().Be(expectedSignal.MDIDownTrendLimit);
        result.UpTrendingTrigger.Should().Be(expectedSignal.UpTrendingTrigger);
        result.DownTrendingTrigger.Should().Be(expectedSignal.DownTrendingTrigger);
        result.EntryTrigger.Should().Be(expectedSignal.EntryTrigger);
        result.ExitTrigger.Should().Be(expectedSignal.ExitTrigger);
        result.TrendDelta.Should().Be(expectedSignal.TrendDelta);
        result.TrendExtreme.Should().Be(expectedSignal.TrendExtreme);
        result.TrendReversal.Should().Be(expectedSignal.TrendReversal);
        result.FiftyDMA.Should().Be(expectedSignal.FiftyDMA);
        result.TwoHundredDMA.Should().Be(expectedSignal.TwoHundredDMA);
        result.TradeExecuteState.Should().Be(expectedSignal.TradeExecuteState);
    }

    /// <summary>
    /// Unit test for GetLastRateOfReturnAsync method using sample data and asserting each expected value.
    /// </summary>
    [Fact]
    public async Task GetLastRateOfReturnAsync_ReturnsExpectedResultWithCorrectValues()
    {
        // Arrange
        var expectedRateOfReturn = SampleData.RateOfReturn;
        var symbol = expectedRateOfReturn.Symbol;

        await TestFixture.DevDatabase.Use($"delete from rate_of_return where symbol = '{symbol}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertRateOfReturnAsync(expectedRateOfReturn);

        // Act
        var result = await TestFixture.DevDatabase.GetLastRateOfReturnAsync(symbol);

        // Assert
        result.Should().NotBeNull();
        result.Symbol.Should().Be(expectedRateOfReturn.Symbol);
        result.ValueDate.Should().Be(expectedRateOfReturn.ValueDate);
        result.RateOfReturn.Should().Be(expectedRateOfReturn.RateOfReturn);
    }

    /// <summary>
    /// Unit test for GetLastYieldCurveRateAsync method.
    /// </summary>
    [Fact]
    public async Task GetLastYieldCurveRateAsync_ReturnsExpectedResults()
    {
        // Arrange
        var expectedRate = SampleData.YieldCurveRate;
        await TestFixture.DevDatabase.Use($"DELETE FROM yield_curve_rates WHERE id = 1 and valueDate = '{expectedRate.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertYieldCurveRateAsync(expectedRate);

        // Act
        var result = await TestFixture.DevDatabase.GetLastYieldCurveRateAsync();

        // Assert
        result.Should().NotBeNull();
        result.ValueDate.Should().Be(expectedRate.ValueDate);
        result.OneMonth.Should().Be(expectedRate.OneMonth);
        result.TwoMonth.Should().Be(expectedRate.TwoMonth);
        result.ThreeMonth.Should().Be(expectedRate.ThreeMonth);
        result.SixMonth.Should().Be(expectedRate.SixMonth);
        result.OneYear.Should().Be(expectedRate.OneYear);
        result.TwoYear.Should().Be(expectedRate.TwoYear);
        result.ThreeYear.Should().Be(expectedRate.ThreeYear);
        result.FiveYear.Should().Be(expectedRate.FiveYear);
        result.SevenYear.Should().Be(expectedRate.SevenYear);
        result.TenYear.Should().Be(expectedRate.TenYear);
        result.TwentyYear.Should().Be(expectedRate.TwentyYear);
        result.ThirtyYear.Should().Be(expectedRate.ThirtyYear);
    }

    /// <summary>
    /// Unit test for DeleteYieldCurveRateAsync method.
    /// </summary>
    [Fact]
    public async Task DeleteYieldCurveRateAsync_DeletesExpectedRecord()
    {
        // Arrange
        var valueDate = SampleData.YieldCurveRate.ValueDate;
        await TestFixture.DevDatabase.Use($"DELETE FROM yield_curve_rates WHERE id = 1").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertYieldCurveRateAsync(SampleData.YieldCurveRate);

        // Act
        await TestFixture.DevDatabase.DeleteYieldCurveRateAsync(valueDate);

        // Assert
        var result = await TestFixture.DevDatabase.GetLastYieldCurveRateAsync();
        result.Should().BeNull();
    }

    /// <summary>
    /// Unit test for GetYieldCurveRateExistsAsync method.
    /// </summary>
    [Fact]
    public async Task GetYieldCurveRateExistsAsync_ReturnsExpectedResults()
    {
        // Arrange
        var valueDate = SampleData.YieldCurveRate.ValueDate;
        await TestFixture.DevDatabase.Use($"DELETE FROM yield_curve_rates WHERE id = 1 and valueDate = '{SampleData.YieldCurveRate.ValueDate}'").ExecuteCommandAsync();

        // Act and Assert for non-existing record
        bool result = await TestFixture.DevDatabase.GetYieldCurveRateExistsAsync(valueDate);
        result.Should().BeFalse();

        // Arrange for existing record
        await TestFixture.DevDatabase.InsertYieldCurveRateAsync(SampleData.YieldCurveRate);

        // Act and Assert for existing record
        result = await TestFixture.DevDatabase.GetYieldCurveRateExistsAsync(valueDate);
        result.Should().BeTrue();
    }

    /// <summary>
    /// Unit test for GetYieldCurveRateYearsAsync method.
    /// </summary>
    [Fact]
    public async Task GetYieldCurveRateYearsAsync_ReturnsExpectedResults()
    {
        // Arrange
        var expectedYear = SampleData.YieldCurveRate.ValueDate.Year; // Example year from SampleData.YieldCurveRateReadModel or any other known year data point
        await TestFixture.DevDatabase.Use($"DELETE FROM market_holiday WHERE currencyType = '{CurrencyType.USD}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"DELETE FROM yield_curve_rates WHERE id = 1 and valueDate = '{SampleData.YieldCurveRate.ValueDate}'")
             .ExecuteCommandAsync();

        // Insert sample data for the expected year
        var yieldCurveRate = SampleData.YieldCurveRate;
        await TestFixture.DevDatabase.InsertYieldCurveRateAsync(yieldCurveRate);

        // Act
        var result = await TestFixture.DevDatabase.GetYieldCurveRateYearsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain(expectedYear);
    }

    /// <summary>
    /// Unit test for GetYieldCurveRatesAsync method.
    /// </summary>
    [Fact]
    public async Task GetYieldCurveRatesAsync_ReturnsExpectedResults()
    {
        // Arrange
        var startDate = new DateOnly(SampleData.YieldCurveRate.ValueDate.Year, 1, 1);
        var endDate = new DateOnly(SampleData.YieldCurveRate.ValueDate.Year, 12, 31);

        // Clear any existing data for the date range
        await TestFixture.DevDatabase.Use($"DELETE FROM yield_curve_rates WHERE id = 1 and valueDate = '{SampleData.YieldCurveRate.ValueDate}'")
             .ExecuteCommandAsync();

        // Insert sample data for the specified date range
        var yieldCurveRate = SampleData.YieldCurveRate;
        await TestFixture.DevDatabase.InsertYieldCurveRateAsync(yieldCurveRate);

        // Act
        var result = await TestFixture.DevDatabase.GetYieldCurveRatesAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        var rate = result.First();
        rate.ValueDate.Should().Be(yieldCurveRate.ValueDate);
        rate.OneMonth.Should().Be(yieldCurveRate.OneMonth);
        rate.TwoMonth.Should().Be(yieldCurveRate.TwoMonth);
        rate.ThreeMonth.Should().Be(yieldCurveRate.ThreeMonth);
        rate.SixMonth.Should().Be(yieldCurveRate.SixMonth);
        rate.OneYear.Should().Be(yieldCurveRate.OneYear);
        rate.TwoYear.Should().Be(yieldCurveRate.TwoYear);
        rate.ThreeYear.Should().Be(yieldCurveRate.ThreeYear);
        rate.FiveYear.Should().Be(yieldCurveRate.FiveYear);
        rate.SevenYear.Should().Be(yieldCurveRate.SevenYear);
        rate.TenYear.Should().Be(yieldCurveRate.TenYear);
        rate.TwentyYear.Should().Be(yieldCurveRate.TwentyYear);
        rate.ThirtyYear.Should().Be(yieldCurveRate.ThirtyYear);
    }

    /// <summary>
    /// Unit test for GetMarketHolidaysAsync method.
    /// </summary>
    [Fact]
    public async Task GetMarketHolidaysAsync_ReturnsExpectedResults()
    {
        // Arrange
        var expectedCurrency = CurrencyType.USD;
        var marketHoliday1 = SampleData.MarketHoliday1;
        var marketHoliday2 = SampleData.MarketHoliday2;

        await TestFixture.DevDatabase.Use($"DELETE FROM market_holiday WHERE currencyType = '{expectedCurrency}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertMarketHolidayAsync(marketHoliday1);
        await TestFixture.DevDatabase.DbWriter.InsertMarketHolidayAsync(marketHoliday2);

        // Act
        var result = await TestFixture.DevDatabase.GetMarketHolidaysAsync(expectedCurrency);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result.Should().ContainEquivalentOf(marketHoliday1);
        result.Should().ContainEquivalentOf(marketHoliday2);
    }

    /// <summary>
    /// Unit test for GetMarketHolidaysByDateRangeAsync method.
    /// </summary>
    [Fact]
    public async Task GetMarketHolidaysByDateRangeAsync_ReturnsExpectedResults()
    {
        // Arrange
        var expectedCurrency = CurrencyType.USD;
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 31);
        var marketHoliday1 = SampleData.MarketHoliday1 with { HolidayDate = startDate };
        var marketHoliday2 = SampleData.MarketHoliday2 with { HolidayDate = endDate };

        await TestFixture.DevDatabase.Use($"DELETE FROM market_holiday WHERE currencyType = '{expectedCurrency}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.DbWriter.InsertMarketHolidayAsync(marketHoliday1);
        await TestFixture.DevDatabase.DbWriter.InsertMarketHolidayAsync(marketHoliday2);

        // Act
        var result = await TestFixture.DevDatabase.GetMarketHolidaysByDateRangeAsync(expectedCurrency, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(2);
        result.Should().ContainEquivalentOf(marketHoliday1);
        result.Should().ContainEquivalentOf(marketHoliday2);
    }

    /// <summary>
    /// Unit test for GetTradingDaysAsync method.
    /// </summary>
    [Fact]
    public async Task GetTradingDaysAsync_ReturnsExpectedResults()
    {
        // Arrange
        var startDate = new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 1);
        var endDate = new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 31);
        var marketType = MarketType.Futures;
        var currencyType = CurrencyType.USD;
        var expectedTradingDaysCount = 22; // Example count

        await TestFixture.DevDatabase.DeleteMarketHolidaysAsync(currencyType);
        await TestFixture.DevDatabase.InsertMarketHolidayAsync(SampleData.MarketHoliday1);
        await TestFixture.DevDatabase.InsertMarketHolidayAsync(SampleData.MarketHoliday2);

        // Act
        var result = await TestFixture.DevDatabase.GetTradingDaysAsync(startDate, endDate, marketType, currencyType);

        // Assert
        result.Should().Be(expectedTradingDaysCount);
    }

    /// <summary>
    /// Unit test for GetTradingDatesAsync method.
    /// </summary>
    [Fact]
    public async Task GetTradingDatesAsync_ReturnsExpectedResults()
    {
        // Arrange
        var startDate = new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 1);
        var endDate = new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 31);
        var marketType = MarketType.Futures;
        var currencyType = CurrencyType.USD;
        var expectedTradingDates = new[]
        {
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 2),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 3),

        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 6),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 7),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 8),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 9),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 10),

        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 13),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 14),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 15),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 16),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 17),

        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 20),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 21),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 22),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 23),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 24),

        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 27),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 28),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 29),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 30),
        new DateOnly(SampleData.MarketHoliday1.HolidayDate.Year, 1, 31),
    };

        await TestFixture.DevDatabase.DeleteMarketHolidaysAsync(currencyType);
        await TestFixture.DevDatabase.InsertMarketHolidayAsync(SampleData.MarketHoliday1);
        await TestFixture.DevDatabase.InsertMarketHolidayAsync(SampleData.MarketHoliday2);

        // Act
        var result = await TestFixture.DevDatabase.GetTradingDatesAsync(startDate, endDate, marketType, currencyType);

        // Assert
        result.Should().BeEquivalentTo(expectedTradingDates);
    }

    [Fact]
    [Trait("read futures eod data from CSV file and insert into database", "FundDb")]
    public async Task GetFuturesEodDataFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = TestFixture.DevDatabase;
        var dbMarketData = db as IMarketDataDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\futures_eod_data.csv"))
           .ReadAsync(MapToFuturesEodData, async reducer =>
           {
               await db.Use($"truncate futures_eod_data").ExecuteCommandAsync();
               rowCount = await dbMarketData.InsertFuturesEodDataAsync(reducer);
           });

        var resultSet = await dbMarketData.GetFuturesEodDataAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static FuturesEodDataV2ReadModel MapToFuturesEodData(string e, int o)
            => new(
                contractId: e.GetString(ref o),
                valueDate: e.GetDateOnly(ref o),
                symbol: e.GetString(ref o),
                openPrice: e.GetDecimal(ref o),
                highPrice: e.GetDecimal(ref o),
                lowPrice: e.GetDecimal(ref o),
                closePrice: e.GetDecimal(ref o),
                volume: e.GetInt(ref o),
                dailyPercentChange: e.GetDouble(ref o),
                dailyStdDev: e.GetDouble(ref o),
                dailyStdDevAmount: e.GetDouble(ref o),
                upperBand: e.GetDouble(ref o),
                mean: e.GetDouble(ref o),
                lowerBand: e.GetDouble(ref o),
                marketDirection: e.GetEnum<MarketDirectionType>(ref o),
                marketVolatility: e.GetEnum<MarketVolatilityType>(ref o),
                priceDirection: e.GetEnum<PriceDirectionType>(ref o),
                priceVolatility: e.GetEnum<PriceVolatilityType>(ref o),
                marketDirectionIndicator: e.GetDouble(ref o),
                windowSize: e.GetInt(ref o)
            );


    }

    [Fact]
    [Trait("read futures bar data from CSV file and insert into database", "FundDb")]
    public async Task GetFuturesBarDataFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = TestFixture.DevDatabase;
        var dbMarketData = db as IMarketDataDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\futures_bar_data.csv"))
           .ReadAsync(MapToFuturesBarData, async reducer =>
           {
               await db.Use($"truncate futures_bar_data").ExecuteCommandAsync();
               rowCount = await dbMarketData.InsertFuturesBarDataAsync(reducer);
           });

        var resultSet = await dbMarketData.GetFuturesBarDataAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static FuturesBarDataReadModel MapToFuturesBarData(string e, int o)
            => new(
                contractId: e.GetString(ref o),
                symbol: e.GetString(ref o),
                valueDate: e.GetDateOnly(ref o),
                barDate: e.GetDateTime(ref o),
                barRateType: e.GetEnum<BarRateType>(ref o),
                barValue: e.GetDecimal(ref o),
                upTrendTrigger: e.GetDouble(ref o),
                downTrendTrigger: e.GetDouble(ref o)
            );

    }

    [Fact]
    [Trait("read futures trade signal from CSV file and insert into database", "FundDb")]
    public async Task GetFuturesTradeSignalsFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = TestFixture.DevDatabase;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\futures_trade_signal.csv"))
           .ReadAsync(MapToFuturesTradeSignal,  async futureTradeSignals => {
               await db.Use($"truncate futures_trade_signal").ExecuteCommandAsync();
               rowCount = await db.InsertFuturesTradeSignalsAsync(futureTradeSignals);
           });
        var resultSet = await db.GetFuturesTradeSignalsAsync();
        Assert.NotNull(resultSet);
        Assert.Equal(rowCount, resultSet.Count);

        static FuturesTradeSignalV2ReadModel MapToFuturesTradeSignal(string e, int start)
            => new(
                contractId: e.GetString(ref start),
                valueDate: e.GetDateOnly(ref start),
                timePeriod: TradeTimePeriodType.FifteenSeconds,
                sequenceId: e.GetLong(ref start),
                timestamp: e.GetTimeOnly(ref start),
                mean: e.GetDouble(ref start),
                stdDev: e.GetDouble(ref start),
                futuresPrice: e.GetDouble(ref start),
                priceChangePercent: e.GetDouble(ref start),
                fundRiskPercent: e.GetDouble(ref start),
                rsi: e.GetDouble(ref start),
                rsiSlope: e.GetDouble(ref start),
                trendType: e.GetEnum<FuturesTrendType>(ref start),
                trendStrength: e.GetEnum<FuturesTrendStrengthType>(ref start),
                tradeSignal: e.GetEnum<TradeSignalType>(ref start),
                tdi: e.GetEnum<FuturesTrendDirectionType>(ref start),
                tdiStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(ref start),
                mdi: e.GetDouble(ref start),
                mdiTrend: e.GetEnum<FuturesMDITrendType>(ref start),
                mdiUpTrendLimit: e.GetDouble(ref start),
                mdiDownTrendLimit: e.GetDouble(ref start),
                upTrendingTrigger: e.GetDouble(ref start),
                downTrendingTrigger: e.GetDouble(ref start),
                entryTrigger: e.GetDouble(ref start),
                exitTrigger: e.GetDouble(ref start),
                trendDelta: e.GetDouble(ref start),
                trendExtreme: e.GetDouble(ref start),
                trendReversal: e.GetDouble(ref start),
                fiftyDMA: e.GetDecimal(ref start),
                twoHundredDMA: e.GetDecimal(ref start),
                tradeExecuteState: e.GetEnum<TradeExecuteState>(ref start));
        }
            
    }
