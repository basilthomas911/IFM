using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.TradeDb;

public class TradeDbFixture : IDisposable
{

    public TradeDbContext TradeDb { get; private set; }
    public SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }

    public TradeDbFixture()
    {
        SetSeqIdDatabase();
        SetTradeDatabase();
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

    void SetTradeDatabase()
    {
        var dbConn = new DbConnectionSettings()
            .Add("TradeDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=trade_test_db", "System.Data.ScyllaDb");
        var diContainer = new Dictionary<Type, TradeDbContext>();
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
        diContainer.Add(typeof(IObjectRepository<TradeDbContext>), new TradeDbContext(dbConn, dbFactory, SequenceIdGenerator, logger));
        TradeDb = dbFactory.TradeDb as TradeDbContext;
    }

    public void Dispose()
    {
    }
}

public class TradeDbTests : IClassFixture<TradeDbFixture>
{
    public TradeDbTests(TradeDbFixture testFixture)
    {
        TestFixture = testFixture;
    }

    TradeDbFixture TestFixture { get; }

    [Fact]
    [Trait("get option trade by order id and trade id", "TradeDb")]
    public async Task GetOptionTradeAsyncOk()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = SampleData.OptionTrade.OrderId;
        var tradeId = SampleData.OptionTrade.TradeId;

        // Clean up any existing data with the same keys
        await db.DbWriter.DeleteOptionTradeAsync(orderId, tradeId);
        await db.DbWriter.InsertOptionTradeAsync(SampleData.OptionTrade);

        // Act
        var result = await db.GetOptionTradeAsync(orderId, tradeId);

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(SampleData.OptionTrade.OrderId);
        result.TradeId.Should().Be(SampleData.OptionTrade.TradeId);
        result.TradeStrategy.Should().Be(SampleData.OptionTrade.TradeStrategy);
        result.TradeDate.Should().Be(SampleData.OptionTrade.TradeDate);
        result.MaturityDate.Should().Be(SampleData.OptionTrade.MaturityDate);
        result.TradeType.Should().Be(SampleData.OptionTrade.TradeType);
        result.TradeState.Should().Be(SampleData.OptionTrade.TradeState);
        result.TradeAction.Should().Be(SampleData.OptionTrade.TradeAction);
        result.UnderlyingContractId.Should().Be(SampleData.OptionTrade.UnderlyingContractId);
        result.UnderlyingAssetType.Should().Be(SampleData.OptionTrade.UnderlyingAssetType);
        result.IsPrimaryTrade.Should().Be(SampleData.OptionTrade.IsPrimaryTrade);
        result.IsHedgeTrade.Should().Be(SampleData.OptionTrade.IsHedgeTrade);

        result.OptionLegs.Should().NotBeNull();
        result.OptionLegs.Length.Should().Be(SampleData.OptionTrade.OptionLegs.Length);
        foreach (var leg in result.OptionLegs)
        {
            var expectedLeg = SampleData.OptionTrade.OptionLegs.First(e => e.Id.TradeId == leg.Id.TradeId && e.ContractId == leg.Id.ContractId );
            leg.OptionLegType.Should().Be(expectedLeg.OptionLegType);
            leg.Quantity.Should().Be(expectedLeg.Quantity);
            leg.StrikePrice.Should().Be(expectedLeg.StrikePrice);
            leg.OptionLegType.Should().Be(expectedLeg.OptionLegType);
            leg.OptionLegAction.Should().Be(expectedLeg.OptionLegAction);
            leg.CreatedBy.Should().Be(expectedLeg.CreatedBy);
            leg.UpdatedBy.Should().Be(expectedLeg.UpdatedBy);
        }

        result.TradePositions.Should().NotBeNull();
        result.TradePositions.Length.Should().Be(SampleData.OptionTrade.TradePositions.Length);
        foreach (var pos in result.TradePositions)
        {
            var expectedPos = SampleData.OptionTrade.TradePositions.First(e => e.EntityId == pos.EntityId);
            pos.TradeType.Should().Be(expectedPos.TradeType);
            pos.ValueDate.Should().Be(expectedPos.ValueDate);
            pos.DaysToExpiry.Should().Be(expectedPos.DaysToExpiry);
            pos.TradeStatus.Should().Be(expectedPos.TradeStatus);
            pos.AssetPrice.Should().Be(expectedPos.AssetPrice);
            pos.Commission.Should().Be(expectedPos.Commission);
            pos.NetSpread.Should().Be(expectedPos.NetSpread);
            pos.TradeValue.Should().Be(expectedPos.TradeValue);
            pos.TradePnl.Should().Be(expectedPos.TradePnl);
            pos.OTMProbability.Should().Be(expectedPos.OTMProbability);
            pos.ForwardPrice.Should().Be(expectedPos.ForwardPrice);
            pos.ForwardLossRatio.Should().Be(expectedPos.ForwardLossRatio);
            pos.LossProbability.Should().Be(expectedPos.LossProbability);
            pos.RiskFreeRate.Should().Be(expectedPos.RiskFreeRate);
            pos.CreatedBy.Should().Be(expectedPos.CreatedBy);
            pos.UpdatedBy.Should().Be(expectedPos.UpdatedBy);

            pos.OptionLegData.Should().NotBeNull();
            pos.OptionLegData.Length.Should().Be(expectedPos.OptionLegData.Length);
            foreach (var legData in pos.OptionLegData)
            {
                var expectedLegData = expectedPos.OptionLegData.First(e => e.OptionLegId == legData.OptionLegId);
                legData.BidPrice.Should().Be(expectedLegData.BidPrice);
                legData.AskPrice.Should().Be(expectedLegData.AskPrice);
                legData.ImpliedVolatility.Should().Be(expectedLegData.ImpliedVolatility);
                legData.Delta.Should().Be(expectedLegData.Delta);
                legData.Gamma.Should().Be(expectedLegData.Gamma);
                legData.Theta.Should().Be(expectedLegData.Theta);
                legData.Vega.Should().Be(expectedLegData.Vega);
                legData.Rho.Should().Be(expectedLegData.Rho);
                legData.CreatedBy.Should().Be(expectedLegData.CreatedBy);
                legData.UpdatedBy.Should().Be(expectedLegData.UpdatedBy);
            }
        }

        result.TradeLimit.Should().NotBeNull();
        result.TradeLimit!.TradeId.Should().Be(SampleData.OptionTrade.TradeLimit!.TradeId);
        result.TradeLimit!.TradeType.Should().Be(SampleData.OptionTrade.TradeLimit!.TradeType);
        result.TradeLimit!.RiskMargin.Should().Be(SampleData.OptionTrade.TradeLimit!.RiskMargin);
        result.TradeLimit!.MaxProfit.Should().Be(SampleData.OptionTrade.TradeLimit!.MaxProfit);
        result.TradeLimit!.MaxLoss.Should().Be(SampleData.OptionTrade.TradeLimit!.MaxLoss);
        result.TradeLimit!.MaxReturn.Should().Be(SampleData.OptionTrade.TradeLimit!.MaxReturn);
        result.TradeLimit!.MaxLossLimit.Should().Be(SampleData.OptionTrade.TradeLimit!.MaxLossLimit);
        result.TradeLimit!.MinProfitLimit.Should().Be(SampleData.OptionTrade.TradeLimit!.MinProfitLimit);
        result.TradeLimit!.MaxProfitLimit.Should().Be(SampleData.OptionTrade.TradeLimit!.MaxProfitLimit);
        result.TradeLimit!.MinProfitTarget.Should().Be(SampleData.OptionTrade.TradeLimit!.MinProfitTarget);
        result.TradeLimit!.DailyProfitTarget.Should().Be(SampleData.OptionTrade.TradeLimit!.DailyProfitTarget);
        result.TradeLimit!.CreatedBy.Should().Be(SampleData.OptionTrade.TradeLimit!.CreatedBy);
        result.TradeLimit!.UpdatedBy.Should().Be(SampleData.OptionTrade.TradeLimit!.UpdatedBy);

        result.TradeTypeLimits.Should().NotBeNull();
        result.TradeTypeLimits.Length.Should().Be(SampleData.OptionTrade.TradeTypeLimits.Length);
        foreach (var limit in result.TradeTypeLimits)
        {
            var expectedLimit = SampleData.OptionTrade.TradeTypeLimits.First(e =>e.TradeId == limit.TradeId &&  e.TradeType == limit.TradeType);
            limit.TradeType.Should().Be(expectedLimit.TradeType);
            limit.MaxLossLimit.Should().Be(expectedLimit.MaxLossLimit);
            limit.MinProfitLimit.Should().Be(expectedLimit.MinProfitLimit);
            limit.MaxProfitLimit.Should().Be(expectedLimit.MaxProfitLimit);
        }

    }

    /// <summary>
    /// Unit tests for the GetOptionTradeSpreadDataAsync method in TradeDbContext class.
    /// This test verifies that the method correctly retrieves trade spread data for a specific
    /// order, trade, date, and trade type from the database.
    /// </summary>
    [Fact]
    [Trait("get option trade spread data", "TradeDb")]
    public async Task GetOptionTradeSpreadDataAsync_ReturnsCorrectData_WhenExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = SampleData.TradeSpreadData.OrderId;
        var tradeId = SampleData.TradeSpreadData.TradeId;
        var valueDate = SampleData.TradeSpreadData.ValueDate;
        var tradeType = SampleData.TradeSpreadData.TradeType;

        // First ensure we remove any existing data
        await db.Use($"DELETE FROM option_trade_spread_data WHERE orderId = {orderId} AND tradeId = {tradeId}").ExecuteCommandAsync();

        // Insert sample data
        await db.InsertOptionTradeSpreadDataAsync(SampleData.TradeSpreadData);

        // Act
        var result = await db.GetOptionTradeSpreadDataAsync(orderId, tradeId, valueDate, tradeType);

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(SampleData.TradeSpreadData.OrderId);
        result.TradeId.Should().Be(SampleData.TradeSpreadData.TradeId);
        result.ValueDate.Should().Be(SampleData.TradeSpreadData.ValueDate);
        result.TradeType.Should().Be(SampleData.TradeSpreadData.TradeType);
        result.LossLimit.Should().Be(SampleData.TradeSpreadData.LossLimit);
        result.WinLimit.Should().Be(SampleData.TradeSpreadData.WinLimit);
        result.ForwardSpread.Should().Be(SampleData.TradeSpreadData.ForwardSpread);
        result.NetSpread.Should().Be(SampleData.TradeSpreadData.NetSpread);
        result.CreatedBy.Should().Be(SampleData.TradeSpreadData.CreatedBy);
    }

    [Fact]
    [Trait("get option trade spread data", "TradeDb")]
    public async Task GetOptionTradeSpreadDataAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = 9999; // Non-existent order ID
        var tradeId = 9999; // Non-existent trade ID
        var valueDate = new DateOnly(2025, 1, 1);
        var tradeType = TradeType.LongIronCondor;

        // Act
        var result = await db.GetOptionTradeSpreadDataAsync(orderId, tradeId, valueDate, tradeType);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Unit tests for the GetOptionTradeSpreadBarDataAsync method in TradeDbContext class.
    /// This test verifies that the method correctly retrieves option trade spread bar data
    /// for a specified order, trade, date, trade type, and date range from the database.
    /// </summary>
    [Fact]
    [Trait("get option trade spread bar data", "TradeDb")]
    public async Task GetOptionTradeSpreadBarDataAsync_ReturnsCorrectData_WhenExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = SampleData.OptionTrade.OrderId;
        var tradeId = SampleData.OptionTrade.TradeId;
        var valueDate = SampleData.OptionTrade.TradeDate;
        var tradeType = SampleData.OptionTrade.TradeType;
        var startDate = DateTime.Now.AddDays(-7);
        var endDate = DateTime.Now;

        // Create sample spread bar data for testing
        var sampleBarData = new OptionTradeSpreadBarsDataModel(
            orderId: orderId,
            tradeId: tradeId,
            valueDate: valueDate,
            tradeType: tradeType,
            barDate: DateTime.Now.AddDays(-3),
            lossLimit: 12.5m,
            winLimit: 5.25m,
            forwardSpread: 8.75m,
            netSpread: 6.5m);

        // Clean up any existing data with the same keys
        await db.DeleteOptionTradeSpreadBarDataAsync(orderId, tradeId, valueDate, tradeType);

        // Insert sample data
        await db.InsertOptionTradeSpreadBarDataAsync(sampleBarData);

        // Act
        var result = await db.GetOptionTradeSpreadBarDataAsync(orderId, tradeId, valueDate, tradeType, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);

        var barData = result.First();
        barData.OrderId.Should().Be(sampleBarData.OrderId);
        barData.TradeId.Should().Be(sampleBarData.TradeId);
        barData.ValueDate.Should().Be(sampleBarData.ValueDate);
        barData.TradeType.Should().Be(sampleBarData.TradeType);
        barData.LossLimit.Should().Be(sampleBarData.LossLimit);
        barData.WinLimit.Should().Be(sampleBarData.WinLimit);
        barData.ForwardSpread.Should().Be(sampleBarData.ForwardSpread);
        barData.NetSpread.Should().Be(sampleBarData.NetSpread);
    }

    [Fact]
    [Trait("get option trade spread bar data", "TradeDb")]
    public async Task GetOptionTradeSpreadBarDataAsync_ReturnsEmptyCollection_WhenNoDataInRange()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = SampleData.OptionTrade.OrderId;
        var tradeId = SampleData.OptionTrade.TradeId;
        var valueDate = SampleData.OptionTrade.TradeDate;
        var tradeType = SampleData.OptionTrade.TradeType;

        // Use date range outside of any possible test data
        var startDate = DateTime.Now.AddYears(1);
        var endDate = DateTime.Now.AddYears(1).AddDays(7);

        // Act
        var result = await db.GetOptionTradeSpreadBarDataAsync(orderId, tradeId, valueDate, tradeType, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("get option trade spread bar data", "TradeDb")]
    public async Task GetOptionTradeSpreadBarDataAsync_FiltersCorrectly_ByDateRange()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = SampleData.OptionTrade.OrderId;
        var tradeId = SampleData.OptionTrade.TradeId;
        var valueDate = SampleData.OptionTrade.TradeDate;
        var tradeType = SampleData.OptionTrade.TradeType;

        // Clean up existing data
        await db.DeleteOptionTradeSpreadBarDataAsync(orderId, tradeId, valueDate, tradeType);

        // Create two sample records with different dates
        var inRangeBarData = new OptionTradeSpreadBarsDataModel(
            orderId: orderId,
            tradeId: tradeId,
            valueDate: valueDate,
            tradeType: tradeType,
            barDate: new DateTime(2025, 3, 15, 10, 0, 0),
            lossLimit: 12.5m,
            winLimit: 5.25m,
            forwardSpread: 8.75m,
            netSpread: 6.5m);

        var outOfRangeBarData = new OptionTradeSpreadBarsDataModel(
            orderId: orderId,
            tradeId: tradeId,
            valueDate: valueDate,
            tradeType: tradeType,
            barDate: new DateTime(2025, 3, 20, 10, 0, 0),
            lossLimit: 13.0m,
            winLimit: 5.5m,
            forwardSpread: 9.0m,
            netSpread: 7.0m);

        // Insert both records
        await db.InsertOptionTradeSpreadBarDataAsync(inRangeBarData);
        await db.InsertOptionTradeSpreadBarDataAsync(outOfRangeBarData);

        // Define date range that should only include the first record
        var startDate =new DateTime(2025, 3, 14);
        var endDate =new DateTime(2025, 3, 16);

        // Act
        var result = await db.GetOptionTradeSpreadBarDataAsync(orderId, tradeId, valueDate, tradeType, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);

        var barData = result.First();
        barData.BarDate.Should().Be(inRangeBarData.BarDate);
        barData.LossLimit.Should().Be(inRangeBarData.LossLimit);

        // Cleanup
        await db.DeleteOptionTradeSpreadBarDataAsync(orderId, tradeId, valueDate, tradeType);
    }

    [Fact]
    [Trait("get trade history", "TradeDb")]
    public async Task GetTradeHistoryAsync_ReturnsCorrectData_WhenExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = SampleData.OptionTrade.OrderId;
        var tradeId = SampleData.OptionTrade.TradeId;

        // First ensure we have the option trade available that corresponds to the trade history
        await db.DbWriter.DeleteOptionTradeAsync(orderId, tradeId);
        await db.DbWriter.InsertOptionTradeAsync(SampleData.OptionTrade);

        // Act
        var result = await db.GetTradeHistoryAsync(orderId);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(SampleData.OptionTrade.TradePositions.Length);

        // Verify the trade history entries match our sample data (already ordered by ValueDate)
        for (int i = 0; i < SampleData.OptionTrade.TradePositions.Length; i++)
        {
            var expected = SampleData.OptionTrade.TradePositions[i];
            var actual = result.First(e => e.ValueDate == expected.ValueDate && e.DaysToExpiry == expected.DaysToExpiry && e.TradeStatus == expected.TradeStatus);

            actual.OrderId.Should().Be(expected.OrderId);
            actual.TradeId.Should().Be(expected.TradeId);
            actual.TradeType.Should().Be(expected.TradeType);
            actual.ValueDate.Should().Be(expected.ValueDate);
            actual.DaysToExpiry.Should().Be(expected.DaysToExpiry);
            actual.TradeStatus.Should().Be(expected.TradeStatus);
            actual.Commission.Should().Be(expected.Commission);
            actual.NetSpread.Should().Be(expected.NetSpread);
            actual.TradePnl.Should().Be(expected.TradePnl);
        }
    }

    /// <summary>
    /// Unit tests for the GetOptionLegContractIdsAsync method in TradeDbContext class.
    /// This test verifies that the method correctly retrieves all option leg contract IDs
    /// for a specified trade ID from the database.
    /// </summary>
    [Fact]
    [Trait("get option leg contract ids", "TradeDb")]
    public async Task GetOptionLegContractIdsAsync_ReturnsCorrectContractIds_WhenTradeExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var tradeId = SampleData.OptionTrade.TradeId;

        // First ensure we have the option trade with legs available in the database
        await db.DbWriter.DeleteOptionTradeAsync(SampleData.OptionTrade.OrderId, tradeId);
        await db.DbWriter.InsertOptionTradeAsync(SampleData.OptionTrade);

        // Get expected contract IDs directly from the sample data for comparison
        var expectedContractIds = SampleData.OptionTrade.OptionLegs.Select(leg => leg.ContractId).ToList();

        // Act
        var result = await db.GetOptionLegContractIdsAsync(tradeId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveSameCount(expectedContractIds);

        // Verify each contract ID is in the expected format (optional)
        foreach (var contractId in result)
        {
            contractId.Should().NotBeNullOrEmpty();
            expectedContractIds.Contains(contractId).Should().BeTrue();
        }
    }

    [Fact]
    [Trait("get option leg contract ids", "TradeDb")]
    public async Task GetOptionLegContractIdsAsync_ReturnsEmptyCollection_WhenTradeDoesNotExist()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var nonExistentTradeId = 9999; // A trade ID that shouldn't exist in the database

        // Ensure the trade doesn't exist
        try
        {
            await db.DbWriter.DeleteOptionTradeAsync(9999, nonExistentTradeId);
        }
        catch
        {
            // Ignore exceptions if the trade doesn't exist
        }

        // Act
        var result = await db.GetOptionLegContractIdsAsync(nonExistentTradeId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Unit tests for the GetTradeQuantityAsync method in TradeDbContext class.
    /// This method tests that the trade quantity is correctly calculated by summing
    /// the quantities of all option legs associated with a trade and dividing by 
    /// the number of legs to get the average quantity.
    /// </summary>
    [Fact]
    [Trait("get trade quantity", "TradeDb")]
    public async Task GetTradeQuantityAsync_ReturnsCorrectQuantity_WhenOptionLegsExist()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var tradeId = SampleData.OptionTrade.TradeId;

        // Ensure we have the option trade with legs available in the database
        await db.DbWriter.DeleteOptionTradeAsync(SampleData.OptionTrade.OrderId, tradeId);
        await db.DbWriter.InsertOptionTradeAsync(SampleData.OptionTrade);

        // Calculate expected quantity manually from the sample data for comparison
        var expectedQuantity = SampleData.OptionTrade.OptionLegs.Any()
            ? SampleData.OptionTrade.OptionLegs.Sum(leg => leg.Quantity) / SampleData.OptionTrade.OptionLegs.Length
            : 0;

        // Act
        var result = await db.GetTradeQuantityAsync(tradeId);

        // Assert
        result.Should().Be(expectedQuantity);
    }

    [Fact]
    [Trait("get trade quantity", "TradeDb")]
    public async Task GetTradeQuantityAsync_ReturnsZero_WhenNoOptionLegsExist()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var nonExistentTradeId = 9999; // A trade ID that shouldn't exist in the database

        // Ensure the trade doesn't exist
        try
        {
            await db.DbWriter.DeleteOptionTradeAsync(9999, nonExistentTradeId);
        }
        catch
        {
            // Ignore exceptions if the trade doesn't exist
        }

        // Act
        var result = await db.GetTradeQuantityAsync(nonExistentTradeId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    [Trait("get trade quantity", "TradeDb")]
    public async Task GetTradeQuantityAsync_CalculatesCorrectly_WithDifferentQuantities()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = 2001;
        var tradeId = 3001;

        // Create a custom option trade with known quantities for precise testing
        var optionTrade = new OptionTradeReadModel(
            orderId: orderId,
            tradeId: tradeId,
            tradeStrategy: "Test Strategy",
            tradeDate: new DateOnly(2025, 3, 20),
            maturityDate: new DateOnly(2025, 4, 17),
            tradeType: TradeType.ShortIronCondor,
            tradeState: TradeState.NewTrade,
            tradeAction: TradeAction.Buy,
            underlyingContractId: "SPY",
            underlyingAssetType: AssetType.Futures,
            isPrimaryTrade: true,
            isHedgeTrade: false,
            createdOn: DateTime.Now,
            createdBy: "UnitTest",
            updatedOn: DateTime.Now,
            updatedBy: "UnitTest");

        // Create option legs with different quantities
        var optionLegs = new[]
        {
            new OptionTradeLegReadModel(
                orderId: orderId,
                tradeId: tradeId,
                contractId: "SPY240417C410",
                quantity: 5,
                strikePrice: 410.0m,
                optionLegType: OptionType.Call,
                optionLegAction: OptionLegAction.Long,
                createdOn: DateTime.Now,
                createdBy: "UnitTest",
                updatedOn: DateTime.Now,
                updatedBy: "UnitTest"),
        new OptionTradeLegReadModel(
            orderId: orderId,
            tradeId: tradeId,
            contractId: "SPY240417C415",
            quantity: 5,
            strikePrice: 415.0m,
            optionLegType: OptionType.Call,
            optionLegAction: OptionLegAction.Short,
            createdOn: DateTime.Now,
            createdBy: "UnitTest",
            updatedOn: DateTime.Now,
            updatedBy: "UnitTest"),
        new OptionTradeLegReadModel(
            orderId: orderId,
            tradeId: tradeId,
            contractId: "SPY240417P400",
            quantity: 10,
            strikePrice: 400.0m,
            optionLegType: OptionType.Put,
            optionLegAction: OptionLegAction.Long,
            createdOn: DateTime.Now,
            createdBy: "UnitTest",
            updatedOn: DateTime.Now,
            updatedBy: "UnitTest"),
        new OptionTradeLegReadModel(
            orderId: orderId,
            tradeId: tradeId,
            contractId: "SPY240417P395",
            quantity: 10,
            strikePrice: 395.0m,
            optionLegType: OptionType.Put,
            optionLegAction: OptionLegAction.Short,
            createdOn: DateTime.Now,
            createdBy: "UnitTest",
            updatedOn: DateTime.Now,
            updatedBy: "UnitTest")
    };

        // Add option legs and other required properties
        optionTrade.AddOptionLegs(optionLegs);

        // Create a trade limit and add it to the trade
        var tradeLimit = TradeLimitReadModel.Default(tradeId, TradeType.ShortIronCondor);
        optionTrade.SetTradeLimit(tradeLimit);

        // Clean up any existing data and insert the test trade
        await db.DbWriter.DeleteOptionTradeAsync(orderId, tradeId);
        await db.DbWriter.InsertOptionTradeAsync(optionTrade);

        // Calculate expected quantity: sum of quantities (5+5+10+10=30) divided by number of legs (4)
        var expectedQuantity = (5 + 5 + 10 + 10) / 4;

        // Act
        var result = await db.GetTradeQuantityAsync(tradeId);

        // Assert
        result.Should().Be(expectedQuantity);

        // Clean up
        await db.DbWriter.DeleteOptionTradeAsync(orderId, tradeId);
    }

    /// <summary>
    /// Unit tests for the GetTradePlanStopLossLimitAsync method in TradeDbContext class.
    /// This test verifies that the method correctly retrieves the trade plan stop loss limit
    /// for a specific order and trade from the database.
    /// </summary>
    [Fact]
    [Trait("get trade plan stop loss limit", "TradeDb")]
    public async Task GetTradePlanStopLossLimitAsync_ReturnsCorrectData_WhenExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = SampleData.TradePlan.OrderId;
        var tradeId = SampleData.TradePlan.TradeId;

        // First ensure we remove any existing data
        await db.Use($"DELETE FROM trade_plan WHERE orderId = {orderId} AND tradeId = {tradeId}").ExecuteCommandAsync();
        await db.InsertTradePlanAsync(SampleData.TradePlan);

        // Act
        var result = await db.GetTradePlanStopLossLimitAsync(orderId, tradeId);

        // Assert
        result.Should().NotBeNull();
        result.StopLossLimit.Should().Be(SampleData.TradePlan.StopLossLimit);
    }

    [Fact]
    [Trait("get trade plan stop loss limit", "TradeDb")]
    public async Task GetTradePlanStopLossLimitAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = 9999; // Non-existent order ID
        var tradeId = 9999; // Non-existent trade ID

        // Act
        var result = await db.GetTradePlanStopLossLimitAsync(orderId, tradeId);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Unit tests for the GetTradePlansAsync method in TradeDbContext class.
    /// This test verifies that the method correctly retrieves the trade plans
    /// for a specific order from the database.
    /// </summary>
    [Fact]
    [Trait("get trade plans", "TradeDb")]
    public async Task GetTradePlansAsync_ReturnsCorrectData_WhenExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = SampleData.TradePlan.OrderId;

        // First ensure we remove any existing data
        await db.Use($"DELETE FROM trade_plan WHERE orderId = {orderId}").ExecuteCommandAsync();

        // Insert sample data
        await db.InsertTradePlanAsync(SampleData.TradePlan);

        // Act
        var result = await db.GetTradePlansAsync(orderId);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);

        var tradePlan = result.First();
        tradePlan.OrderId.Should().Be(SampleData.TradePlan.OrderId);
        tradePlan.TradeId.Should().Be(SampleData.TradePlan.TradeId);
        tradePlan.ValueDate.Should().Be(SampleData.TradePlan.ValueDate);
        tradePlan.ActionDate.Should().Be(SampleData.TradePlan.ActionDate);
        tradePlan.TradeDate.Should().Be(SampleData.TradePlan.TradeDate);
        tradePlan.MaturityDate.Should().Be(SampleData.TradePlan.MaturityDate);
        tradePlan.TradeType.Should().Be(SampleData.TradePlan.TradeType);
        tradePlan.ActionType.Should().Be(SampleData.TradePlan.ActionType);
        tradePlan.ActionSubType.Should().Be(SampleData.TradePlan.ActionSubType);
        tradePlan.ActionState.Should().Be(SampleData.TradePlan.ActionState);
        tradePlan.ActionReason.Should().Be(SampleData.TradePlan.ActionReason);
        tradePlan.TradePnl.Should().Be(SampleData.TradePlan.TradePnl);
        tradePlan.ForwardLossRatio.Should().Be(SampleData.TradePlan.ForwardLossRatio);
        tradePlan.LossProbability.Should().Be(SampleData.TradePlan.LossProbability);
        tradePlan.MScore.Should().Be(SampleData.TradePlan.MScore);
        tradePlan.MaxProfit.Should().Be(SampleData.TradePlan.MaxProfit);
        tradePlan.MaxLoss.Should().Be(SampleData.TradePlan.MaxLoss);
        tradePlan.MinProfitTarget.Should().Be(SampleData.TradePlan.MinProfitTarget);
        tradePlan.DailyProfitTarget.Should().Be(SampleData.TradePlan.DailyProfitTarget);
        tradePlan.AssetPrice.Should().Be(SampleData.TradePlan.AssetPrice);
        tradePlan.AssetStdDev.Should().Be(SampleData.TradePlan.AssetStdDev);
        tradePlan.AssetMean.Should().Be(SampleData.TradePlan.AssetMean);
        tradePlan.AssetPriceChange.Should().Be(SampleData.TradePlan.AssetPriceChange);
        tradePlan.MarketTrend.Should().Be(SampleData.TradePlan.MarketTrend);
        tradePlan.MarketVolatility.Should().Be(SampleData.TradePlan.MarketVolatility);
        tradePlan.MarketDirection.Should().Be(SampleData.TradePlan.MarketDirection);
        tradePlan.VixVolatility.Should().Be(SampleData.TradePlan.VixVolatility);
        tradePlan.TradeRisk.Should().Be(SampleData.TradePlan.TradeRisk);
        tradePlan.FiftyDayMA.Should().Be(SampleData.TradePlan.FiftyDayMA);
        tradePlan.FiveDayXMA.Should().Be(SampleData.TradePlan.FiveDayXMA);
        tradePlan.PutOTMProbability.Should().Be(SampleData.TradePlan.PutOTMProbability);
        tradePlan.CallOTMProbability.Should().Be(SampleData.TradePlan.CallOTMProbability);
        tradePlan.ShortPutGamma.Should().Be(SampleData.TradePlan.ShortPutGamma);
        tradePlan.ShortCallGamma.Should().Be(SampleData.TradePlan.ShortCallGamma);
        tradePlan.GammaRisk.Should().Be(SampleData.TradePlan.GammaRisk);
        tradePlan.NetPrice.Should().Be(SampleData.TradePlan.NetPrice);
        tradePlan.ForwardPrice.Should().Be(SampleData.TradePlan.ForwardPrice);
        tradePlan.ForwardDelta.Should().Be(SampleData.TradePlan.ForwardDelta);
        tradePlan.StopLossLimit.Should().Be(SampleData.TradePlan.StopLossLimit);
        tradePlan.TrendType.Should().Be(SampleData.TradePlan.TrendType);
        tradePlan.TrendStrength.Should().Be(SampleData.TradePlan.TrendStrength);
        tradePlan.RSI.Should().Be(SampleData.TradePlan.RSI);
        tradePlan.RSISlope.Should().Be(SampleData.TradePlan.RSISlope);
        tradePlan.TDI.Should().Be(SampleData.TradePlan.TDI);
        tradePlan.TDIStrength.Should().Be(SampleData.TradePlan.TDIStrength);
        tradePlan.CreatedBy.Should().Be(SampleData.TradePlan.CreatedBy);
    }

    [Fact]
    [Trait("get trade plans", "TradeDb")]
    public async Task GetTradePlansAsync_ReturnsEmptyCollection_WhenNotExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = 9999; // Non-existent order ID

        // Act
        var result = await db.GetTradePlansAsync(orderId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Unit tests for the GetTradePlanForwardLossRatiosAsync method in TradeDbContext class.
    /// This test verifies that the method correctly retrieves the trade plan forward loss ratios
    /// for a specified date range from the database.
    /// </summary>
    [Fact]
    [Trait("get trade plan forward loss ratios", "TradeDb")]
    public async Task GetTradePlanForwardLossRatiosAsync_ReturnsCorrectData_WhenExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var startDate = new DateOnly(2025, 3, 1);
        var endDate = new DateOnly(2025, 3, 31);
        var orderId = SampleData.TradePlan.OrderId;

        // First ensure we remove any existing data
        await db.Use($"DELETE FROM trade_plan_forward_loss_ratio where partitionId = 1").ExecuteCommandAsync();
        await db.Use($"DELETE FROM trade_plan WHERE orderId = {orderId}").ExecuteCommandAsync();

        // Insert sample data
        await db.InsertTradePlanAsync(SampleData.TradePlan);
        
        // Act
        var result = await db.GetTradePlanForwardLossRatiosAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);

        var forwardLossRatio = result.First();
        forwardLossRatio.ForwardLossRatio .Should().Be(SampleData.TradePlan.ForwardLossRatio);
    }

    [Fact]
    [Trait("get trade plan forward loss ratios", "TradeDb")]
    public async Task GetTradePlanForwardLossRatiosAsync_ReturnsEmptyCollection_WhenNoDataInRange()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 1, 31);

        // Act
        var result = await db.GetTradePlanForwardLossRatiosAsync(startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Unit test for the GetTradePlanForwardLossRatioAsync method.
    /// This test verifies that the method correctly retrieves the trade plan forward loss ratio
    /// for a specific value date from the database.
    /// </summary>
    [Fact]
    [Trait("get trade plan forward loss ratio", "TradeDb")]
    public async Task GetTradePlanForwardLossRatioAsync_ReturnsCorrectData_WhenExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var valueDate = SampleData.TradePlan.ValueDate;

        // Clean up any existing data for this test date
        await db.Use($"DELETE FROM trade_plan  WHERE orderId  = {SampleData.TradePlan.OrderId}").ExecuteCommandAsync();
        await db.Use($"DELETE FROM trade_plan_forward_loss_ratio WHERE partitionId = 1 and valueDate = '{valueDate}'").ExecuteCommandAsync();

        // Create sample forward loss ratio data
        var tradePlan = SampleData.TradePlan;
       
        // Insert the test data
        await db.InsertTradePlanAsync(tradePlan);

        // Act
        var result = await db.GetTradePlanForwardLossRatioAsync(valueDate);

        // Assert
        result.Should().NotBeNull();
        result.ForwardLossRatio.Should().Be(SampleData.TradePlan.ForwardLossRatio);
    }

    [Fact]
    [Trait("get trade plan forward loss ratio", "TradeDb")]
    public async Task GetTradePlanForwardLossRatioAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var valueDate = SampleData.TradePlan.ValueDate;
        var nonExistentDate = new DateOnly(2026, 12, 31); // Use a date unlikely to exist in test data

        // Ensure the data doesn't exist
        await db.Use($"DELETE FROM trade_plan  WHERE orderId  = {SampleData.TradePlan.OrderId}").ExecuteCommandAsync();
        await db.Use($"DELETE FROM trade_plan_forward_loss_ratio WHERE partitionId = 1 and valueDate = '{valueDate}'").ExecuteCommandAsync();

        // Act
        var result = await db.GetTradePlanForwardLossRatioAsync(nonExistentDate);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Unit tests for the GetTradeLiveFeedAsync method in TradeDbContext class.
    /// This test verifies that the method correctly retrieves trade live feed data
    /// for a specific order and trade ID from the database.
    /// </summary>
    [Fact]
    [Trait("get trade live feed", "TradeDb")]
    public async Task GetTradeLiveFeedAsync_ReturnsCorrectData_WhenExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = 1001;
        var tradeId = 2001;

        // Sample live feed data for testing
        var sampleLiveFeed = new TradeLiveFeedReadModel(
            orderId: orderId,
            tradeId: tradeId,
            liveFeed: true);

        // Clean up any existing data with the same keys
        await db.DeleteTradeLiveFeedAsync(orderId, tradeId);

        // Insert sample data
        await db.InsertTradeLiveFeedAsync(sampleLiveFeed);

        // Act
        var result = await db.GetTradeLiveFeedAsync(orderId, tradeId);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(1);

        var liveFeed = result.First();
        liveFeed.OrderId.Should().Be(sampleLiveFeed.OrderId);
        liveFeed.TradeId.Should().Be(sampleLiveFeed.TradeId);
        liveFeed.LiveFeed.Should().Be(sampleLiveFeed.LiveFeed);

        // Cleanup
        await db.DeleteTradeLiveFeedAsync(orderId, tradeId);
    }

    [Fact]
    [Trait("get trade live feed", "TradeDb")]
    public async Task GetTradeLiveFeedAsync_ReturnsEmptyCollection_WhenNotExists()
    {
        // Arrange
        var db = TestFixture.TradeDb;
        var orderId = 9999; // Non-existent order ID
        var tradeId = 9999; // Non-existent trade ID

        // Ensure the data doesn't exist
        try
        {
            await db.DeleteTradeLiveFeedAsync(orderId, tradeId);
        }
        catch
        {
            // Ignore exceptions if the trade doesn't exist
        }

        // Act
        var result = await db.GetTradeLiveFeedAsync(orderId, tradeId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("read option legs from CSV file and insert into database", "FundDb")]
    public async Task GetOptionLegFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\option_leg.csv"))
           .ReadAsync(MapToOptionLeg, async reducer =>
           {
               await db.Use($"truncate option_leg").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertOptionLegsAsync(reducer);
           });

        var resultSet = await dbTrade.GetOptionLegsAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static OptionTradeLegReadModel MapToOptionLeg(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                contractId: e.GetString(ref o),
                quantity: e.GetInt(ref o),
                strikePrice: e.GetDecimal(ref o),
                optionLegType: e.GetEnum<OptionType>(ref o),
                optionLegAction: e.GetEnum<OptionLegAction>(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o),
                updatedOn: e.GetDateTime(ref o),
                updatedBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read option leg data from CSV file and insert into database", "FundDb")]
    public async Task GetOptionLegDataFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\option_leg_data.csv"))
           .ReadAsync(MapToOptionLegData, async reducer =>
           {
               await db.Use($"truncate option_leg_data").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertOptionLegDataAsync(reducer);
           });

        var resultSet = await dbTrade.GetOptionLegDataAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static OptionTradeLegDataReadModel MapToOptionLegData(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                valueDate: e.GetDateOnly(ref o),
                optionLegId: e.GetString(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                daysToExpiry: e.GetInt(ref o),
                tradeStatus: e.GetEnum<TradeStatus>(ref o),
                bidPrice: e.GetDecimal(ref o),
                askPrice: e.GetDecimal(ref o),
                impliedVolatility: e.GetDouble(ref o),
                delta: e.GetDouble(ref o),
                gamma: e.GetDouble(ref o),
                theta: e.GetDouble(ref o),
                vega: e.GetDouble(ref o),
                rho: e.GetDouble(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o),
                updatedOn: e.GetDateTime(ref o),
                updatedBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read option trades from CSV file and insert into database", "FundDb")]
    public async Task GetOptionTradesFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\option_trade.csv"))
           .ReadAsync(MapToOptionTrade, async reducer =>
           {
               await db.Use($"truncate option_trade").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertOptionTradesAsync(reducer);
           });

        var resultSet = await dbTrade.GetOptionTradesAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static OptionTradeReadModel MapToOptionTrade(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                tradeStrategy: e.GetString(ref o),
                tradeDate: e.GetDateOnly(ref o),
                maturityDate: e.GetDateOnly(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                tradeState: e.GetEnum<TradeState>(ref o),
                tradeAction: e.GetEnum<TradeAction>(ref o),
                underlyingContractId: e.GetString(ref o),
                underlyingAssetType: e.GetEnum<AssetType>(ref o),
                isPrimaryTrade: e.GetBool(ref o),
                isHedgeTrade: e.GetBool(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o),
                updatedOn: e.GetDateTime(ref o),
                updatedBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read option trade spread bar data from CSV file and insert into database", "FundDb")]
    public async Task GetOptionTradeSpreadBarDataFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\option_trade_spread_bar_data.csv"))
           .ReadAsync(MapToOptionTradeSpreadBarData, async reducer =>
           {
               await db.Use($"truncate option_trade_spread_bar_data").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertOptionTradeSpreadBarDataAsync(reducer);
           });

        var resultSet = await dbTrade.GetOptionTradeSpreadBarDataAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static OptionTradeSpreadBarsDataModel MapToOptionTradeSpreadBarData(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                valueDate: e.GetDateOnly(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                barDate: e.GetDateTime(ref o),
                lossLimit: e.GetDecimal(ref o),
                winLimit: e.GetDecimal(ref o),
                forwardSpread: e.GetDecimal(ref o),
                netSpread: e.GetDecimal(ref o)
            );
    }

    [Fact]
    [Trait("read option trade spread data from CSV file and insert into database", "FundDb")]
    public async Task GetOptionTradeSpreadDataFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\option_trade_spread_data.csv"))
           .ReadAsync(MapToOptionTradeSpreadData, async reducer =>
           {
               await db.Use($"truncate option_trade_spread_data").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertOptionTradeSpreadDataAsync(reducer);
           });

        var resultSet = await dbTrade.GetOptionTradeSpreadDataAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static OptionTradeSpreadsDataModel MapToOptionTradeSpreadData(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                valueDate: e.GetDateOnly(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                sequenceId: e.GetLong(ref o),
                lossLimit: e.GetDecimal(ref o),
                winLimit: e.GetDecimal(ref o),
                forwardSpread: e.GetDecimal(ref o),
                netSpread: e.GetDecimal(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read trade fill from CSV file and insert into database", "FundDb")]
    public async Task GetTradeFillsFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\trade_fill.csv"))
           .ReadAsync(MapToTradeFill, async reducer =>
           {
               await db.Use($"truncate trade_fill").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertTradeFillsAsync(reducer);
           });

        var resultSet = await dbTrade.GetTradeFillsAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static TradeFillReadModel MapToTradeFill(string e, int o)
             => new(
                 orderId: e.GetInt(ref o),
                 tradeId: e.GetInt(ref o),
                 fillDate: e.GetDateTime(ref o),
                 fillQuantity: e.GetInt(ref o),
                 createdOn: e.GetDateTime(ref o),
                 createdBy: e.GetString(ref o)
             );
    }

    [Fact]
    [Trait("read trade limit from CSV file and insert into database", "FundDb")]
    public async Task GetTradeLimitsFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\trade_limit.csv"))
           .ReadAsync(MapToTradeLimit, async reducer =>
           {
               await db.Use($"truncate trade_limit").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertTradeLimitsAsync(reducer);
           });

        var resultSet = await dbTrade.GetTradeLimitsAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static TradeLimitReadModel MapToTradeLimit(string e, int o)
             => new(
                 tradeId: e.GetInt(ref o),
                 tradeType: e.GetEnum<TradeType>(ref o),
                 riskMargin: e.GetDecimal(ref o),
                 maxProfit: e.GetDecimal(ref o),
                 maxLoss: e.GetDecimal(ref o),
                 maxReturn: e.GetDecimal(ref o),
                 maxLossLimit: e.GetDecimal(ref o),
                 minProfitLimit: e.GetDecimal(ref o),
                 maxProfitLimit: e.GetDecimal(ref o),
                 minProfitTarget: e.GetDecimal(ref o),
                 dailyProfitTarget: e.GetDecimal(ref o),
                 createdOn: e.GetDateTime(ref o),
                 createdBy: e.GetString(ref o),
                 updatedOn: e.GetDateTime(ref o),
                 updatedBy: e.GetString(ref o)
             );

    }

    [Fact]
    [Trait("read trade type limit from CSV file and insert into database", "FundDb")]
    public async Task GetTradeTypeLimitsFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\trade_type_limit.csv"))
           .ReadAsync(MapToTradeTypeLimit, async reducer =>
           {
               await db.Use($"truncate trade_type_limit").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertTradeTypeLimitsAsync(reducer);
           });

        var resultSet = await dbTrade.GetTradeTypeLimitsAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static TradeTypeLimitReadModel MapToTradeTypeLimit(string e, int o)
            => new(
                tradeId: e.GetInt(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                maxLossLimit: e.GetDecimal(ref o),
                minProfitLimit: e.GetDecimal(ref o),
                maxProfitLimit: e.GetDecimal(ref o)
            );
    }

    [Fact]
    [Trait("read trade position from CSV file and insert into database", "FundDb")]
    public async Task GetTradePositionsFromCsvFileOk()
    {

        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\trade_position.csv"))
           .ReadAsync(MapToTradePosition, async reducer =>
           {
               await db.Use($"truncate trade_position").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertTradePositionsAsync(reducer);
           });

        var resultSet = await dbTrade.GetTradePositionsAsync();
        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static TradePositionReadModel MapToTradePosition(string e, int o)
            => new(
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                valueDate: e.GetDateOnly(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                tradeStatus: e.GetEnum<TradeStatus>(ref o),
                daysToExpiry: e.GetInt(ref o),
                commission: e.GetDecimal(ref o),
                deltaHedge: e.GetInt(ref o),
                netSpread: e.GetDecimal(ref o),
                tradeValue: e.GetDecimal(ref o),
                tradePnl: e.GetDecimal(ref o),
                assetPrice: e.GetDecimal(ref o),
                otmProbability: e.GetDouble(ref o),
                forwardPrice: e.GetDecimal(ref o),
                forwardLossRatio: e.GetDouble(ref o),
                lossProbability: e.GetDouble(ref o),
                riskFreeRate: e.GetDouble(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o),
                updatedOn: e.GetDateTime(ref o),
                updatedBy: e.GetString(ref o)
            );

    }

    [Fact]
    [Trait("read trade plan from CSV file and insert into database", "FundDb")]
    public async Task GetTradePlansFromCsvFileOk()
    {
        var rowCount = 0L;
        var db = TestFixture.TradeDb;
        var dbTrade = db as ITradeDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\trade_plan.csv"))
           .ReadAsync(MapToTradePlan, async reducer =>
           {
               await db.Use($"truncate trade_plan").ExecuteCommandAsync();
               rowCount = await dbTrade.InsertTradePlansAsync(reducer);
           });

        var resultSet = await dbTrade.GetTradePlansAsync();

        resultSet.Should().NotBeNull();
        rowCount.Should().Be(resultSet.Count);
        return;

        static TradePlanByIdReadModel MapToTradePlan(string e, int o)
            => new(
                id: e.GetInt(ref o),
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                valueDate: e.GetDateOnly(ref o),
                actionDate: e.GetDateTime(ref o),
                tradeDate: e.GetDateOnly(ref o),
                maturityDate: e.GetDateOnly(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                actionType: e.GetEnum<ActionType>(ref o),
                actionSubType: e.GetEnum<ActionSubType>(ref o),
                actionState: e.GetEnum<ActionState>(ref o),
                actionReason: e.GetString(ref o),
                tradePnl: e.GetDecimal(ref o),
                forwardLossRatio: e.GetDouble(ref o),
                lossProbability: e.GetDouble(ref o),
                mScore: e.GetDouble(ref o),
                maxProfit: e.GetDecimal(ref o),
                maxLoss: e.GetDecimal(ref o),
                minProfitTarget: e.GetDecimal(ref o),
                dailyProfitTarget: e.GetDecimal(ref o),
                assetPrice: e.GetDecimal(ref o),
                assetStdDev: e.GetDouble(ref o),
                assetMean: e.GetDouble(ref o),
                assetPriceChange: e.GetDouble(ref o),
                marketTrend: e.GetEnum<MarketDirectionType>(ref o),
                marketVolatility: e.GetEnum<MarketVolatilityType>(ref o),
                marketDirection: e.GetEnum<PriceDirectionType>(ref o),
                vixVolatility: e.GetEnum<PriceVolatilityType>(ref o),
                tradeRisk: e.GetEnum<TradeRiskType>(ref o),
                fiftyDayMA: e.GetDouble(ref o),
                fiveDayXMA: e.GetDouble(ref o),
                putOTMProbability: e.GetDouble(ref o),
                callOTMProbability: e.GetDouble(ref o),
                shortPutGamma: e.GetDouble(ref o),
                shortCallGamma: e.GetDouble(ref o),
                gammaRisk: e.GetEnum<GammaRiskType>(ref o),
                netPrice: e.GetDecimal(ref o),
                forwardPrice: e.GetDecimal(ref o),
                forwardDelta: e.GetDouble(ref o),
                stopLossLimit: e.GetDouble(ref o),
                trendType: e.GetEnum<FuturesTrendType>(ref o),
                trendStrength: e.GetEnum<FuturesTrendStrengthType>(ref o),
                rsi: e.GetDouble(ref o),
                rsiSlope: e.GetDouble(ref o),
                tdi: e.GetEnum<FuturesTrendDirectionType>(ref o),
                tdiStrength: e.GetEnum<FuturesTrendDirectionStrengthType>(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o)
            );
    }
}

