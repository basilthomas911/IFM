using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using FluentAssertions.Extensions;
using NSubstitute;
using Xunit;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.ScyllaDb.OptionPricerDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.OptionPricerDb;

public class OptionPricerFixture : IDisposable
{
    public OptionPricerFixture()
    {
        SetSeqIdDatabase(); 
        SetDevDatabase();
    }

    public void Dispose()
    {
        // Do "global" teardown here; Only called once.
    }

    public Storage.ScyllaDb.OptionPricerDb.OptionPricerDbContext DevDatabase { get; private set; }
    public SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }
    public DbContextFactory DbFactory { get; private set; }

    void SetDevDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("OptionPricerDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=option_pricer_test_db", "System.Data.ScyllaDb");
        var diContainer = new Dictionary<Type, Storage.ScyllaDb.OptionPricerDb.OptionPricerDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var redisCache = Substitute.For<IRedisCache>();
        var redisCacheMap = new Dictionary<string, string>();
        redisCache.Get(Arg.Any<string>()).Returns(callInfo => redisCacheMap[callInfo.Arg<string>()]);
        redisCache.When(_ => _.Set(Arg.Any<string>(), Arg.Any<string>())).Do(_ => { redisCacheMap.Add(_.ArgAt<string>(0), _.ArgAt<string>(1)); });
        var blackboardServce = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<Storage.ScyllaDb.OptionPricerDb.OptionPricerDbContext>), new Storage.ScyllaDb.OptionPricerDb.OptionPricerDbContext(dbConn, DbFactory, SequenceIdGenerator, logger));
        DevDatabase = DbFactory.OptionPricerDb as Storage.ScyllaDb.OptionPricerDb.OptionPricerDbContext;
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
        SeqIdDatabase = dbFactory.SequenceIdDb as SequenceIdDbContext;
        SequenceIdGenerator = new PostgresSequenceIdGenerator(dbFactory.SequenceIdDb as SequenceIdDbContext);
    }

}

public  class OptionPricerDbTests : IClassFixture<OptionPricerFixture>
{
    public OptionPricerDbTests(OptionPricerFixture testFixture)
    {
        TestFixture = testFixture;
    }

    OptionPricerFixture TestFixture { get; }

    /// <summary>
    /// Unit test for GetOptionPricerDevicesAsync method.
    /// </summary>
    [Fact]
    public async Task GetOptionPricerDevicesAsync_ReturnsExpectedResults()
    {
        // Arrange
        var optionPricerDevice = SampleData.OptionPricerDevice;
        var deviceId = optionPricerDevice.DeviceId;
        var entityId = optionPricerDevice.EntityId;

        await TestFixture.DevDatabase.DbWriter.DeleteOptionPricerDeviceAsync(entityId);
        await TestFixture.DevDatabase.DbWriter.InsertOptionPricerDeviceAsync(optionPricerDevice);

        // Act
        var result = await TestFixture.DevDatabase.GetOptionPricerDevicesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);
        result.Should().ContainEquivalentOf(optionPricerDevice);
    }

    /// <summary>
    /// Unit test for GetSpreadDistributionAsync method.
    /// </summary>
    [Fact]
    public async Task GetSpreadDistributionAsync_ReturnsExpectedResults()
    {
        // Arrange
        var expectedSpreadDistribution1 = SampleData.SpreadDistribution1;
        var expectedSpreadDistribution2 = SampleData.SpreadDistribution2;
        var tradeId = SampleData.SpreadDistribution1.TradeId;
        var tradeType = SampleData.SpreadDistribution1.TradeType;
        var tradeStatus = SampleData.SpreadDistribution1.TradeStatus;
        var valueDate = SampleData.SpreadDistribution1.ValueDate;
        var daysToExpiry = SampleData.SpreadDistribution1.DaysToExpiry;


        await TestFixture.DevDatabase.DbWriter.DeleteSpreadDistributionAsync(tradeId, valueDate);
        await TestFixture.DevDatabase.DbWriter.InsertSpreadDistributionsAsync(expectedSpreadDistribution1, expectedSpreadDistribution2);

        // Act
        var result = await TestFixture.DevDatabase.GetSpreadDistributionAsync(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry);

        // Assert
        result.Should().NotBeNull();
        result.TradeId.Should().Be(tradeId);
        result.TradeType.Should().Be(tradeType);
        result.TradeStatus.Should().Be(tradeStatus);
        result.ValueDate.Should().Be(valueDate);
        result.DaysToExpiry.Should().Be(daysToExpiry);
    }

    /// <summary>
    /// Unit test for GetSpreadDistributionJobInProgressCountAsync method.
    /// </summary>
    [Fact]
    public async Task GetSpreadDistributionJobInProgressCountAsync_ReturnsExpectedCount()
    {
        // Arrange
        var entityId = SampleData.SpreadDistributionJob.EntityId;
        var expectedCount = 1;

        await TestFixture.DevDatabase.DeleteSpreadDistributionJobsAsync(entityId.OrderId, entityId.TradeId);
        await TestFixture.DevDatabase.InsertSpreadDistributionJobAsync(SampleData.SpreadDistributionJob);

        // Act
        var result = await TestFixture.DevDatabase.GetSpreadDistributionJobInProgressCountAsync(entityId.OrderId, entityId.TradeId);

        // Assert
        result.Should().Be(expectedCount);
    }
}
