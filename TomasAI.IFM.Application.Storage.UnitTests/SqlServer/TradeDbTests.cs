using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Framework.Storage;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ReferenceDb;

namespace TomasAI.IFM.Application.Storage.UnitTests.SqlServer
{
    public class TradeDatabaseFixture : IDisposable
    {
        public TradeDatabaseFixture()
        {
            var dbConn = new DbConnectionSettings()
                .Add("TradeDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=tradetestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, TradeDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            var logger = Substitute.For<ILogger<TradeDbContext>>();
            logger.When(_ => { }).Do(_ => { });
            diContainer.Add(typeof(IObjectRepository<TradeDbContext>), new TradeDbContext(dbConn, dbFactory, logger));
            Db = dbFactory.TradeDb as TradeDbContext;
        }

        public TradeDbContext Db { get; }

        public void Dispose()
        {
        }
    }

    public class TradeDbTests : IClassFixture<TradeDatabaseFixture>
    {
        private TradeDatabaseFixture _fixture;

        public TradeDbTests(TradeDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetTradeTypeLimitOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use("delete from trade_type_limit").ExecuteCommandAsync();
            await db.Use("insert into trade_type_limit (TradeId, TradeType, MaxLossLimit, MinProfitLimit) values (1038, 'CallCreditSpread', 13, 1.625)").ExecuteCommandAsync();

            // when...
            var tradeTypeLimit = await db.GetTradeTypeLimitAsync(1038, TradeType.CallCreditSpread);

            // then..
            tradeTypeLimit.Should().NotBeNull();
            tradeTypeLimit.TradeId.Should().Be(1038);
            tradeTypeLimit.TradeType.Should().Be(TradeType.CallCreditSpread);
            tradeTypeLimit.MaxLossLimit.Should().Be(13);
            $"{tradeTypeLimit.MinProfitLimit:F3}".Should().Be("1.625");
        }

        [Fact]
        public async Task GetTradeTypeLimitsOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use("delete from trade_type_limit").ExecuteCommandAsync();
            await db.Use("insert into trade_type_limit (TradeId, TradeType, MaxLossLimit, MinProfitLimit) values (1038, 'CallCreditSpread', 13, 1.625)").ExecuteCommandAsync();

            // when...
            var tradeTypeLimits = await db.GetTradeTypeLimitsAsync(1038);

            // then..
            tradeTypeLimits.Should().NotBeNull();
            var tradeTypeLimit = tradeTypeLimits[0];
            tradeTypeLimit.TradeId.Should().Be(1038);
            tradeTypeLimit.TradeType.Should().Be(TradeType.CallCreditSpread);
            tradeTypeLimit.MaxLossLimit.Should().Be(13);
            $"{tradeTypeLimit.MinProfitLimit:F3}".Should().Be("1.625");
        }

        [Fact]
        public async Task InsertTradeTypeLimitOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use("delete from trade_type_limit").ExecuteCommandAsync();
            var tradeTypeLimit = new TradeTypeLimitReadModel(1038, TradeType.CallCreditSpread, 13, 1.625, 1.625);

            // when...
            await db.InsertTradeTypeLimitAsync(tradeTypeLimit);
            var tradeTypeLimits = await db.GetTradeTypeLimitsAsync(1038);

            // then..
            tradeTypeLimits.Should().NotBeNull();
            tradeTypeLimit = tradeTypeLimits[0];
            tradeTypeLimit.TradeId.Should().Be(1038);
            tradeTypeLimit.TradeType.Should().Be(TradeType.CallCreditSpread);
            tradeTypeLimit.MaxLossLimit.Should().Be(13);
            $"{tradeTypeLimit.MinProfitLimit:F3}".Should().Be("1.625");
        }

        [Fact]
        public async Task GetTradeFillOk()
        {
            var db = _fixture.Db;
            await db.Use("delete from trade_fill").ExecuteCommandAsync();
            await db.Use("insert into trade_fill (FundId, OrderId, TradeId, FillDate, FillQuantity, CreatedOn, CreatedBy) values (10, 20, 1038, '2018-11-15', 7, '2018-11-15','basilt')").ExecuteCommandAsync();
            var tradeFills = await db.GetTradeFillsAsync(1038);
            tradeFills.Should().NotBeNull();
            tradeFills.Count.Should().Be(1);
            var tradeFill = tradeFills[0];
            tradeFill.FundId.Should().Be(10);
            tradeFill.OrderId.Should().Be(20);
            tradeFill.TradeId.Should().Be(1038);
            $"{tradeFill.FillDate:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFill.FillQuantity.Should().Be(7);
            $"{tradeFill.CreatedOn:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFill.CreatedBy.Should().Be("basilt");
        }

        [Fact]
        public async Task InsertTradeFillAsync()
        {
            var db = _fixture.Db;
            await db.Use("delete from trade_fill").ExecuteCommandAsync();
            var tradeFill = new TradeFillReadModel(
                FundId: 10,
                OrderId: 20,
                TradeId: 1038,
                FillDate: new DateTime(2018, 11, 15),
                FillQuantity: 7,
                CreatedOn: new DateTime(2018, 11, 15),
                CreatedBy: "basilt");
            await db.InsertTradeFillsAsync(new TradeFillReadModel[] { tradeFill });
            var tradeFills = await db.GetTradeFillsAsync(1038);
            tradeFills.Should().NotBeNull();
            tradeFills.Count.Should().Be(1);
            tradeFill = tradeFills[0];
            tradeFill.FundId.Should().Be(10);
            tradeFill.OrderId.Should().Be(20);
            tradeFill.TradeId.Should().Be(1038);
            $"{tradeFill.FillDate:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFill.FillQuantity.Should().Be(7);
            $"{tradeFill.CreatedOn:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFill.CreatedBy.Should().Be("basilt");
        }

        [Fact]
        public async Task GetTradeFillDataOk()
        {
            var db = _fixture.Db;
            await db.Use("delete from trade_fill").ExecuteCommandAsync();
            await db.Use("insert into trade_fill (FundId, OrderId, TradeId, FillDate, FillQuantity, CreatedOn, CreatedBy) values (10, 20, 1038, '2018-11-15', 7, '2018-11-15','basilt')").ExecuteCommandAsync();
            await db.Use("delete from trade_fill_data").ExecuteCommandAsync();
            await db.Use("insert into trade_fill_data (FundId, OrderId, TradeId, ContractId, FillDate, BidPrice, AskPrice, Commission, OptionLegAction, CreatedOn, CreatedBy) values (10, 20, 1038, 'ES20220429C4850', '2018-11-15', 10.00, 10.25, 15.25,'Short', '2018-11-15','basilt')").ExecuteCommandAsync();
            var tradeFills = await db.GetTradeFillsAsync(1038);
            tradeFills.Should().NotBeNull();
            tradeFills.Count.Should().Be(1);
            var tradeFill = tradeFills[0];
            tradeFill.FundId.Should().Be(10);
            tradeFill.OrderId.Should().Be(20);
            tradeFill.TradeId.Should().Be(1038);
            $"{tradeFill.FillDate:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFill.FillQuantity.Should().Be(7);
            $"{tradeFill.CreatedOn:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFill.CreatedBy.Should().Be("basilt");
            tradeFill.TradeFillData.Should().NotBeNull();
            tradeFill.TradeFillData.Count().Should().Be(1);
            var tradeFillData = tradeFill.TradeFillData[0];
            tradeFillData.FundId.Should().Be(10);
            tradeFillData.OrderId.Should().Be(20);
            tradeFillData.TradeId.Should().Be(1038);
            tradeFillData.ContractId.Should().Be("ES20220429C4850");
            $"{tradeFillData.FillDate:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFillData.BidPrice.Should().Be(10.00M);
            tradeFillData.AskPrice.Should().Be(10.25M);
            tradeFillData.Commission.Should().Be(15.25M);
            tradeFillData.OptionLegAction.Should().Be(OptionLegAction.Short);
            $"{tradeFillData.CreatedOn:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFillData.CreatedBy.Should().Be("basilt");
        }

        [Fact]
        public async Task InsertTradeFillDataAsync()
        {
            var db = _fixture.Db;
            await db.Use("delete from trade_fill").ExecuteCommandAsync();
            await db.Use("delete from trade_fill_data").ExecuteCommandAsync();
            var tradeFill = new TradeFillReadModel(
                FundId: 10,
                OrderId: 20,
                TradeId: 1038,
                FillDate: new DateTime(2018, 11, 15),
                FillQuantity: 7,
                CreatedOn: new DateTime(2018, 11, 15),
                CreatedBy: "basilt").AddTradeFillData(new TradeFillDataReadModel[]
                {
                    new TradeFillDataReadModel(
                        FundId: 10,
                        OrderId: 20,
                        TradeId: 1038,
                        ContractId: "ES20220429C4850",
                        FillDate: new DateTime(2018, 11, 15),
                        BidPrice: 10.00M,
                        AskPrice: 10.25M,
                        Commission: 15.25M,
                        OptionLegAction: OptionLegAction.Short,
                        CreatedOn: new DateTime(2018,11,15),
                        CreatedBy: "basilt"
                    )
                });
            await db.InsertTradeFillsAsync(new TradeFillReadModel[] { tradeFill });
            var tradeFills = await db.GetTradeFillsAsync(1038);
            tradeFills.Should().NotBeNull();
            tradeFills.Count.Should().Be(1);
            tradeFill = tradeFills[0];
            tradeFill.FundId.Should().Be(10);
            tradeFill.OrderId.Should().Be(20);
            tradeFill.TradeId.Should().Be(1038);
            $"{tradeFill.FillDate:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFill.FillQuantity.Should().Be(7);
            $"{tradeFill.CreatedOn:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFill.CreatedBy.Should().Be("basilt");
            tradeFill.TradeFillData.Should().NotBeNull();
            tradeFill.TradeFillData.Count().Should().Be(1);
            var tradeFillData = tradeFill.TradeFillData[0];
            tradeFillData.FundId.Should().Be(10);
            tradeFillData.OrderId.Should().Be(20);
            tradeFillData.TradeId.Should().Be(1038);
            tradeFillData.ContractId.Should().Be("ES20220429C4850");
            $"{tradeFillData.FillDate:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFillData.BidPrice.Should().Be(10.00M);
            tradeFillData.AskPrice.Should().Be(10.25M);
            tradeFillData.Commission.Should().Be(15.25M);
            tradeFillData.OptionLegAction.Should().Be(OptionLegAction.Short);
            $"{tradeFillData.CreatedOn:yyyy-MM-dd}".Should().Be("2018-11-15");
            tradeFillData.CreatedBy.Should().Be("basilt");
        }

        [Fact]
        public async Task GetTradeLiveFeedOk()
        {
            var db = _fixture.Db;
            await db.Use("delete from trade_live_feed").ExecuteCommandAsync();
            await db.Use("insert into trade_live_feed (OrderId, TradeId, LiveFeed) values (1001, 2002, 1)").ExecuteCommandAsync();
            var tradeLiveFeeds = await db.GetTradeLiveFeedAsync(1001, 2002);
            Assert.NotNull(tradeLiveFeeds);
            Assert.Equal(1, tradeLiveFeeds.Count);
            Assert.Equal(1001, tradeLiveFeeds.ElementAt(0).OrderId);
            Assert.Equal(2002, tradeLiveFeeds.ElementAt(0).TradeId);
            Assert.True(tradeLiveFeeds.ElementAt(0).LiveFeed);
        }

        [Fact]
        public async Task InsertTradeLiveFeedOk()
        {
            var db = _fixture.Db;
            await db.Use("delete from trade_live_feed").ExecuteCommandAsync();
            var tradeLiveFeed = new TradeLiveFeedReadModel(1001, 2002, false);
            await db.InsertTradeLiveFeedAsync(tradeLiveFeed);
            var tradeLiveFeeds = await db.GetTradeLiveFeedAsync(1001, 2002);
            Assert.NotNull(tradeLiveFeeds);
            Assert.Equal(1, tradeLiveFeeds.Count);
            Assert.Equal(1001, tradeLiveFeeds.ElementAt(0).OrderId);
            Assert.Equal(2002, tradeLiveFeeds.ElementAt(0).TradeId);
            Assert.False(tradeLiveFeeds.ElementAt(0).LiveFeed);
        }

        [Fact]
        public async Task DeleteTradeLiveFeedOk()
        {
            var db = _fixture.Db;
            await db.Use("delete from trade_live_feed").ExecuteCommandAsync();
            var tradeLiveFeed = new TradeLiveFeedReadModel(1001, 2002, false);
            await db.InsertTradeLiveFeedAsync(tradeLiveFeed);
            var tradeLiveFeeds = await db.GetTradeLiveFeedAsync(1001, 2002);
            Assert.NotNull(tradeLiveFeeds);
            Assert.Equal(1, tradeLiveFeeds.Count);
            Assert.Equal(1001, tradeLiveFeeds.ElementAt(0).OrderId);
            Assert.Equal(2002, tradeLiveFeeds.ElementAt(0).TradeId);
            Assert.False(tradeLiveFeeds.ElementAt(0).LiveFeed);
            await db.DeleteTradeLiveFeedAsync(1001, 2002);
            tradeLiveFeeds = await db.GetTradeLiveFeedAsync(1001, 2002);
            Assert.NotNull(tradeLiveFeeds);
            Assert.Equal(0, tradeLiveFeeds.Count);
        }

        [Fact]
        public async Task GetTradePlansOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use("delete from trade_plan").ExecuteCommandAsync();
            await db.InsertTradePlanAsync(SampleData.TradePlan);

            // when...
            var tradePlans = await db.GetTradePlansAsync(startDate: SampleData.TradePlan.ValueDate.AddDays(-10), endDate: SampleData.TradePlan.ValueDate.AddDays(10));

            // then...
            tradePlans.Should().NotBeNull();
            tradePlans.Count.Should().Be(1);
            var tradePlan = tradePlans[0];
            tradePlan.OrderId.Should().Be(SampleData.TradePlan.OrderId);
            tradePlan.TradeId.Should().Be(SampleData.TradePlan.TradeId);
            tradePlan.TradeType.Should().Be(SampleData.TradePlan.TradeType);
            $"{tradePlan.TradeDate:yyyy-MM-dd}".Should().Be($"{SampleData.TradePlan.TradeDate:yyyy-MM-dd}");
            $"{tradePlan.ValueDate:yyyy-MM-dd}".Should().Be($"{SampleData.TradePlan.ValueDate:yyyy-MM-dd}");
            $"{tradePlan.MaturityDate:yyyy-MM-dd}".Should().Be($"{SampleData.TradePlan.MaturityDate:yyyy-MM-dd}");
            $"{tradePlan.ActionDate:yyyy-MM-dd}".Should().Be($"{SampleData.TradePlan.ActionDate:yyyy-MM-dd}");
            tradePlan.ActionType.Should().Be(SampleData.TradePlan.ActionType);
            tradePlan.ActionState.Should().Be(SampleData.TradePlan.ActionState);
            tradePlan.ActionReason.Should().Be(SampleData.TradePlan.ActionReason);
            $"{tradePlan.TradePnl:F2}".Should().Be($"{SampleData.TradePlan.TradePnl:F2}");
            $"{tradePlan.ForwardLossRatio:F4}".Should().Be($"{SampleData.TradePlan.ForwardLossRatio:F4}");
            $"{tradePlan.LossProbability:F4}".Should().Be($"{SampleData.TradePlan.LossProbability:F4}");
            $"{tradePlan.MaxProfit:F2}".Should().Be($"{SampleData.TradePlan.MaxProfit:F2}");
            $"{tradePlan.MaxLoss:F2}".Should().Be($"{SampleData.TradePlan.MaxLoss:F2}");
        }

        [Fact]
        public async Task GetTradePlanForwardLossRatiosOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use("delete from trade_plan").ExecuteCommandAsync();
            await db.InsertTradePlanAsync(SampleData.TradePlan);

            // when...
            var tradePlanForwardLosRatios = await db.GetTradePlanForwardLossRatiosAsync(startDate: SampleData.TradePlan.ValueDate.AddDays(-10), endDate: SampleData.TradePlan.ValueDate.AddDays(10));

            // then...
            tradePlanForwardLosRatios.Should().NotBeNull();
            tradePlanForwardLosRatios.Count.Should().Be(1);
            var tradePlanForwardLosRatio = tradePlanForwardLosRatios[0];
            $"{tradePlanForwardLosRatio.ForwardLossRatio:F4}".Should().Be($"{SampleData.TradePlan.ForwardLossRatio:F4}");
        }

        [Fact]
        public async Task InsertTradeOrderOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use("delete from trade_order").ExecuteCommandAsync();

            // when...
            await db.InsertTradeOrderAsync(SampleData.TradeOrder);

            // then..
            var fundId = SampleData.TradeOrder.FundId;
            var orderId = SampleData.TradeOrder.OrderId;
            var tradeId = SampleData.TradeOrder.TradeId;
            var rowCount = await db.Use($"select count(*) from trade_order where FundId = {fundId} and OrderId = {orderId} and TradeId = {tradeId}").ExecuteScalarAsync<int>();
            rowCount.Should().Be(1);

            await db.Use("delete from trade_order").ExecuteCommandAsync();
        }

        [Fact]
        public async Task GetTradeOrderOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use("delete from trade_order").ExecuteCommandAsync();
            await db.InsertTradeOrderAsync(SampleData.TradeOrder);

            // when...
            var fundId = SampleData.TradeOrder.FundId;
            var orderId = SampleData.TradeOrder.OrderId;
            var tradeId = SampleData.TradeOrder.TradeId;
            var result = await db.GetTradeOrderAsync(fundId, orderId, tradeId);

            // then..
            result.Should().NotBeNull();
            result.FundId.Should().Be(SampleData.TradeOrder.FundId);
            result.OrderId.Should().Be(SampleData.TradeOrder.OrderId);
            result.TradeId.Should().Be(SampleData.TradeOrder.TradeId);

            await db.Use("delete from trade_order").ExecuteCommandAsync();
        }

        [Fact]
        public async Task GetTradeOrdersOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use("delete from trade_order").ExecuteCommandAsync();
            await db.InsertTradeOrderAsync(SampleData.TradeOrder);

            // when...
            var fundId = SampleData.TradeOrder.FundId;
            var valueDate = SampleData.TradeOrder.ValueDate;
            var result = await db.GetTradeOrdersAsync(fundId, valueDate);

            // then..
            result.Should().NotBeNull();
            result.Count().Should().Be(1);
            var e = result.ElementAt(0);
            e.FundId.Should().Be(SampleData.TradeOrder.FundId);
            e.ValueDate.Should().Be(SampleData.TradeOrder.ValueDate);

            await db.Use("delete from trade_order").ExecuteCommandAsync();
        }

        [Fact]
        public async Task UpdateTradeOrderStateOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use("delete from trade_order").ExecuteCommandAsync();
            await db.InsertTradeOrderAsync(SampleData.TradeOrder);

            // when...
            var fundId = SampleData.TradeOrder.EntityId.FundId;
            var orderId = SampleData.TradeOrder.EntityId.OrderId;
            var tradeId = SampleData.TradeOrder.EntityId.TradeId;
            var tradeOrderState = TradeOrderState.OrderFilled;
            var updatedOn = new DateTime(2020, 12, 07);
            var updatedBy = "basilt";
            await db.UpdateTradeOrderStateAsync(SampleData.TradeOrder.EntityId, tradeOrderState, updatedOn, updatedBy);

            // then..
            var result = await db.GetTradeOrderAsync(fundId, orderId, tradeId);
            result.Should().NotBeNull();
            result.FundId.Should().Be(SampleData.TradeOrder.EntityId.FundId);
            result.OrderId.Should().Be(SampleData.TradeOrder.EntityId.OrderId);
            result.TradeId.Should().Be(SampleData.TradeOrder.EntityId.TradeId);
            result.TradeOrderState.Should().NotBe(SampleData.TradeOrder.TradeOrderState);
            result.TradeOrderState.Should().Be(TradeOrderState.OrderFilled);
            result.UpdatedOn.Should().Be(updatedOn);
            result.UpdatedBy.Should().Be(updatedBy);

            await db.Use("delete from trade_order").ExecuteCommandAsync();
        }

        [Fact]
        public async Task UpdateTradeOrderOrderPriceOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use("delete from trade_order").ExecuteCommandAsync();
            await db.InsertTradeOrderAsync(SampleData.TradeOrder);

            // when...
            var fundId = SampleData.TradeOrder.EntityId.FundId;
            var orderId = SampleData.TradeOrder.EntityId.OrderId;
            var tradeId = SampleData.TradeOrder.EntityId.TradeId;
            var orderPrice = SampleData.TradeOrder.OrderPrice;
            var updatedOn = new DateTime(2020, 12, 07);
            var updatedBy = "basilt";
            await db.UpdateTradeOrderOrderPriceAsync(SampleData.TradeOrder.EntityId, 1234.89m, updatedOn, updatedBy);

            // then..
            var result = await db.GetTradeOrderAsync(fundId, orderId, tradeId);
            result.Should().NotBeNull();
            result.FundId.Should().Be(SampleData.TradeOrder.EntityId.FundId);
            result.OrderId.Should().Be(SampleData.TradeOrder.EntityId.OrderId);
            result.TradeId.Should().Be(SampleData.TradeOrder.EntityId.TradeId);
            result.OrderPrice.Should().NotBe(SampleData.TradeOrder.OrderPrice);
            result.OrderPrice.Should().Be(1234.89m);
            result.UpdatedOn.Should().Be(updatedOn);
            result.UpdatedBy.Should().Be(updatedBy);

            await db.Use("delete from trade_order").ExecuteCommandAsync();
        }
    }
}
