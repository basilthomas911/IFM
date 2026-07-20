using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Event.Denormalizer.UnitTests
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
            diContainer.Add(typeof(IObjectRepository<TradeDbContext>), new TradeDbContext(dbConn, dbFactory));
            Db = dbFactory.TradeDb as TradeDbContext;
        }

        public TradeDbContext Db { get; }

        public void Dispose()
        {
        }
    }

    public class TradeEventDenormalizerTests : IClassFixture<TradeDatabaseFixture>
    {

        private TradeDatabaseFixture _fixture;

        public TradeEventDenormalizerTests(TradeDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        /*
        IAsyncEventHandler<OptionTradeOrderPlacedEvent>,
        IAsyncEventHandler<OptionTradeOrderFilledEvent>,
        IAsyncEventHandler<TradePositionAddedEvent>,
        IAsyncEventHandler<TradePositionUpdatedEvent>,
        IAsyncEventHandler<TradePositionStatusUpdatedEvent>,
        IAsyncEventHandler<OptionTradeDailyProfitTargetUpdatedEvent>,
        IAsyncEventHandler<TradePlanUpdatedEvent>         */

        [Fact]
        public async Task ExecuteOptionTradeOrderPlacedEvent()
        {
            // given...
            var orderId = TestDataDefaults.OptionTrade.OrderId;
            var tradeId = TestDataDefaults.OptionTrade.TradeId;
            await _fixture.Db.Use($"delete from option_trade where OrderId = {orderId} and TradeId = {tradeId}").ExecuteCommandAsync();
   
            var completedEvent = default(OptionTradeOrderPlacedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OptionTradeOrderPlacedCompleteEvent>()))
                .Callback<OptionTradeOrderPlacedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var optionTrade = TestDataDefaults.OptionTrade;
            optionTrade.AddOptionLegs(new OptionLegReadModel[] { TestDataDefaults.OptionLeg });
            optionTrade.AddTradePosition(new TradePositionReadModel[] { TestDataDefaults.TradePosition });
            optionTrade.SetTradeLimit(TestDataDefaults.TradeLimit);
            optionTrade.AddTradeTypeLimits(new TradeTypeLimitReadModel[] { TestDataDefaults.TradeTypeLimit });
            optionTrade.AddTradeFills(TestDataDefaults.TradeFills);
            var optionTradeOrderPlaced = new OptionTradeOrderPlacedEvent
            {
                CommandId = Guid.NewGuid(),
                OptionTrade = optionTrade,
                OrderAction = Shared.Trade.OrderActionType.Open,
                OrderPrice = 2345.65m,
                OrderType = OrderType.Limit,
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2020,10,10)
            };

            // when...
             await denormalizer.ExecuteAsync(optionTradeOrderPlaced);

            // then...
            optionTrade = await (_fixture.Db.Use($"select *  from option_trade where OrderId = {orderId} and TradeId = {tradeId}")).ExecuteSingleAsync<OptionTradeViewModel>();
            optionTrade.Should().NotBeNull();
            optionTrade.OrderId.Should().Be(TestDataDefaults.OptionTrade.OrderId);
            optionTrade.TradeId.Should().Be(TestDataDefaults.OptionTrade.TradeId);
            optionTrade.TradeStrategy.Should().Be(TestDataDefaults.OptionTrade.TradeStrategy);
            optionTrade.TradeDate.Should().Be(TestDataDefaults.OptionTrade.TradeDate);
            optionTrade.MaturityDate.Should().Be(TestDataDefaults.OptionTrade.MaturityDate);
            optionTrade.TradeType.Should().Be(TestDataDefaults.OptionTrade.TradeType);
            optionTrade.TradeState.Should().Be(TestDataDefaults.OptionTrade.TradeState);
            optionTrade.TradeAction.Should().Be(TestDataDefaults.OptionTrade.TradeAction);
            optionTrade.UnderlyingContractId.Should().Be(TestDataDefaults.OptionTrade.UnderlyingContractId);
            optionTrade.UnderlyingAssetType.Should().Be(TestDataDefaults.OptionTrade.UnderlyingAssetType);
            optionTrade.IsPrimaryTrade.Should().Be(TestDataDefaults.OptionTrade.IsPrimaryTrade);
            optionTrade.IsHedgeTrade.Should().Be(TestDataDefaults.OptionTrade.IsHedgeTrade);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(optionTradeOrderPlaced.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(optionTradeOrderPlaced.CommandId);
        }

        [Fact]
        public async Task ExecuteOptionTradeOrderPlacedEventWithEmptyCommandId()
        {
            // given...
            var orderId = TestDataDefaults.OptionTrade.OrderId;
            var tradeId = TestDataDefaults.OptionTrade.TradeId;
            await _fixture.Db.Use($"delete from option_trade where OrderId = {orderId} and TradeId = {tradeId}").ExecuteCommandAsync();

            var failedEvent = default(OptionTradeOrderPlacedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OptionTradeOrderPlacedFailEvent>()))
                .Callback<OptionTradeOrderPlacedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var optionTrade = TestDataDefaults.OptionTrade;
            optionTrade.AddOptionLegs(new OptionLegReadModel[] { TestDataDefaults.OptionLeg });
            optionTrade.AddTradePosition(new TradePositionReadModel[] { TestDataDefaults.TradePosition });
            optionTrade.SetTradeLimit(TestDataDefaults.TradeLimit);
            optionTrade.AddTradeTypeLimits(new TradeTypeLimitReadModel[] { TestDataDefaults.TradeTypeLimit });
            optionTrade.AddTradeFills(TestDataDefaults.TradeFills);

            var optionTradeOrderPlaced = new OptionTradeOrderPlacedEvent
            {
                OptionTrade = optionTrade,
                OrderAction = Shared.Trade.OrderActionType.Open,
                OrderPrice = 2345.65m,
                OrderType = OrderType.Limit,
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2020, 10, 10)
            };

            // when...
            await denormalizer.ExecuteAsync(optionTradeOrderPlaced);

            // then...
            optionTrade = await (_fixture.Db.Use($"select *  from option_trade where OrderId = {orderId} and TradeId = {tradeId}")).ExecuteSingleAsync<OptionTradeViewModel>();
            optionTrade.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(optionTradeOrderPlaced.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(optionTradeOrderPlaced.CommandId);
        }


        [Fact]
        public async Task ExecuteTradePositionAddedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}").ExecuteCommandAsync();

            var completedEvent = default(TradePositionAddedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradePositionAddedCompleteEvent>()))
                .Callback<TradePositionAddedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var tradePositionAdded = new TradePositionAddedEvent
            {
                CommandId = Guid.NewGuid(),
                OrderId = 1200,
                AssetPrice = TestDataDefaults.TradePosition.AssetPrice,
                RiskFreeRate = TestDataDefaults.TradePosition.RiskFreeRate,
                TradePosition = TestDataDefaults.TradePosition,
                CreatedBy = TestDataDefaults.TradePosition.CreatedBy,
                CreatedOn = TestDataDefaults.TradePosition.CreatedOn
            };

            // when...
            await denormalizer.ExecuteAsync(tradePositionAdded);

            // then...
            var tradePosition = await (_fixture.Db.Use($"select * from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}")).ExecuteSingleAsync<TradePositionReadModel>();
            tradePosition.Should().NotBeNull();
            tradePosition.TradeId.Should().Be(TestDataDefaults.TradePosition.TradeId);
            tradePosition.TradeType.Should().Be(TestDataDefaults.TradePosition.TradeType);
            tradePosition.ValueDate.Should().Be(TestDataDefaults.TradePosition.ValueDate);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(tradePositionAdded.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(tradePositionAdded.CommandId);
        }

        [Fact]
        public async Task ExecuteTradePositionAddedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}").ExecuteCommandAsync();

            var failedEvent = default(TradePositionAddedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradePositionAddedFailEvent>()))
                .Callback<TradePositionAddedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var tradePositionAdded = new TradePositionAddedEvent
            {
                OrderId = 1200,
                AssetPrice = TestDataDefaults.TradePosition.AssetPrice,
                RiskFreeRate = TestDataDefaults.TradePosition.RiskFreeRate,
                TradePosition = TestDataDefaults.TradePosition,
                CreatedBy = TestDataDefaults.TradePosition.CreatedBy,
                CreatedOn = TestDataDefaults.TradePosition.CreatedOn
            };

            // when...
            await denormalizer.ExecuteAsync(tradePositionAdded);

            // then...
            var tradePosition = await (_fixture.Db.Use($"select * from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}")).ExecuteSingleAsync<TradePositionReadModel>();
            tradePosition.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(tradePositionAdded.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(tradePositionAdded.CommandId);
        }

        [Fact]
        public async Task ExecuteTradePositionUpdatedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}").ExecuteCommandAsync();

            var mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradePositionAddedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var tradePositionAdded = new TradePositionAddedEvent
            {
                CommandId = Guid.NewGuid(),
                OrderId = 1200,
                AssetPrice = TestDataDefaults.TradePosition.AssetPrice,
                RiskFreeRate = TestDataDefaults.TradePosition.RiskFreeRate,
                TradePosition = TestDataDefaults.TradePosition,
                CreatedBy = TestDataDefaults.TradePosition.CreatedBy,
                CreatedOn = TestDataDefaults.TradePosition.CreatedOn
            };
            await denormalizer.ExecuteAsync(tradePositionAdded);

            // when...
            var completedEvent = default(TradePositionUpdatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradePositionUpdatedCompleteEvent>()))
                .Callback<TradePositionUpdatedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var tradePositionUpdated = new TradePositionUpdatedEvent
            {
                CommandId = Guid.NewGuid(),
                TradePositionChangeSource = TradePositionChangeSourceType.SpreadDistributionStatistics,
                OptionLegId = "ES20181130C2800",
                CallTradePosition = TestDataDefaults.TradePosition,
                PutTradePosition = TestDataDefaults.TradePosition,
                UpdatedBy = "basilt",
                UpdatedOn = new DateTime(2020,10,10)
            };
            await denormalizer.ExecuteAsync(tradePositionUpdated);

            // then...
            var tradePosition = await (_fixture.Db.Use($"select * from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}")).ExecuteSingleAsync<TradePositionReadModel>();
            tradePosition.Should().NotBeNull();
            tradePosition.TradeId.Should().Be(TestDataDefaults.TradePosition.TradeId);
            tradePosition.TradeType.Should().Be(TestDataDefaults.TradePosition.TradeType);
            tradePosition.ValueDate.Should().Be(TestDataDefaults.TradePosition.ValueDate);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(tradePositionUpdated.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(tradePositionUpdated.CommandId);
        }

        [Fact]
        public async Task ExecuteTradePositionUpdatedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}").ExecuteCommandAsync();

            var mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradePositionAddedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var tradePositionAdded = new TradePositionAddedEvent
            {
                CommandId = Guid.NewGuid(),
                OrderId = 1200,
                AssetPrice = TestDataDefaults.TradePosition.AssetPrice,
                RiskFreeRate = TestDataDefaults.TradePosition.RiskFreeRate,
                TradePosition = TestDataDefaults.TradePosition,
                CreatedBy = TestDataDefaults.TradePosition.CreatedBy,
                CreatedOn = TestDataDefaults.TradePosition.CreatedOn
            };
            await denormalizer.ExecuteAsync(tradePositionAdded);

            // when...
            var failedEvent = default(TradePositionUpdatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradePositionUpdatedFailEvent>()))
                .Callback<TradePositionUpdatedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var tradePositionUpdated = new TradePositionUpdatedEvent
            {
                TradePositionChangeSource = TradePositionChangeSourceType.SpreadDistributionStatistics,
                OptionLegId = "ES20181130C2800",
                CallTradePosition = TestDataDefaults.TradePosition,
                PutTradePosition = TestDataDefaults.TradePosition,
                UpdatedBy = "basilt",
                UpdatedOn = new DateTime(2020, 10, 10)
            };
            await denormalizer.ExecuteAsync(tradePositionUpdated);

            // then...
            var tradePosition = await (_fixture.Db.Use($"select * from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}")).ExecuteSingleAsync<TradePositionReadModel>();
            tradePosition.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(tradePositionUpdated.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(tradePositionUpdated.CommandId);
        }

        [Fact]
        public async Task ExecuteTradePositionStatusUpdatedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}").ExecuteCommandAsync();

            var mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradePositionAddedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var tradePositionAdded = new TradePositionAddedEvent
            {
                CommandId = Guid.NewGuid(),
                OrderId = 1200,
                AssetPrice = TestDataDefaults.TradePosition.AssetPrice,
                RiskFreeRate = TestDataDefaults.TradePosition.RiskFreeRate,
                TradePosition = TestDataDefaults.TradePosition,
                CreatedBy = TestDataDefaults.TradePosition.CreatedBy,
                CreatedOn = TestDataDefaults.TradePosition.CreatedOn
            };
            await denormalizer.ExecuteAsync(tradePositionAdded);

            // when...
            var completedEvent = default(TradePositionStatusUpdatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradePositionStatusUpdatedCompleteEvent>()))
                .Callback<TradePositionStatusUpdatedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var tradePositionStatusUpdated = new TradePositionStatusUpdatedEvent
            {
                CommandId = Guid.NewGuid(),
                TradeId = TestDataDefaults.TradePosition.TradeId,
                TradeType = TestDataDefaults.TradePosition.TradeType,
                ValueDate = TestDataDefaults.TradePosition.ValueDate,
                DaysToExpiry = TestDataDefaults.TradePosition.DaysToExpiry,
                OldTradeStatus = TestDataDefaults.TradePosition.TradeStatus,
                NewTradeStatus = TradeStatus.Close,
                UpdatedBy = "basilt",
                UpdatedOn = new DateTime(2020, 10, 10)
            };
            await denormalizer.ExecuteAsync(tradePositionStatusUpdated);

            // then...
            var tradePosition = await (_fixture.Db.Use($"select * from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}")).ExecuteSingleAsync<TradePositionReadModel>();
            tradePosition.Should().NotBeNull();
            tradePosition.TradeId.Should().Be(TestDataDefaults.TradePosition.TradeId);
            tradePosition.TradeType.Should().Be(TestDataDefaults.TradePosition.TradeType);
            tradePosition.ValueDate.Should().Be(TestDataDefaults.TradePosition.ValueDate);
            tradePosition.TradeStatus.Should().Be(TradeStatus.Close);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(tradePositionStatusUpdated.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(tradePositionStatusUpdated.CommandId);
        }

        [Fact]
        public async Task ExecuteTradePositionStatusUpdatedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}").ExecuteCommandAsync();

            var mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradePositionAddedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var tradePositionAdded = new TradePositionAddedEvent
            {
                CommandId = Guid.NewGuid(),
                OrderId = 1200,
                AssetPrice = TestDataDefaults.TradePosition.AssetPrice,
                RiskFreeRate = TestDataDefaults.TradePosition.RiskFreeRate,
                TradePosition = TestDataDefaults.TradePosition,
                CreatedBy = TestDataDefaults.TradePosition.CreatedBy,
                CreatedOn = TestDataDefaults.TradePosition.CreatedOn
            };
            await denormalizer.ExecuteAsync(tradePositionAdded);

            // when...
            var failedEvent = default(TradePositionStatusUpdatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradePositionStatusUpdatedFailEvent>()))
                .Callback<TradePositionStatusUpdatedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            var tradePositionStatusUpdated = new TradePositionStatusUpdatedEvent
            {
                TradeId = TestDataDefaults.TradePosition.TradeId,
                TradeType = TestDataDefaults.TradePosition.TradeType,
                ValueDate = TestDataDefaults.TradePosition.ValueDate,
                DaysToExpiry = TestDataDefaults.TradePosition.DaysToExpiry,
                OldTradeStatus = TestDataDefaults.TradePosition.TradeStatus,
                NewTradeStatus = TradeStatus.Close,
                UpdatedBy = "basilt",
                UpdatedOn = new DateTime(2020, 10, 10)
            };
            await denormalizer.ExecuteAsync(tradePositionStatusUpdated);

            // then...
            var tradePosition = await (_fixture.Db.Use($"select * from trade_position where TradeId = {TestDataDefaults.TradePosition.TradeId}")).ExecuteSingleAsync<TradePositionReadModel>();
            tradePosition.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(tradePositionStatusUpdated.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(tradePositionStatusUpdated.CommandId);
        }

        [Fact]
        public async Task ExecuteOptionTradeDailyProfitTargetUpdatedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from trade_limit where TradeId = {TestDataDefaults.TradePosition.TradeId}").ExecuteCommandAsync();
            await _fixture.Db.Use($@"
                INSERT INTO[dbo].[trade_limit]
                           ([TradeId]
                           ,[TradeType]
                           ,[RiskMargin]
                           ,[MaxProfit]
                           ,[MaxLoss]
                           ,[MaxReturn]
                           ,[MaxLossLimit]
                           ,[MinProfitLimit]
                           ,[MinProfitTarget]
                           ,[DailyProfitTarget]
                           ,[CreatedOn]
                           ,[CreatedBy]
                           ,[UpdatedOn]
                           ,[UpdatedBy])
                     VALUES
                           ({TestDataDefaults.TradePosition.TradeId}
                           ,'IronCondor'
                           ,46116.00
                           ,3187.50
                           ,-3187.50
                           ,0.05866949
                           ,4.25
                           ,0.53125
                           ,1934.55
                           ,1137.675
                           ,'2020-01-10'
                           ,'basil'
                           ,'2020-01-12'
                           ,'basil')
                ").ExecuteCommandAsync();

            var completedEvent = default(OptionTradeDailyProfitTargetUpdatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OptionTradeDailyProfitTargetUpdatedCompleteEvent>()))
                .Callback<OptionTradeDailyProfitTargetUpdatedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            // when...
            var optionTradeDailyProfitTargetUpdated = new OptionTradeDailyProfitTargetUpdatedEvent
            {
                CommandId = Guid.NewGuid(),
                OrderId = 1200,
                TradeId = TestDataDefaults.TradeLimit.TradeId,
                TradeType = TestDataDefaults.TradeLimit.TradeType,
                DailyProfitTarget = 693.56m,
                UpdatedBy = TestDataDefaults.TradeLimit.UpdatedBy,
                UpdatedOn = TestDataDefaults.TradeLimit.UpdatedOn
            };
            await denormalizer.ExecuteAsync(optionTradeDailyProfitTargetUpdated);

            // then...
            var tradeLimit = await (_fixture.Db.Use($"select * from trade_limit where TradeId = {TestDataDefaults.TradeLimit.TradeId}")).ExecuteSingleAsync<TradeLimitReadModel>();
            tradeLimit.Should().NotBeNull();
            tradeLimit.TradeId.Should().Be(TestDataDefaults.TradeLimit.TradeId);
            tradeLimit.DailyProfitTarget.Should().Be(693.56m);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(optionTradeDailyProfitTargetUpdated.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(optionTradeDailyProfitTargetUpdated.CommandId);
        }

        [Fact]
        public async Task ExecuteOptionTradeDailyProfitTargetUpdatedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from trade_limit where TradeId = {TestDataDefaults.TradePosition.TradeId}").ExecuteCommandAsync();
            await _fixture.Db.Use($@"
                INSERT INTO[dbo].[trade_limit]
                           ([TradeId]
                           ,[TradeType]
                           ,[RiskMargin]
                           ,[MaxProfit]
                           ,[MaxLoss]
                           ,[MaxReturn]
                           ,[MaxLossLimit]
                           ,[MinProfitLimit]
                           ,[MinProfitTarget]
                           ,[DailyProfitTarget]
                           ,[CreatedOn]
                           ,[CreatedBy]
                           ,[UpdatedOn]
                           ,[UpdatedBy])
                     VALUES
                           ({TestDataDefaults.TradePosition.TradeId}
                           ,'IronCondor'
                           ,46116.00
                           ,3187.50
                           ,-3187.50
                           ,0.05866949
                           ,4.25
                           ,0.53125
                           ,1934.55
                           ,1137.675
                           ,'2020-01-10'
                           ,'basil'
                           ,'2020-01-12'
                           ,'basil')
                ").ExecuteCommandAsync();

            var failedEvent = default(OptionTradeDailyProfitTargetUpdatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockOptionPricerEventProducer = new Mock<ITradeEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OptionTradeDailyProfitTargetUpdatedFailEvent>()))
                .Callback<OptionTradeDailyProfitTargetUpdatedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new TradeEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<TradeEventDenormalizer>());

            // when...
            var optionTradeDailyProfitTargetUpdated = new OptionTradeDailyProfitTargetUpdatedEvent
            {
                OrderId = 1200,
                TradeId = TestDataDefaults.TradeLimit.TradeId,
                TradeType = TestDataDefaults.TradeLimit.TradeType,
                DailyProfitTarget = 693.56m,
                UpdatedBy = TestDataDefaults.TradeLimit.UpdatedBy,
                UpdatedOn = TestDataDefaults.TradeLimit.UpdatedOn
            };
            await denormalizer.ExecuteAsync(optionTradeDailyProfitTargetUpdated);

            // then...
            var tradeLimit = await (_fixture.Db.Use($"select * from trade_limit where TradeId = {TestDataDefaults.TradeLimit.TradeId}")).ExecuteSingleAsync<TradeLimitReadModel>();
            tradeLimit.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(optionTradeDailyProfitTargetUpdated.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(optionTradeDailyProfitTargetUpdated.CommandId);
        }

        private ILogger<TLogger> GetLogger<TLogger>()
        {
            var mockLogger = new Mock<ILogger<TLogger>>();
            mockLogger
                .Setup(e => e.LogInformation(It.IsAny<string>()));
            mockLogger
                .Setup(e => e.LogError(It.IsAny<Exception>(), It.IsAny<string>()));
            return mockLogger.Object;
        }
    }
}
