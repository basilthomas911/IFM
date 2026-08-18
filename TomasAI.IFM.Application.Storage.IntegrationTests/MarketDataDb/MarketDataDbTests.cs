using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using Xunit;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

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

    public Storage.MarketDataDb.MarketDataDbContext DevDatabase { get; private set; }
    public Storage.SecuritiesDb.SecuritiesDbContext SecDatabase { get; private set; }
    public Storage.PredictiveModelDb.PredictiveModelDbContext PMDatabase { get; private set; }
    public Storage.MarketDataDb.MarketDataDbContext ProdDatabase { get; private set; }
    public Storage.SequenceIdDb.SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }

    void SetDevDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("MarketDataDbConnection", "Contact Points=localhost;Port=9042;Default Keyspace=market_data_test_db", "System.Data.ScyllaDb");
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
        new TomasAI.IFM.Application.Storage.MarketDataDb.Schema.MarketDataSchemaDb(dbConn, logger)
            .CreateAllAsync().GetAwaiter().GetResult();
        diContainer.Add(typeof(IObjectRepository<Storage.MarketDataDb.MarketDataDbContext>), new Storage.MarketDataDb.MarketDataDbContext(dbConn, dbFactory, blackboardService, SequenceIdGenerator, logger));
        diContainer.Add(typeof(IObjectRepository<SecuritiesDbContext>), SecDatabase );

        DevDatabase = dbFactory.MarketDataDb as Storage.MarketDataDb.MarketDataDbContext;
    }

    void SetPMDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("PredictiveModelDbConnection", "Contact Points=localhost;Port=9042;Default Keyspace=predictive_model_test_db", "System.Data.ScyllaDb");
        var diContainer = new Dictionary<Type, Storage.PredictiveModelDb.PredictiveModelDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        new TomasAI.IFM.Application.Storage.PredictiveModelDb.Schema.PredictiveModelSchemaDb(dbConn, logger)
            .CreateAllAsync().GetAwaiter().GetResult();
        diContainer.Add(typeof(IObjectRepository<Storage.PredictiveModelDb.PredictiveModelDbContext>), new Storage.PredictiveModelDb.PredictiveModelDbContext(dbConn, dbFactory, logger));
        PMDatabase = dbFactory.PredictiveModelDb as Storage.PredictiveModelDb.PredictiveModelDbContext;
    }

    void SetProdDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("MarketDataDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True;TrustServerCertificate=True", "System.Data.SqlClient");
        var diContainer = new Dictionary<Type, Storage.MarketDataDb.MarketDataDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var dbFactory = new DbContextFactory(dbResolver);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var redisCache = Substitute.For<IRedisCache>();
        redisCache.When(_ => { }).Do(_ => { });
        var blackboardService = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        diContainer.Add(typeof(IObjectRepository<Storage.MarketDataDb.MarketDataDbContext>), new Storage.MarketDataDb.MarketDataDbContext(dbConn, dbFactory, blackboardService, SequenceIdGenerator, logger));
        ProdDatabase = dbFactory.MarketDataDb as Storage.MarketDataDb.MarketDataDbContext;
    }

    void SetSeqIdDatabase()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection", "Host=localhost;Port=5432;Database=sequence-id-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, SequenceIdDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<SequenceIdDbContext>), new SequenceIdDbContext(dbConn, dbFactory, logger));
        SeqIdDatabase  = dbFactory.SequenceIdDb as SequenceIdDbContext;
        SequenceIdDatabaseInitializer.EnsureInitialized(new TomasAI.IFM.Application.Storage.SequenceIdDb.Schema.SequenceIdSchemaDb(dbConn, logger));
        SequenceIdGenerator = new PostgresSequenceIdGenerator(dbFactory.SequenceIdDb as SequenceIdDbContext);
        
    }

    void SetSecDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("SecuritiesDbConnection", "Contact Points=localhost;Port=9042;Default Keyspace=securities_test_db", "System.Data.ScyllaDb");
        var diContainer = new Dictionary<Type, SecuritiesDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var dbFactory  = new DbContextFactory(dbResolver);
        new TomasAI.IFM.Application.Storage.SecuritiesDb.Schema.SecuritiesSchemaDb(dbConn, logger)
            .CreateAllAsync().GetAwaiter().GetResult();
        diContainer.Add(typeof(IObjectRepository<SecuritiesDbContext>), new SecuritiesDbContext(dbConn, dbFactory, logger));
        SecDatabase = dbFactory.SecuritiesDb as SecuritiesDbContext;
    }

}

public class MarketDataDbTests(MarketDataFixture testFixture) : IClassFixture<MarketDataFixture>
{
    MarketDataFixture TestFixture { get; } = testFixture;

    async Task DeleteFuturesItiSignalsAsync(string contractId, DateOnly? valueDate = null)
    {
        var rows = await TestFixture.DevDatabase
            .Use(MarketDataDbCql.GetFuturesItiSignalsCanonicalByContract)
            .SetParameters(new GetFuturesItiSignalsCanonicalByContract(contractId))
            .ExecuteQueryAsync(record => (
                ValueDate: record.GetDateOnly(1),
                TimePeriod: record.GetEnum<TimeFrameType>(2)));
        foreach (var key in rows
            .Where(row => !valueDate.HasValue || row.ValueDate == valueDate.Value)
            .Distinct())
        {
            await TestFixture.DevDatabase.DeleteFuturesItiSignalAsync(
                contractId, key.ValueDate, key.TimePeriod);
        }
    }

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
        resultData.BarRateType.Should().Be(BarRateType.FifteenSeconds);
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
        var contractId = $"VX_UPSERT_{Guid.NewGuid():N}";
        var futuresEodData = SampleData.VixFuturesEodData with { ContractId = contractId };
        var futuresTickData = SampleData.VixFuturesTickData with { ContractId = contractId };
        var lowTickData = SampleData.VixFuturesTickDataLowPrice with
        {
            ContractId = contractId,
            TickId = futuresTickData.TickId + 1
        };
        var highTickData = SampleData.VixFuturesTickDataHighPrice with
        {
            ContractId = contractId,
            TickId = futuresTickData.TickId + 2
        };
        await TestFixture.DevDatabase.Use($"delete from vix_futures_eod_data where contractId = '{futuresEodData.ContractId}' ").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_tick_data where contractId = '{futuresTickData.ContractId}' and valueDate = '{futuresTickData.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(futuresTickData);
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(highTickData);
        await TestFixture.DevDatabase.InsertFuturesTickDataAsync(lowTickData);
        await TestFixture.DevDatabase.InsertVixFuturesEodDataAsync(futuresTickData);
        var totalVolume = futuresTickData.Size + highTickData.Size + lowTickData.Size + 341;

        // Act: Insert the FuturesClosingPriceReadModel into the database
        await TestFixture.DevDatabase.InsertVixFuturesEodDataAsync(futuresTickData with { Price = 77.50m, Size = 341 });

        // Assert: Verify that the data was inserted by retrieving it and checking the values
        var retrievedData = await TestFixture.DevDatabase.GetVixFuturesEodDataAsync(futuresEodData.EntityId.ContractId, futuresEodData.EntityId.ValueDate);
        retrievedData.Should().NotBeNull();
        retrievedData.ContractId.Should().Be(futuresTickData.ContractId);
        retrievedData.ValueDate.Should().Be(futuresTickData.ValueDate);
        retrievedData.OpenPrice.Should().Be(futuresTickData.Price);
        retrievedData.HighPrice.Should().Be(highTickData.Price);
        retrievedData.LowPrice.Should().Be(lowTickData.Price);
        retrievedData.ClosePrice.Should().Be(77.50m);
        retrievedData.Volume.Should().Be(totalVolume);
    }

    [Fact]
    public async Task GetLastFuturesTickDataByTickDateAsync_UsesTimeProjectionAndHighestTickId()
    {
        const string contractId = "ALLOW_FREE_TICK";
        var valueDate = new DateOnly(2042, 1, 5);
        var tickTime = new TimeOnly(10, 15, 30);
        FuturesTickDataV2ReadModel[] ticks =
        [
            SampleData.FuturesTickData with
            {
                ContractId = contractId,
                ValueDate = valueDate,
                TickId = 4101,
                TickTime = tickTime,
                Price = 101.25m
            },
            SampleData.FuturesTickData with
            {
                ContractId = contractId,
                ValueDate = valueDate,
                TickId = 4102,
                TickTime = tickTime,
                Price = 102.50m
            },
            SampleData.FuturesTickData with
            {
                ContractId = contractId,
                ValueDate = valueDate,
                TickId = 4103,
                TickTime = tickTime.Add(TimeSpan.FromSeconds(1)),
                Price = 103.75m
            }
        ];

        await TestFixture.DevDatabase.DeleteFuturesTickDataAsync(contractId, valueDate);
        try
        {
            await TestFixture.DevDatabase.InsertFuturesTickDataAsync(ticks);

            var result = await TestFixture.DevDatabase.GetLastFuturesTickDataByTickDateAsync(
                contractId,
                valueDate.ToDateTime(tickTime));

            result.Should().NotBeNull();
            result!.TickId.Should().Be(4102);
            result.Price.Should().Be(102.50m);
        }
        finally
        {
            await TestFixture.DevDatabase.DeleteFuturesTickDataAsync(contractId, valueDate);
        }
    }

    [Fact]
    public async Task GetFuturesEodDataAsync_PriorDateRemainsInRequestedContractPartition()
    {
        const string expectedContractId = "ALLOW_FREE_EOD_A";
        const string distractorContractId = "ALLOW_FREE_EOD_B";
        var priorDate = new DateOnly(2042, 1, 7);
        var requestedDate = priorDate.AddDays(1);
        var expected = SampleData.FuturesEodData with
        {
            ContractId = expectedContractId,
            ValueDate = priorDate,
            Symbol = "AFEODA",
            ClosePrice = 201.25m
        };
        var distractor = SampleData.FuturesEodData with
        {
            ContractId = distractorContractId,
            ValueDate = priorDate,
            Symbol = "AFEODB",
            ClosePrice = 999.00m
        };

        await TestFixture.DevDatabase.DeleteFuturesEodDataAsync(expectedContractId, priorDate);
        await TestFixture.DevDatabase.DeleteFuturesEodDataAsync(distractorContractId, priorDate);
        try
        {
            await TestFixture.DevDatabase.InsertFuturesEodDataAsync(new[] { expected, distractor });

            var result = await TestFixture.DevDatabase.GetFuturesEodDataAsync(expectedContractId, requestedDate);

            result.Should().NotBeNull();
            result!.ContractId.Should().Be(expectedContractId);
            result.ValueDate.Should().Be(priorDate);
            result.ClosePrice.Should().Be(201.25m);
        }
        finally
        {
            await TestFixture.DevDatabase.DeleteFuturesEodDataAsync(expectedContractId, priorDate);
            await TestFixture.DevDatabase.DeleteFuturesEodDataAsync(distractorContractId, priorDate);
        }
    }

    [Fact]
    public async Task FuturesEodMonthlyProjection_ReadsAcrossMonthsAndFiltersBeforeLimit()
    {
        const string contractId = "ALLOW_FREE_EOD_RANGE";
        const string symbol = "AFRANGE";
        var januaryDate = new DateOnly(2042, 1, 31);
        var februaryDate = new DateOnly(2042, 2, 1);
        var distractorDate = new DateOnly(2042, 2, 2);
        var january = SampleData.FuturesEodData with
        {
            ContractId = contractId,
            ValueDate = januaryDate,
            Symbol = symbol,
            ClosePrice = 301.00m
        };
        var february = SampleData.FuturesEodData with
        {
            ContractId = contractId,
            ValueDate = februaryDate,
            Symbol = symbol,
            ClosePrice = 302.00m
        };
        var distractor = SampleData.FuturesEodData with
        {
            ContractId = contractId,
            ValueDate = distractorDate,
            Symbol = "OTHER",
            ClosePrice = 999.00m
        };

        foreach (var valueDate in new[] { januaryDate, februaryDate, distractorDate })
            await TestFixture.DevDatabase.DeleteFuturesEodDataAsync(contractId, valueDate);

        try
        {
            await TestFixture.DevDatabase.InsertFuturesEodDataAsync(new[] { january, february, distractor });

            var range = await TestFixture.DevDatabase.GetCurrentFuturesEodDataByDateRangeAsync(
                januaryDate,
                distractorDate);
            var matchingRange = range.Where(e => e.ContractId == contractId && e.Symbol == symbol).ToArray();
            matchingRange.Should().HaveCount(2);
            matchingRange.Select(e => e.ValueDate).Should().BeEquivalentTo(new[] { januaryDate, februaryDate });

            var closingPrices = await TestFixture.DevDatabase.GetFuturesEodClosingPricesAsync(
                contractId,
                symbol,
                januaryDate,
                distractorDate,
                maxDays: 1);
            closingPrices.Should().ContainSingle();
            closingPrices.Single().ValueDate.Should().Be(februaryDate);
            closingPrices.Single().ClosingPrice.Should().Be(302.00m);
        }
        finally
        {
            foreach (var valueDate in new[] { januaryDate, februaryDate, distractorDate })
                await TestFixture.DevDatabase.DeleteFuturesEodDataAsync(contractId, valueDate);
        }
    }

    [Fact]
    public async Task GetVixFuturesEodDataByValueDateAsync_ReturnsEveryRowThroughDate()
    {
        const string contractA = "ALLOW_FREE_VIX_A";
        const string contractB = "ALLOW_FREE_VIX_B";
        var firstDate = new DateOnly(2042, 3, 1);
        var asOfDate = new DateOnly(2042, 3, 2);
        var futureDate = new DateOnly(2042, 3, 3);
        var rows = new[]
        {
            SampleData.VixFuturesTickData with
            {
                ContractId = contractA,
                ValueDate = firstDate,
                Price = 21.00m,
                Size = 100
            },
            SampleData.VixFuturesTickData with
            {
                ContractId = contractA,
                ValueDate = asOfDate,
                Price = 22.00m,
                Size = 200
            },
            SampleData.VixFuturesTickData with
            {
                ContractId = contractA,
                ValueDate = futureDate,
                Price = 23.00m,
                Size = 300
            },
            SampleData.VixFuturesTickData with
            {
                ContractId = contractB,
                ValueDate = asOfDate,
                Price = 22.00m,
                Size = 200
            }
        };

        foreach (var row in rows)
            await TestFixture.DevDatabase.DeleteVixFuturesEodDataAsync(row.ContractId, row.ValueDate);

        try
        {
            foreach (var row in rows)
                await TestFixture.DevDatabase.InsertVixFuturesEodDataAsync(row);

            var result = await TestFixture.DevDatabase.GetVixFuturesEodDataByValueDateAsync(asOfDate);
            var matching = result.Where(e => e.ContractId == contractA || e.ContractId == contractB).ToArray();

            matching.Should().HaveCount(3);
            matching.Where(e => e.ContractId == contractA)
                .Select(e => e.ValueDate)
                .Should().Equal(asOfDate, firstDate);
            matching.Single(e => e.ContractId == contractA && e.ValueDate == firstDate)
                .ClosePrice.Should().Be(21.00m);
            matching.Single(e => e.ContractId == contractB).ValueDate.Should().Be(asOfDate);
            matching.Should().NotContain(e => e.ValueDate == futureDate);
        }
        finally
        {
            foreach (var row in rows)
                await TestFixture.DevDatabase.DeleteVixFuturesEodDataAsync(row.ContractId, row.ValueDate);
        }
    }

    [Fact]
    public async Task BackfillQueryProjectionsV2Async_ReconcilesIdentitiesAndCompletesCutover()
    {
        var result = await TestFixture.DevDatabase.BackfillQueryProjectionsV2Async(batchSize: 64);
        var readiness = await TestFixture.DevDatabase.GetQueryProjectionReadinessAsync();

        result.IsReconciled.Should().BeTrue();
        result.CutoverCompleted.Should().BeTrue(
            $"readiness was tick={readiness.FuturesTickByTime}, eod={readiness.FuturesEodByMonth}, " +
            $"vix={readiness.VixFuturesContractIndex}, iti={readiness.FuturesItiSignalQueries}");
        result.FuturesTicksProjected.Should().Be(result.FuturesTicksSource);
        result.FuturesTicksProjectedFingerprint.Should().Be(result.FuturesTicksSourceFingerprint);
        result.FuturesEodRowsProjected.Should().Be(result.FuturesEodRowsSource);
        result.FuturesEodProjectedFingerprint.Should().Be(result.FuturesEodSourceFingerprint);
        result.VixContractsIndexed.Should().Be(result.VixContractsSource);
        result.VixContractsIndexedFingerprint.Should().Be(result.VixContractsSourceFingerprint);
        result.FuturesItiSignalsByDayProjected.Should().Be(result.FuturesItiSignalsSource);
        result.FuturesItiSignalsByMonthProjected.Should().Be(result.FuturesItiSignalsSource);
        result.FuturesItiSignalsByTrendModeProjected.Should().Be(result.FuturesItiSignalsSource);
        result.FuturesItiSignalsByDayFingerprint.Should().Be(result.FuturesItiSignalsSourceFingerprint);
        result.FuturesItiSignalsByMonthFingerprint.Should().Be(result.FuturesItiSignalsSourceFingerprint);
        result.FuturesItiSignalsByTrendModeFingerprint.Should().Be(result.FuturesItiSignalsSourceFingerprint);
        readiness.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task BackfillQueryProjectionsV2Async_WithGuardConflict_DoesNotPublishReadiness()
    {
        var db = TestFixture.DevDatabase;
        const string projectionName = "futures_tick_data_by_time";
        const string guardScope = "$guard:0";
        var conflictingOperationId = Guid.NewGuid();
        var conflictingOperations = new HashSet<Guid> { conflictingOperationId };

        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        await db.Use(MarketDataDbCql.InsertMarketDataProjectionScopeMutationV3)
            .SetParameters(new InsertMarketDataProjectionScopeMutationV3(
                projectionName,
                guardScope,
                conflictingOperationId,
                DateTime.UtcNow))
            .ExecuteCommandAsync();
        await db.Use(MarketDataDbCql.BeginMarketDataProjectionScopeOperationV3)
            .SetParameters(new BeginMarketDataProjectionScopeOperationV3(
                projectionName,
                guardScope,
                conflictingOperationId,
                conflictingOperations))
            .ExecuteCommandAsync();

        try
        {
            var conflicted = await db.BackfillQueryProjectionsV2Async(batchSize: 64);
            var readiness = await db.GetQueryProjectionReadinessAsync();

            conflicted.CutoverCompleted.Should().BeFalse();
            readiness.FuturesTickByTime.Should().BeFalse();
        }
        finally
        {
            await db.Use(MarketDataDbCql.RemoveMarketDataProjectionScopeOperationV3)
                .SetParameters(new RemoveMarketDataProjectionScopeOperationV3(
                    projectionName,
                    guardScope,
                    conflictingOperationId))
                .ExecuteCommandAsync();
            await db.Use(MarketDataDbCql.DeleteMarketDataProjectionScopeMutationV3)
                .SetParameters(new DeleteMarketDataProjectionScopeMutationV3(
                    projectionName,
                    guardScope,
                    conflictingOperationId))
                .ExecuteCommandAsync();

            var repaired = await db.BackfillQueryProjectionsV2Async(batchSize: 64);
            repaired.CutoverCompleted.Should().BeTrue();
        }
    }

    [Fact]
    public async Task BackfillQueryProjectionsV2Async_WithUnknownTargetMutationResponse_RemainsUnclassifiedUntilExplicitCutoff()
    {
        var db = TestFixture.DevDatabase;
        string[] projectionNames =
        [
            "futures_tick_data_by_time",
            "futures_eod_data_by_month",
            "vix_futures_contract_index",
            "futures_iti_signal_queries_v2"
        ];

        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        db.ProjectionBackfillTargetMutationSubmittingForTestingAsync = () =>
            Task.FromException(new TimeoutException("Simulated unknown Scylla TRUNCATE response."));

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                db.BackfillQueryProjectionsV2Async(batchSize: 64));

            List<(string ProjectionName, DateTime StartedOn)> globalMarkers = [];
            foreach (var projectionName in projectionNames)
            {
                var markers = await db.Use(MarketDataDbCql.GetMarketDataProjectionMutations)
                    .SetParameters(new GetMarketDataProjectionMutation(projectionName))
                    .ExecuteQueryAsync(record => (
                        ProjectionName: projectionName,
                        StartedOn: record.GetDateTime(1)));
                globalMarkers.AddRange(markers);
            }
            globalMarkers.Should().HaveCount(projectionNames.Length)
                .And.OnlyContain(static marker => marker.StartedOn != DateTime.UnixEpoch);

            var scopedMarkers = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)
                .ExecuteQueryAsync(record => (
                    ProjectionName: record.GetString(0),
                    ScopeKey: record.GetString(1),
                    StartedOn: record.GetDateTime(3)));
            var guardMarkers = scopedMarkers.Where(marker =>
                    projectionNames.Contains(marker.ProjectionName, StringComparer.Ordinal) &&
                    marker.ScopeKey.StartsWith("$guard:", StringComparison.Ordinal))
                .ToArray();
            guardMarkers.Should().HaveCount(projectionNames.Length * 32)
                .And.OnlyContain(static marker => marker.StartedOn != DateTime.UnixEpoch);
            (await db.GetQueryProjectionReadinessAsync()).IsReady.Should().BeFalse();
        }
        finally
        {
            db.ProjectionBackfillTargetMutationSubmittingForTestingAsync = null;
            var repaired = await db.BackfillQueryProjectionsV2Async(
                batchSize: 64,
                staleOperationCutoffUtc: DateTime.UtcNow);
            repaired.CutoverCompleted.Should().BeTrue();
        }
    }

    [Fact]
    public async Task TickGuardRegistration_WithUnknownResponse_RemainsUnclassifiedUntilExplicitCutoff()
    {
        var db = TestFixture.DevDatabase;
        var suffix = Guid.NewGuid().ToString("N");
        var row = SampleData.FuturesTickData with
        {
            ContractId = $"TICK-BEGIN-UNKNOWN-{suffix}",
            ValueDate = new DateOnly(2049, 1, 16),
            TickId = 9_200_001,
            TickTime = new TimeOnly(10, 11, 12)
        };
        var scopeKey = GetTestTickScope(row.ContractId, row.ValueDate);
        var guardScope = $"$guard:{GetTestVixBucket(scopeKey)}";

        await db.DeleteFuturesTickDataAsync(row.ContractId, row.ValueDate);
        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        db.TickProjectionGuardRegistrationForTestingAsync = async registration =>
        {
            await registration();
            throw new TimeoutException("Simulated lost Scylla registration acknowledgement.");
        };

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                db.InsertFuturesTickDataAsync(new[] { row }));

            var states = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)
                .SetParameters(new GetMarketDataProjectionScopeStatesV3(
                    "futures_tick_data_by_time",
                    new[] { guardScope }))
                .ExecuteQueryAsync(record => record.IsCollectionEmpty(5));
            states.Should().ContainSingle().Which.Should().BeFalse();

            var markers = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)
                .ExecuteQueryAsync(record => (
                    ProjectionName: record.GetString(0),
                    ScopeKey: record.GetString(1),
                    StartedOn: record.GetDateTime(3)));
            markers.Where(marker =>
                    marker.ProjectionName == "futures_tick_data_by_time" &&
                    marker.ScopeKey == guardScope)
                .Should().ContainSingle()
                .Which.StartedOn.Should().NotBe(DateTime.UnixEpoch);
        }
        finally
        {
            db.TickProjectionGuardRegistrationForTestingAsync = null;
            var repaired = await db.BackfillQueryProjectionsV2Async(
                batchSize: 64,
                staleOperationCutoffUtc: DateTime.UtcNow);
            repaired.CutoverCompleted.Should().BeTrue();
            await db.DeleteFuturesTickDataAsync(row.ContractId, row.ValueDate);
        }
    }

    [Fact]
    public async Task TickGuardRegistration_WithAcknowledgedPreDataFailure_IsAutomaticallyRecoverable()
    {
        var db = TestFixture.DevDatabase;
        var suffix = Guid.NewGuid().ToString("N");
        var row = SampleData.FuturesTickData with
        {
            ContractId = $"TICK-BEGIN-ACK-{suffix}",
            ValueDate = new DateOnly(2049, 1, 17),
            TickId = 9_200_002,
            TickTime = new TimeOnly(10, 11, 13)
        };
        var scopeKey = GetTestTickScope(row.ContractId, row.ValueDate);
        var guardScope = $"$guard:{GetTestVixBucket(scopeKey)}";

        await db.DeleteFuturesTickDataAsync(row.ContractId, row.ValueDate);
        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        db.TickProjectionGuardRegisteredForTestingAsync = () =>
            Task.FromException(new InvalidOperationException("Known pre-data failure after registration acknowledgement."));

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                db.InsertFuturesTickDataAsync(new[] { row }));

            var markers = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)
                .ExecuteQueryAsync(record => (
                    ProjectionName: record.GetString(0),
                    ScopeKey: record.GetString(1),
                    StartedOn: record.GetDateTime(3)));
            markers.Where(marker =>
                    marker.ProjectionName == "futures_tick_data_by_time" &&
                    marker.ScopeKey == guardScope)
                .Should().ContainSingle()
                .Which.StartedOn.Should().Be(DateTime.UnixEpoch);
        }
        finally
        {
            db.TickProjectionGuardRegisteredForTestingAsync = null;
            var repaired = await db.BackfillQueryProjectionsV2Async(batchSize: 64);
            repaired.CutoverCompleted.Should().BeTrue();
            await db.DeleteFuturesTickDataAsync(row.ContractId, row.ValueDate);
        }
    }

    [Fact]
    public async Task FuturesEodScopeActivation_WithUnknownResponse_RemainsUnclassifiedUntilExplicitCutoff()
    {
        var db = TestFixture.DevDatabase;
        var suffix = Guid.NewGuid().ToString("N");
        var row = SampleData.FuturesEodData with
        {
            ContractId = $"EOD-BEGIN-UNKNOWN-{suffix}",
            Symbol = "EBU",
            ValueDate = new DateOnly(2049, 2, 17)
        };
        const string projectionName = "futures_eod_data_by_month";
        const string scopeKey = "204902";
        var guardScope = $"$guard:{GetTestVixBucket(scopeKey)}";

        await db.DeleteFuturesEodDataAsync(row.ContractId, row.ValueDate);
        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        db.MaintainedProjectionScopeActivationForTestingAsync = async activation =>
        {
            await activation();
            throw new TimeoutException("Simulated lost Scylla scope-Begin acknowledgement.");
        };

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                db.InsertFuturesEodDataAsync(new[] { row }));

            var states = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)
                .SetParameters(new GetMarketDataProjectionScopeStatesV3(
                    projectionName,
                    new[] { scopeKey, guardScope }))
                .ExecuteQueryAsync(record => (
                    Blocked: record.GetBool(4),
                    ActiveOperationsEmpty: record.IsCollectionEmpty(5)));
            states.Should().HaveCount(2)
                .And.OnlyContain(static state => state.Blocked && !state.ActiveOperationsEmpty);

            var markers = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)
                .ExecuteQueryAsync(record => (
                    ProjectionName: record.GetString(0),
                    ScopeKey: record.GetString(1),
                    StartedOn: record.GetDateTime(3)));
            markers.Where(marker =>
                    marker.ProjectionName == projectionName &&
                    (marker.ScopeKey == scopeKey || marker.ScopeKey == guardScope))
                .Should().HaveCount(2)
                .And.OnlyContain(static marker => marker.StartedOn != DateTime.UnixEpoch);
        }
        finally
        {
            db.MaintainedProjectionScopeActivationForTestingAsync = null;
            var repaired = await db.BackfillQueryProjectionsV2Async(
                batchSize: 64,
                staleOperationCutoffUtc: DateTime.UtcNow);
            repaired.CutoverCompleted.Should().BeTrue();
            await db.DeleteFuturesEodDataAsync(row.ContractId, row.ValueDate);
        }
    }

    [Fact]
    public async Task BackfillGlobalActivation_WithUnknownResponse_RemainsUnclassifiedUntilExplicitCutoff()
    {
        var db = TestFixture.DevDatabase;
        const string projectionName = "futures_tick_data_by_time";
        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        db.ProjectionBackfillGlobalActivationForTestingAsync = async activation =>
        {
            await activation();
            throw new TimeoutException("Simulated lost Scylla global-Begin acknowledgement.");
        };

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                db.BackfillQueryProjectionsV2Async(batchSize: 64));

            var markers = await db.Use(MarketDataDbCql.GetMarketDataProjectionMutations)
                .SetParameters(new GetMarketDataProjectionMutation(projectionName))
                .ExecuteQueryAsync(record => record.GetDateTime(1));
            markers.Should().ContainSingle()
                .Which.Should().NotBe(DateTime.UnixEpoch);
            (await db.GetQueryProjectionReadinessAsync()).FuturesTickByTime.Should().BeFalse();
        }
        finally
        {
            db.ProjectionBackfillGlobalActivationForTestingAsync = null;
            var repaired = await db.BackfillQueryProjectionsV2Async(
                batchSize: 64,
                staleOperationCutoffUtc: DateTime.UtcNow);
            repaired.CutoverCompleted.Should().BeTrue();
        }
    }

    [Fact]
    public async Task BackfillScopeActivation_WithUnknownResponse_RemainsUnclassifiedUntilExplicitCutoff()
    {
        var db = TestFixture.DevDatabase;
        const string projectionName = "futures_tick_data_by_time";
        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        db.ProjectionBackfillScopeActivationForTestingAsync = async activation =>
        {
            await activation();
            throw new TimeoutException("Simulated lost Scylla scoped-Begin acknowledgement.");
        };

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                db.BackfillQueryProjectionsV2Async(batchSize: 64));

            var markers = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)
                .ExecuteQueryAsync(record => (
                    ProjectionName: record.GetString(0),
                    ScopeKey: record.GetString(1),
                    StartedOn: record.GetDateTime(3)));
            markers.Where(marker =>
                    marker.ProjectionName == projectionName &&
                    marker.ScopeKey.StartsWith("$guard:", StringComparison.Ordinal))
                .Should().HaveCount(32)
                .And.OnlyContain(static marker => marker.StartedOn != DateTime.UnixEpoch);
            (await db.GetQueryProjectionReadinessAsync()).FuturesTickByTime.Should().BeFalse();
        }
        finally
        {
            db.ProjectionBackfillScopeActivationForTestingAsync = null;
            var repaired = await db.BackfillQueryProjectionsV2Async(
                batchSize: 64,
                staleOperationCutoffUtc: DateTime.UtcNow);
            repaired.CutoverCompleted.Should().BeTrue();
        }
    }

    [Fact]
    public async Task BackfillQueryProjectionsV2Async_WithTickRegisteredBeforeScanAndCommittedAfterReconciliation_DoesNotPublishReadiness()
    {
        var db = TestFixture.DevDatabase;
        var suffix = Guid.NewGuid().ToString("N");
        var row = SampleData.FuturesTickData with
        {
            ContractId = $"FENCED-TICK-{suffix}",
            ValueDate = new DateOnly(2048, 3, 18),
            TickId = 9_100_000,
            TickTime = new TimeOnly(11, 22, 33),
            Price = 412.25m,
            Size = 17
        };
        var scopeKey = GetTestTickScope(row.ContractId, row.ValueDate);
        var guardScope = $"$guard:{GetTestVixBucket(scopeKey)}";
        var tickRegistered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTickData = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backfillReconciled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBackfill = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? tickWrite = null;
        Task<MarketDataProjectionBackfillResult>? backfill = null;

        await db.DeleteFuturesTickDataAsync(row.ContractId, row.ValueDate);
        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        db.TickProjectionGuardRegisteredForTestingAsync = async () =>
        {
            tickRegistered.TrySetResult(true);
            await releaseTickData.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };
        db.ProjectionBackfillReconciledForTestingAsync = async () =>
        {
            backfillReconciled.TrySetResult(true);
            await releaseBackfill.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };

        try
        {
            tickWrite = db.InsertFuturesTickDataAsync(new[] { row });
            await tickRegistered.Task.WaitAsync(TimeSpan.FromSeconds(30));

            backfill = db.BackfillQueryProjectionsV2Async(batchSize: 64);
            await backfillReconciled.Task.WaitAsync(TimeSpan.FromSeconds(30));

            // The source and target identity scans have both finished. Commit the tick
            // now so only the pre-registered guard can veto this cutover.
            releaseTickData.TrySetResult(true);
            await tickWrite.WaitAsync(TimeSpan.FromSeconds(30));

            var failedGuardMarkers = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)
                .ExecuteQueryAsync(record => (
                    ProjectionName: record.GetString(0),
                    ScopeKey: record.GetString(1),
                    StartedOn: record.GetDateTime(3)));
            failedGuardMarkers.Should().Contain(marker =>
                marker.ProjectionName == "futures_tick_data_by_time" &&
                marker.ScopeKey == guardScope &&
                marker.StartedOn == DateTime.UnixEpoch);

            releaseBackfill.TrySetResult(true);
            var result = await backfill.WaitAsync(TimeSpan.FromSeconds(30));
            var readiness = await db.GetQueryProjectionReadinessAsync();

            result.IsReconciled.Should().BeTrue();
            result.CutoverCompleted.Should().BeFalse();
            readiness.FuturesTickByTime.Should().BeFalse();
        }
        finally
        {
            db.TickProjectionGuardRegisteredForTestingAsync = null;
            db.ProjectionBackfillReconciledForTestingAsync = null;
            releaseTickData.TrySetResult(true);
            releaseBackfill.TrySetResult(true);
            if (tickWrite is not null)
            {
                try { await tickWrite; }
                catch { }
            }
            if (backfill is not null)
            {
                try { await backfill; }
                catch { }
            }

            await db.BackfillQueryProjectionsV2Async(batchSize: 64);
            await db.DeleteFuturesTickDataAsync(row.ContractId, row.ValueDate);
        }
    }

    [Fact]
    public async Task FuturesEodMutation_WithUnknownSubmissionResponse_RemainsUnclassifiedUntilExplicitCutoff()
    {
        var db = TestFixture.DevDatabase;
        var suffix = Guid.NewGuid().ToString("N");
        var row = SampleData.FuturesEodData with
        {
            ContractId = $"FENCED-EOD-{suffix}",
            Symbol = "FENCE",
            ValueDate = new DateOnly(2049, 3, 18),
            ClosePrice = 512.25m
        };
        const string projectionName = "futures_eod_data_by_month";
        var scopeKey = "204903";
        var guardScope = $"$guard:{GetTestVixBucket(scopeKey)}";

        await db.DeleteFuturesEodDataAsync(row.ContractId, row.ValueDate);
        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        db.MaintainedProjectionMutationSubmittingForTestingAsync = () =>
            Task.FromException(new TimeoutException("Simulated unknown Scylla mutation response."));

        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                db.InsertFuturesEodDataAsync(new[] { row }));

            var states = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)
                .SetParameters(new GetMarketDataProjectionScopeStatesV3(
                    projectionName,
                    new[] { scopeKey, guardScope }))
                .ExecuteQueryAsync(record => (
                    ScopeKey: record.GetString(1),
                    Blocked: record.GetBool(4),
                    ActiveOperationsEmpty: record.IsCollectionEmpty(5)));
            states.Should().HaveCount(2);
            states.Should().OnlyContain(static state =>
                state.Blocked && !state.ActiveOperationsEmpty);

            var markers = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeMutationsV3All)
                .ExecuteQueryAsync(record => (
                    ProjectionName: record.GetString(0),
                    ScopeKey: record.GetString(1),
                    StartedOn: record.GetDateTime(3)));
            markers.Where(marker =>
                    marker.ProjectionName == projectionName &&
                    (marker.ScopeKey == scopeKey || marker.ScopeKey == guardScope))
                .Should().HaveCount(2)
                .And.OnlyContain(static marker => marker.StartedOn != DateTime.UnixEpoch);
        }
        finally
        {
            db.MaintainedProjectionMutationSubmittingForTestingAsync = null;
            var repaired = await db.BackfillQueryProjectionsV2Async(
                batchSize: 64,
                staleOperationCutoffUtc: DateTime.UtcNow);
            repaired.CutoverCompleted.Should().BeTrue();
            await db.DeleteFuturesEodDataAsync(row.ContractId, row.ValueDate);
        }
    }

    [Fact]
    public async Task VixMutation_StartedBeforeBackfillAndCommittedAfterReconciliation_DoesNotPublishReadiness()
    {
        var db = TestFixture.DevDatabase;
        var suffix = Guid.NewGuid().ToString("N");
        var row = SampleData.VixFuturesTickData with
        {
            ContractId = $"FENCED-VIX-{suffix}",
            ValueDate = new DateOnly(2049, 4, 19),
            Price = 44.25m,
            Size = 23
        };
        var mutationSubmitting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMutation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var backfillReconciled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBackfill = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? write = null;
        Task<MarketDataProjectionBackfillResult>? backfill = null;

        await db.DeleteVixFuturesEodDataAsync(row.ContractId, row.ValueDate);
        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        db.MaintainedProjectionMutationSubmittingForTestingAsync = async () =>
        {
            mutationSubmitting.TrySetResult(true);
            await releaseMutation.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };
        db.ProjectionBackfillReconciledForTestingAsync = async () =>
        {
            backfillReconciled.TrySetResult(true);
            await releaseBackfill.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };

        try
        {
            write = db.InsertVixFuturesEodDataAsync(row);
            await mutationSubmitting.Task.WaitAsync(TimeSpan.FromSeconds(30));

            backfill = db.BackfillQueryProjectionsV2Async(batchSize: 64);
            await backfillReconciled.Task.WaitAsync(TimeSpan.FromSeconds(30));

            releaseMutation.TrySetResult(true);
            await write.WaitAsync(TimeSpan.FromSeconds(30));
            releaseBackfill.TrySetResult(true);

            var result = await backfill.WaitAsync(TimeSpan.FromSeconds(30));
            var readiness = await db.GetQueryProjectionReadinessAsync();
            result.IsReconciled.Should().BeTrue();
            result.CutoverCompleted.Should().BeFalse();
            readiness.VixFuturesContractIndex.Should().BeFalse();
        }
        finally
        {
            db.MaintainedProjectionMutationSubmittingForTestingAsync = null;
            db.ProjectionBackfillReconciledForTestingAsync = null;
            releaseMutation.TrySetResult(true);
            releaseBackfill.TrySetResult(true);
            if (write is not null)
            {
                try { await write; }
                catch { }
            }
            if (backfill is not null)
            {
                try { await backfill; }
                catch { }
            }

            await db.BackfillQueryProjectionsV2Async(batchSize: 64);
            await db.DeleteVixFuturesEodDataAsync(row.ContractId, row.ValueDate);
        }
    }

    [Fact]
    public async Task GetCurrentFuturesEodDataAsync_NewMonthBeforeInventoryInsert_FallsBackToCanonicalData()
    {
        var db = TestFixture.DevDatabase;
        const string projectionName = "futures_eod_data_by_month";
        await db.BackfillQueryProjectionsV2Async(batchSize: 64);

        var indexedMonths = await db.Use(MarketDataDbCql.GetMarketDataProjectionMonths)
            .SetParameters(new GetMarketDataProjectionMonths(projectionName, 999912))
            .ExecuteQueryAsync(record => record.GetInt(0));
        var valueDate = FindMonthWhoseGuardIsAbsentFromEarlierInventory(indexedMonths);
        var yearMonth = valueDate.Year * 100 + valueDate.Month;
        var suffix = Guid.NewGuid().ToString("N");
        var row = SampleData.FuturesEodData with
        {
            ContractId = $"EOD-INVENTORY-RACE-{suffix}",
            Symbol = $"EIR-{suffix}",
            ValueDate = valueDate,
            ClosePrice = 612.75m
        };
        var projectionWritten = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMonthInventory = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? write = null;

        db.FuturesEodProjectionMonthSubmittingForTestingAsync = async () =>
        {
            projectionWritten.TrySetResult(true);
            await releaseMonthInventory.Task.WaitAsync(TimeSpan.FromSeconds(30));
        };

        try
        {
            write = db.InsertFuturesEodDataAsync(new[] { row });
            await projectionWritten.Task.WaitAsync(TimeSpan.FromSeconds(30));

            var monthsBeforeInventoryInsert = await db.Use(MarketDataDbCql.GetMarketDataProjectionMonths)
                .SetParameters(new GetMarketDataProjectionMonths(projectionName, yearMonth))
                .ExecuteQueryAsync(record => record.GetInt(0));
            monthsBeforeInventoryInsert.Should().NotContain(yearMonth);

            // The canonical and projected rows are visible, but the month inventory is
            // deliberately still absent. The all-guard stamp must reject the projection
            // path so this semantically global <= target read observes canonical data.
            var result = await db.GetCurrentFuturesEodDataAsync(valueDate);
            result.Should().NotBeNull();
            result!.ContractId.Should().Be(row.ContractId);
            result.ValueDate.Should().Be(row.ValueDate);
            result.ClosePrice.Should().Be(row.ClosePrice);

            releaseMonthInventory.TrySetResult(true);
            await write.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            db.FuturesEodProjectionMonthSubmittingForTestingAsync = null;
            releaseMonthInventory.TrySetResult(true);
            if (write is not null)
            {
                try { await write; }
                catch { }
            }

            await db.DeleteFuturesEodDataAsync(row.ContractId, row.ValueDate);
            await db.Use("DELETE FROM market_data_projection_month " +
                    "WHERE projectionName = :projectionName AND yearMonth = :yearMonth;")
                .SetParameters(new InsertMarketDataProjectionMonth(projectionName, yearMonth))
                .ExecuteCommandAsync();
        }
    }

    [Fact]
    public async Task DisjointTickEodAndVixWrites_KeepScopedProjectionReadsReady()
    {
        var db = TestFixture.DevDatabase;
        var suffix = Guid.NewGuid().ToString("N");
        var tickDate = new DateOnly(2047, 1, 12);
        var tickRows = Enumerable.Range(0, 8)
            .Select(index => SampleData.FuturesTickData with
            {
                ContractId = $"SCOPED-TICK-{suffix}-{index}",
                ValueDate = tickDate.AddDays(index),
                TickId = 9_000_000 + index,
                TickTime = new TimeOnly(10, 0, index),
                Price = 200m + index
            })
            .ToArray();
        var eodRows = Enumerable.Range(0, 6)
            .Select(index => SampleData.FuturesEodData with
            {
                ContractId = $"SCOPED-EOD-{suffix}-{index}",
                Symbol = $"SE{index}",
                ValueDate = new DateOnly(2047, index + 1, 15),
                ClosePrice = 300m + index
            })
            .ToArray();

        var firstVixContract = $"SCOPED-VIX-{suffix}-0";
        var secondVixContract = Enumerable.Range(1, 128)
            .Select(index => $"SCOPED-VIX-{suffix}-{index}")
            .First(contractId => GetTestVixBucket(contractId) != GetTestVixBucket(firstVixContract));
        var vixDate = new DateOnly(2047, 7, 20);
        var vixRows = new[]
        {
            SampleData.VixFuturesTickData with
            {
                ContractId = firstVixContract,
                ValueDate = vixDate,
                Price = 31m,
                Size = 101
            },
            SampleData.VixFuturesTickData with
            {
                ContractId = secondVixContract,
                ValueDate = vixDate,
                Price = 32m,
                Size = 102
            }
        };

        await db.BackfillQueryProjectionsV2Async(batchSize: 64);
        var missingTickScope = GetTestTickScope($"SCOPED-MISSING-{suffix}", tickDate);
        var missingState = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)
            .SetParameters(new GetMarketDataProjectionScopeStatesV3(
                "futures_tick_data_by_time",
                new[] { missingTickScope }))
            .ExecuteQueryAsync(record => record.GetString(1));
        missingState.Should().BeEmpty();
        await AssertProjectionScopeStampAvailableAsync(
            db,
            "futures_tick_data_by_time",
            missingTickScope);
        try
        {
            await Task.WhenAll(tickRows.Select(row =>
                db.InsertFuturesTickDataAsync(new[] { row })));
            (await db.GetQueryProjectionReadinessAsync()).FuturesTickByTime.Should().BeTrue();
            await AssertProjectionScopesReadyAsync(
                db,
                "futures_tick_data_by_time",
                tickRows.Select(row => GetTestTickScope(row.ContractId, row.ValueDate)));
            foreach (var row in tickRows)
            {
                var projected = await db.GetLastFuturesTickDataByTickDateAsync(
                    row.ContractId,
                    row.ValueDate.ToDateTime(row.TickTime));
                projected.Should().NotBeNull();
                projected!.TickId.Should().Be(row.TickId);
            }

            await Task.WhenAll(eodRows.Select(row =>
                db.InsertFuturesEodDataAsync(new[] { row })));
            (await db.GetQueryProjectionReadinessAsync()).FuturesEodByMonth.Should().BeTrue();
            await AssertProjectionScopesReadyAsync(
                db,
                "futures_eod_data_by_month",
                eodRows.Select(row => (row.ValueDate.Year * 100 + row.ValueDate.Month)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var projectedEod = await db.GetCurrentFuturesEodDataByDateRangeAsync(
                eodRows.Min(static row => row.ValueDate),
                eodRows.Max(static row => row.ValueDate));
            projectedEod.Where(row => row.ContractId.StartsWith($"SCOPED-EOD-{suffix}", StringComparison.Ordinal))
                .Should().HaveCount(eodRows.Length);

            await Task.WhenAll(vixRows.Select(db.InsertVixFuturesEodDataAsync));
            (await db.GetQueryProjectionReadinessAsync()).VixFuturesContractIndex.Should().BeTrue();
            await AssertProjectionScopesReadyAsync(
                db,
                "vix_futures_contract_index",
                vixRows.Select(row => GetTestVixBucket(row.ContractId)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var projectedVix = await db.GetVixFuturesEodDataByValueDateAsync(vixDate);
            projectedVix.Where(row => row.ContractId == firstVixContract || row.ContractId == secondVixContract)
                .Should().HaveCount(vixRows.Length);
        }
        finally
        {
            await Task.WhenAll(tickRows.Select(row =>
                db.DeleteFuturesTickDataAsync(row.ContractId, row.ValueDate)));
            await Task.WhenAll(eodRows.Select(row =>
                db.DeleteFuturesEodDataAsync(row.ContractId, row.ValueDate)));
            await Task.WhenAll(vixRows.Select(row =>
                db.DeleteVixFuturesEodDataAsync(row.ContractId, row.ValueDate)));
        }
    }

    static async Task AssertProjectionScopesReadyAsync(
        MarketDataDbContext db,
        string projectionName,
        IEnumerable<string> scopeKeys)
    {
        var expectedScopes = scopeKeys.Distinct(StringComparer.Ordinal).ToArray();
        var states = await db.Use(MarketDataDbCql.GetMarketDataProjectionScopeStatesV3)
            .SetParameters(new GetMarketDataProjectionScopeStatesV3(projectionName, expectedScopes))
            .ExecuteQueryAsync(record => (
                ScopeKey: record.GetString(1),
                IsReady: record.GetBool(3),
                Blocked: record.GetBool(4),
                ActiveOperationsEmpty: record.IsCollectionEmpty(5)));

        states.Select(static state => state.ScopeKey)
            .Should().BeEquivalentTo(expectedScopes);
        states.Should().OnlyContain(static state =>
            state.IsReady && !state.Blocked && state.ActiveOperationsEmpty);
    }

    static async Task AssertProjectionScopeStampAvailableAsync(
        MarketDataDbContext db,
        string projectionName,
        string scopeKey)
    {
        var method = typeof(MarketDataDbContext).GetMethod(
            "GetProjectionScopeReadStampAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var task = (Task)method.Invoke(
            db,
            new object[] { projectionName, new[] { scopeKey } })!;
        await task;
        var stamp = task.GetType().GetProperty("Result")!.GetValue(task);
        stamp.Should().NotBeNull();
    }

    static string GetTestTickScope(string contractId, DateOnly valueDate)
        => $"{contractId.Length}:{contractId}:{valueDate.DayNumber}";

    static DateOnly FindMonthWhoseGuardIsAbsentFromEarlierInventory(IEnumerable<int> indexedMonths)
    {
        var indexed = indexedMonths.ToHashSet();
        var earlierInventoryGuards = new HashSet<int>();
        for (var year = 1; year <= 9999; year++)
        {
            for (var month = 1; month <= 12; month++)
            {
                var yearMonth = year * 100 + month;
                var scopeKey = yearMonth.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (indexed.Contains(yearMonth))
                {
                    earlierInventoryGuards.Add(GetTestVixBucket(scopeKey));
                    continue;
                }

                if (!earlierInventoryGuards.Contains(GetTestVixBucket(scopeKey)))
                    return new DateOnly(year, month, 15);
            }
        }

        throw new InvalidOperationException("No unindexed EOD month with an unstamped inventory guard was available.");
    }

    static int GetTestVixBucket(string contractId)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        foreach (var character in contractId)
        {
            hash = unchecked((hash ^ (byte)character) * prime);
            hash = unchecked((hash ^ (byte)(character >> 8)) * prime);
        }
        hash = unchecked((hash ^ 0xFF) * prime);
        return (int)(hash % 32);
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
        var suffix = Guid.NewGuid().ToString("N");
        var contractId = $"!current-eod-{suffix}";
        var symbol = $"current-eod-{suffix}";
        var valueDate = new DateOnly(9999, 12, 30);
        var todayClosingPrice = SampleData.FuturesClosingPrice with
        {
            ContractId = contractId,
            ValueDate = valueDate
        };
        var yesterdayClosingPrice = SampleData.YesterdaysFuturesClosingPrice with
        {
            ContractId = contractId,
            ValueDate = valueDate.AddDays(-1)
        };
        var expectedEodData = SampleData.FuturesEodData with
        {
            ContractId = contractId,
            Symbol = symbol,
            ValueDate = valueDate
        };
        var yesterdayEodData = SampleData.YesterdaysFuturesEodData with
        {
            ContractId = contractId,
            Symbol = symbol,
            ValueDate = valueDate.AddDays(-1)
        };

        // Insert the sample data for today and yesterday into the database
        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{todayClosingPrice.ContractId}' and valueDate = '{todayClosingPrice.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.Use($"delete from futures_closing_price where contractId = '{yesterdayClosingPrice.ContractId}' and valueDate = '{yesterdayClosingPrice.ValueDate}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(todayClosingPrice);
        await TestFixture.DevDatabase.InsertFuturesClosingPriceAsync(yesterdayClosingPrice);

        try
        {
            await TestFixture.DevDatabase.InsertFuturesEodDataAsync(expectedEodData);
            await TestFixture.DevDatabase.InsertFuturesEodDataAsync(yesterdayEodData);

            // Act
            var result = await TestFixture.DevDatabase.GetCurrentFuturesEodDataAsync(valueDate);

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
        finally
        {
            await TestFixture.DevDatabase.DeleteFuturesEodDataAsync(contractId, valueDate);
            await TestFixture.DevDatabase.DeleteFuturesEodDataAsync(contractId, valueDate.AddDays(-1));
            await TestFixture.DevDatabase.DeleteFuturesClosingPriceAsync(contractId, valueDate);
            await TestFixture.DevDatabase.DeleteFuturesClosingPriceAsync(contractId, valueDate.AddDays(-1));
        }
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

        await DeleteFuturesItiSignalsAsync(SampleData.FuturesItiSignal1.ContractId, SampleData.FuturesItiSignal1.ValueDate);
        await DeleteFuturesItiSignalsAsync(SampleData.FuturesItiSignal2.ContractId, SampleData.FuturesItiSignal2.ValueDate);
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

        await DeleteFuturesItiSignalsAsync(SampleData.FuturesItiSignal1.ContractId);
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

        await DeleteFuturesItiSignalsAsync(SampleData.FuturesItiSignal1.ContractId);
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

        await DeleteFuturesItiSignalsAsync(SampleData.FuturesItiSignal1.ContractId, SampleData.FuturesItiSignal1.ValueDate);
        await DeleteFuturesItiSignalsAsync(SampleData.FuturesItiSignal2.ContractId, SampleData.FuturesItiSignal2.ValueDate);
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

        await DeleteFuturesItiSignalsAsync(SampleData.FuturesItiSignal1.ContractId, SampleData.FuturesItiSignal1.ValueDate);
        await DeleteFuturesItiSignalsAsync(SampleData.FuturesItiSignal2.ContractId, SampleData.FuturesItiSignal2.ValueDate);
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

        await TestFixture.SecDatabase.Use("truncate futures_contract").ExecuteCommandAsync();
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract1);
        await TestFixture.SecDatabase.DbWriter.InsertFuturesContractAsync(SampleData.FuturesContract2);
        await DeleteFuturesItiSignalsAsync(futuresItiSignal1.ContractId);
        await DeleteFuturesItiSignalsAsync(futuresItiSignal2.ContractId);
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

        await TestFixture.SecDatabase.Use("truncate futures_contract").ExecuteCommandAsync();
        await DeleteFuturesItiSignalsAsync(futuresItiSignal1.ContractId);
        await DeleteFuturesItiSignalsAsync(futuresItiSignal2.ContractId);
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

        await DeleteFuturesItiSignalsAsync(SampleData.FuturesItiSignal1.ContractId, SampleData.FuturesItiSignal1.ValueDate);
        await DeleteFuturesItiSignalsAsync(SampleData.FuturesItiSignal2.ContractId, SampleData.FuturesItiSignal2.ValueDate);
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

        await DeleteFuturesItiSignalsAsync(expectedSignal.ContractId, expectedSignal.ValueDate);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendDirectionSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendingSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(expectedSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesItiSignalAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.SequenceId.Should().Be(expectedSignal.SequenceId);
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

    [Fact]
    public async Task FuturesItiTimeFrameState_RoundTripsFrameAndBandFields()
    {
        var valueDate = new DateOnly(2026, 9, 10);
        var calendarBucketStart = new DateOnly(2026, 9, 7);
        var frameStart = new DateOnly(2026, 9, 8);
        var signal = SampleData.FuturesItiSignal1 with
        {
            ContractId = $"ES-ITI-FRAME-{Guid.NewGuid():N}",
            ValueDate = valueDate,
            TimePeriod = TimeFrameType.Weekly,
            TimeFrameStartValueDate = frameStart,
            BandAnchorPrice = 5_432.25,
            BandPercentage = 0.10,
            BandSize = 2.75
        };

        try
        {
            await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(signal);

            var result = await TestFixture.DevDatabase.GetFuturesItiTimeFrameStateAsync(
                signal.ContractId,
                signal.TimePeriod,
                calendarBucketStart);

            result.Should().NotBeNull();
            result!.TimeFrameStartValueDate.Should().Be(frameStart);
            result.ValueDate.Should().Be(valueDate);
            result.BandAnchorPrice.Should().Be(5_432.25);
            result.BandPercentage.Should().Be(0.10);
            result.BandSize.Should().Be(2.75);
        }
        finally
        {
            await TestFixture.DevDatabase.DbWriter.DeleteFuturesItiSignalAsync(
                signal.ContractId,
                signal.ValueDate,
                signal.TimePeriod);
        }
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

        await DeleteFuturesItiSignalsAsync(expectedSignal.ContractId, expectedSignal.ValueDate);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendReversalChangedSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(expectedSignal);
        await TestFixture.DevDatabase.DbWriter.InsertFuturesItiSignalAsync(trendingSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesItiSignalTrendDirectionChangeAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.SequenceId.Should().Be(expectedSignal.SequenceId);
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
        var expectedSignal = SampleData.FuturesItiSignal1 with
        {
            SequenceId = 102,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendExtremeChanged
        };
        var trendingSignal = SampleData.FuturesItiSignal1 with
        {
            SequenceId = 103,
            IntrinsicTimeMode = IntrinsicTimeModeType.Trending
        };
        var trendDirectionChangedSignal = SampleData.FuturesItiSignal1 with { SequenceId = 101 };

        await DeleteFuturesItiSignalsAsync(expectedSignal.ContractId, expectedSignal.ValueDate);
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
        var expectedSignal = SampleData.FuturesItiSignal1 with
        {
            SequenceId = 202,
            IntrinsicTimeMode = IntrinsicTimeModeType.TrendReversalChanged
        };
        var trendingSignal = SampleData.FuturesItiSignal1 with
        {
            SequenceId = 203,
            IntrinsicTimeMode = IntrinsicTimeModeType.Trending
        };
        var trendDirectionChangedSignal = SampleData.FuturesItiSignal1 with { SequenceId = 201 };

        await DeleteFuturesItiSignalsAsync(expectedSignal.ContractId, expectedSignal.ValueDate);
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
        await TestFixture.DevDatabase.InsertFuturesOptionTickDataAsync(expectedData);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesOptionTickDataAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedData.ContractId);
        result.ValueDate.Should().Be(expectedData.ValueDate);
        result.TickId.Should().Be(expectedData.TickId,
            "a supplied projector identity must be preserved by the target store");
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

        await TestFixture.DevDatabase.Use(
            $"delete from futures_rsi_signal where contractId = '{expectedSignal.ContractId}' " +
            $"and timePeriod = '{expectedSignal.TimePeriod}' and periodLength = {expectedSignal.PeriodLength}")
            .ExecuteCommandAsync();
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
        result.SourceSequence.Should().Be(expectedSignal.SourceSequence);
        result.SourceEventTimestamp.Should().Be(expectedSignal.SourceEventTimestamp);
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

        await TestFixture.DevDatabase.Use($"delete from futures_traders_dynamic_index_signal where contractId = '{expectedSignal.ContractId}' and timePeriod = '{expectedSignal.TimePeriod}' and configurationId = '{expectedSignal.ConfigurationId}' and valueDate = '{expectedSignal.ValueDate:yyyy-MM-dd}' and timestamp = '{expectedSignal.Timestamp:HH:mm:ss}'").ExecuteCommandAsync();
        await TestFixture.DevDatabase.InsertFuturesTdiSignalAsync(expectedSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesTdiSignalAsync(
            entityId.ContractId,
            entityId.ValueDate,
            entityId.TimePeriod,
            entityId.ConfigurationId);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.Timestamp.Hour.Should().Be(expectedSignal.Timestamp.Hour);
        result.Timestamp.Minute.Should().Be(expectedSignal.Timestamp.Minute);
        result.SchemaVersion.Should().Be(FuturesTdiConfiguration.CurrentSchemaVersion);
        result.ConfigurationId.Should().Be(expectedSignal.ConfigurationId);
        result.Rsi.Should().Be(expectedSignal.Rsi);
        result.PriceLine.Should().Be(expectedSignal.PriceLine);
        result.SignalLine.Should().Be(expectedSignal.SignalLine);
        result.MarketBaseLine.Should().Be(expectedSignal.MarketBaseLine);
        result.UpperVolatilityBand.Should().Be(expectedSignal.UpperVolatilityBand);
        result.LowerVolatilityBand.Should().Be(expectedSignal.LowerVolatilityBand);
        result.Cross.Should().Be(expectedSignal.Cross);
        result.MarketState.Should().Be(expectedSignal.MarketState);
        result.TDI.Should().Be(expectedSignal.TDI);
        result.TDIStrength.Should().Be(expectedSignal.TDIStrength);
    }

    /// <summary>
    /// Verifies that TDI projections for the same contract and date remain isolated by period and configuration.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesTdiSignalAsync_IsolatesPeriodAndConfigurationPartitions()
    {
        var contractId = $"TDI{Guid.NewGuid():N}";
        var oneMinute = SampleData.FuturesTdiSignal with
        {
            ContractId = contractId,
            TimePeriod = TimeFrameType.OneMinute,
            ConfigurationId = FuturesTdiConfiguration.StandardConfigurationId
        };
        var fiveMinute = oneMinute with
        {
            TimePeriod = TimeFrameType.FiveMinutes,
            PriceLine = oneMinute.PriceLine + 1d
        };
        const string alternateConfiguration = "TDI-INTEGRATION-ALTERNATE";
        var alternate = oneMinute with
        {
            ConfigurationId = alternateConfiguration,
            PriceLine = oneMinute.PriceLine + 2d
        };

        await TestFixture.DevDatabase.InsertFuturesTdiSignalAsync(oneMinute);
        await TestFixture.DevDatabase.InsertFuturesTdiSignalAsync(fiveMinute);
        await TestFixture.DevDatabase.InsertFuturesTdiSignalAsync(alternate);

        var oneMinuteResult = await TestFixture.DevDatabase.GetLastFuturesTdiSignalAsync(
            contractId, oneMinute.ValueDate, TimeFrameType.OneMinute,
            FuturesTdiConfiguration.StandardConfigurationId);
        var fiveMinuteResult = await TestFixture.DevDatabase.GetLastFuturesTdiSignalAsync(
            contractId, oneMinute.ValueDate, TimeFrameType.FiveMinutes,
            FuturesTdiConfiguration.StandardConfigurationId);
        var alternateResult = await TestFixture.DevDatabase.GetLastFuturesTdiSignalAsync(
            contractId, oneMinute.ValueDate, TimeFrameType.OneMinute, alternateConfiguration);

        oneMinuteResult.PriceLine.Should().Be(oneMinute.PriceLine);
        fiveMinuteResult.PriceLine.Should().Be(fiveMinute.PriceLine);
        alternateResult.PriceLine.Should().Be(alternate.PriceLine);
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
        await TestFixture.DevDatabase.InsertFuturesTradeSignalAsync(expectedSignal);

        // Act
        var result = await TestFixture.DevDatabase.GetLastFuturesTradeSignalAsync(entityId.ContractId, entityId.ValueDate);

        // Assert
        result.Should().NotBeNull();
        result.ContractId.Should().Be(expectedSignal.ContractId);
        result.ValueDate.Should().Be(expectedSignal.ValueDate);
        result.SequenceId.Should().Be(expectedSignal.SequenceId,
            "a fail-stop replay must address the same logical target row");
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
        await TestFixture.DevDatabase.DeleteYieldCurveRateAsync(expectedRate.ValueDate);
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
        await TestFixture.DevDatabase.DeleteYieldCurveRateAsync(valueDate);
        await TestFixture.DevDatabase.InsertYieldCurveRateAsync(SampleData.YieldCurveRate);

        // Act
        await TestFixture.DevDatabase.DeleteYieldCurveRateAsync(valueDate);

        // Assert
        var result = await TestFixture.DevDatabase.GetYieldCurveRateAsync(valueDate);
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
        await TestFixture.DevDatabase.DeleteYieldCurveRateAsync(valueDate);

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
        await TestFixture.DevDatabase.DeleteYieldCurveRateAsync(SampleData.YieldCurveRate.ValueDate);

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
        await TestFixture.DevDatabase.DeleteYieldCurveRateAsync(SampleData.YieldCurveRate.ValueDate);

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

}
