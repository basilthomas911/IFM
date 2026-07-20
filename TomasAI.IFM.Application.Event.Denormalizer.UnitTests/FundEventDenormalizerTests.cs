using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Domain.Fund;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Event.Denormalizer.UnitTests
{
    public class FundDatabaseFixture : IDisposable
    {

        public FundDbContext Db { get; }

        public FundDatabaseFixture()
        {
            var dbConn = new DbConnectionSettings()
                .Add("FundDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=fundtestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, FundDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<FundDbContext>), new FundDbContext(dbConn, dbFactory));
            Db = dbFactory.FundDb as FundDbContext;
        }

        public void Dispose()
        {
        }
    }

    /*
     
      IAsyncEventHandler<FundCreatedEvent>,
        IAsyncEventHandler<OrderAddedToFundEvent>,
        IAsyncEventHandler<OrderRemovedFromFundEvent>,
        IAsyncEventHandler<TradeAddedToFundOrderEvent>,
        IAsyncEventHandler<TradeRemovedFromFundOrderEvent>,
        IAsyncEventHandler<FundOrderTradeStateChangedEvent>,
        IAsyncEventHandler<OpeningTradeFundTransactionCreatedEvent>,
        IAsyncEventHandler<OpeningTradeFundTransactionAdjustmentCreatedEvent>,
        IAsyncEventHandler<RealizedTradePnlFundTransactionCreatedEvent>,
        IAsyncEventHandler<RealizedTradePnlFundTransactionAdjustmentCreatedEvent>,
        IAsyncEventHandler<TradeCommissionFundTransactionCreatedEvent>,
        IAsyncEventHandler<TradeCommissionFundTransactionAdjustmentCreatedEvent>,
        IAsyncEventHandler<UnrealizedTradePnlFundTransactionCreatedEvent>,
        IAsyncEventHandler<UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent>,
        IAsyncEventHandler<FundOrderCompletedEvent> 
     */
    public class FundEventDenormalizerTests : IClassFixture<FundDatabaseFixture>
    {
        private FundDatabaseFixture _fixture;

        public FundEventDenormalizerTests(FundDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ExecuteFundCreatedEvent()
        {
            // given...
            var sql = $"delete from fund where FundId = 1001";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(FundCreatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FundCreatedCompleteEvent>()))
                .Callback<FundCreatedCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
 
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var fundCreatedEvent = new FundCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                NewFund = new FundReadModel(
                    fundId: 1001,
                    name: "Test Fund",
                    description: "Test Fund Description",
                    balance: 1003.45m,
                    createdOn: new DateTime(2019, 8, 15),
                    createdBy: "basil")
            };

            // when...
            await denormalizer.ExecuteAsync(fundCreatedEvent);
            sql = "select * from fund where FundId = 1001";
            var fund = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundReadModel>();

            // then...
            fund.Should().NotBeNull();
            fund.Name.Should().Be(fundCreatedEvent.Name);
            fund.Description.Should().Be(fundCreatedEvent.Description);
            fund.Balance.ToString("F2").Should().Be(fundCreatedEvent.InitialBalance.ToString("F2"));
            fund.CreatedOn.ToString("yyyy-MM-dd").Should().Be(fundCreatedEvent.CreatedOn.ToString("yyyy-MM-dd"));
            fund.CreatedBy.Should().Be(fundCreatedEvent.CreatedBy);
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(fundCreatedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(fundCreatedEvent.CommandId);
        }

       
        [Fact]
        public async Task ExecuteFundCreatedEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund where FundId = 1001";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(FundCreatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FundCreatedFailEvent>()))
                .Callback<FundCreatedFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());

            var domainEvent = new FundCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                NewFund = new FundReadModel(
                    fundId: 1001,
                    name: "Test Fund",
                    description: "Test Fund Description",
                    balance: 1003.45m,
                    createdOn: new DateTime(2019, 8, 15),
                    createdBy: "basil")
            };

            // when...
            await denormalizer.ExecuteAsync(domainEvent);
            sql = "select * from Fund where FundId = 1001";
            var fund = await _fixture.Db.Use(sql).ExecuteSingleAsync<FundReadModel>();

            // then...
            fund.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().NotBe(Guid.Empty);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().NotBe(Guid.Empty);
        }

         
        [Fact]
        public async Task ExecuteOrderAddedToFundEvent()
        {
            // given...
            var sql = $"delete from fund_order where FundId = 1001 and OrderId = 1002";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(OrderAddedToFundCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OrderAddedToFundCompleteEvent>()))
                .Callback<OrderAddedToFundCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var orderAddedToFundEvent = new OrderAddedToFundEvent
            {
                CommandId = Guid.NewGuid(),
                FundId = 1001,
                OrderId = 1002,
                OrderDate = new DateTime(2019,8,13),
                OrderStatus = Shared.Fund.OrderStatus.Completed,
                Reference = "Order Reference",
                CreatedOn = new DateTime(2019, 8, 15),
                CreatedBy = "basil",
                UpdatedOn = new DateTime(2019, 8, 15),
                UpdatedBy = "basil"
            };

            // when...
            await denormalizer.ExecuteAsync(orderAddedToFundEvent);
            sql = "select * from fund_order where FundId = 1001 and OrderId = 1002";
            var fundOrder = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderReadModel>();

            // then...
            fundOrder.Should().NotBeNull();
            fundOrder.FundId.Should().Be(orderAddedToFundEvent.FundId);
            fundOrder.OrderId.Should().Be(orderAddedToFundEvent.OrderId);
            fundOrder.OrderDate.ToString("yyyy-MM-dd").Should().Be(orderAddedToFundEvent.OrderDate.ToString("yyyy-MM-dd"));
            fundOrder.OrderStatus.Should().Be(Shared.Fund.OrderStatus.Completed);
            fundOrder.Reference.Should().Be(orderAddedToFundEvent.Reference);
            fundOrder.Reference.Should().Be("Order Reference");
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(orderAddedToFundEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(orderAddedToFundEvent.CommandId);

        }

       
        [Fact]
        public async Task ExecuteOrderAddedToFundEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_order where FundId = 1001 and OrderId = 1002";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(OrderAddedToFundFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OrderAddedToFundFailEvent>()))
                .Callback<OrderAddedToFundFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var orderAddedToFundEvent = new OrderAddedToFundEvent
            {
                FundId = 1001,
                OrderId = 1002,
                OrderDate = new DateTime(2019, 8, 13),
                OrderStatus = Shared.Fund.OrderStatus.Completed,
                Reference = "Order Reference",
                CreatedOn = new DateTime(2019, 8, 15),
                CreatedBy = "basil",
                UpdatedOn = new DateTime(2019, 8, 15),
                UpdatedBy = "basil"
            };

            // when...
            await denormalizer.ExecuteAsync(orderAddedToFundEvent);

            // then...
            sql = "select * from fund_order where FundId = 1001 and OrderId = 1002";
            var fundOrder = await _fixture.Db.Use(sql).ExecuteSingleAsync<FundOrderReadModel>();
            fundOrder.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().NotBe(Guid.Empty);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().NotBe(Guid.Empty);
        }

       
        [Fact]
        public async Task ExecuteOrderRemovedFromFundEvent()
        {
            // given...
            var sql = $"delete from fund_order where FundId = 1001 and OrderId = 1002";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OrderAddedToFundCompleteEvent>()));
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            await denormalizer.ExecuteAsync(new OrderAddedToFundEvent
            {
                CommandId = Guid.NewGuid(),
                FundId = 1001,
                OrderId = 1002,
                OrderDate = new DateTime(2019, 8, 13),
                OrderStatus = Shared.Fund.OrderStatus.Completed,
                Reference = "Order Reference",
                CreatedOn = new DateTime(2019, 8, 15),
                CreatedBy = "basil",
                UpdatedOn = new DateTime(2019, 8, 15),
                UpdatedBy = "basil"
            });
            sql = "select * from fund_order where FundId = 1001 and OrderId = 1002 and Deleted = 0";
            var fundOrder = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderReadModel>();
            Assert.NotNull(fundOrder);

            // when...
            var completeEvent = default(OrderRemovedFromFundCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OrderRemovedFromFundCompleteEvent>()))
                .Callback<OrderRemovedFromFundCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var orderRemovedFromFundEvent = new OrderRemovedFromFundEvent
            {
                CommandId = Guid.NewGuid(),
                FundId = 1001,
                OrderId = 1002,
                RemovedOn = new DateTime(2019, 8, 15),
                RemovedBy = "basil"
            };
            await denormalizer.ExecuteAsync(orderRemovedFromFundEvent);

            // then...
            sql = "select * from fund_order where FundId = 1001 and OrderId = 1002 and Deleted = 1";
            fundOrder = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderReadModel>();
            fundOrder.Should().NotBeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(orderRemovedFromFundEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(orderRemovedFromFundEvent.CommandId);
        }

        [Fact]
        public async Task ExecuteOrderRemovedFromFundEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_order where FundId = 1001 and OrderId = 1002";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OrderAddedToFundCompleteEvent>()));
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            await denormalizer.ExecuteAsync(new OrderAddedToFundEvent
            {
                CommandId = Guid.NewGuid(),
                FundId = 1001,
                OrderId = 1002,
                OrderDate = new DateTime(2019, 8, 13),
                OrderStatus = Shared.Fund.OrderStatus.Completed,
                Reference = "Order Reference",
                CreatedOn = new DateTime(2019, 8, 15),
                CreatedBy = "basil",
                UpdatedOn = new DateTime(2019, 8, 15),
                UpdatedBy = "basil"
            });
            sql = "select * from fund_order where FundId = 1001 and OrderId = 1002 and Deleted = 0";
            var fundOrder = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderReadModel>();
            Assert.NotNull(fundOrder);

            // when...
            var failEvent = default(OrderRemovedFromFundFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OrderRemovedFromFundFailEvent>()))
                .Callback<OrderRemovedFromFundFailEvent>(e => failEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var orderRemovedFromFundEvent = new OrderRemovedFromFundEvent
            {
                FundId = 1001,
                OrderId = 1002,
                RemovedOn = new DateTime(2019, 8, 15),
                RemovedBy = "basil"
            };
            await denormalizer.ExecuteAsync(orderRemovedFromFundEvent);

            // then...
            sql = "select * from fund_order where FundId = 1001 and OrderId = 1002 and Deleted = 1";
            fundOrder = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderReadModel>();
            fundOrder.Should().BeNull();
            failEvent.Should().NotBeNull();
            failEvent.CommandId.Should().NotBe(Guid.Empty);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().NotBe(Guid.Empty);
        }

       
        [Fact]
        public async Task ExecuteTradeAddedToFundOrderEvent()
        {
            // given...
            var sql = $"delete from fund_order_trade where OrderId = 1002 and TradeId = 1003";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(TradeAddedToFundOrderCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeAddedToFundOrderCompleteEvent>()))
                .Callback<TradeAddedToFundOrderCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var tradeAddedToFundOrderEvent = new TradeAddedToFundOrderEvent
            {
                CommandId = Guid.NewGuid(),
                FundId = 1001,
                FundOrderTrade = TestDataDefaults.FundOrderTrade
            };

            // when...
            await denormalizer.ExecuteAsync(tradeAddedToFundOrderEvent);

            // then...
            sql = "select * from fund_order_trade where OrderId = 1002 and TradeId = 1003";
            var fundOrderTrade = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderTradeReadModel>();
            fundOrderTrade.Should().NotBeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(tradeAddedToFundOrderEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(tradeAddedToFundOrderEvent.CommandId);
        }

         [Fact]
        public async Task ExecuteTradeAddedToFundOrderEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_order_trade where OrderId = 1002 and TradeId = 1003";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(TradeAddedToFundOrderFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeAddedToFundOrderFailEvent>()))
                .Callback<TradeAddedToFundOrderFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var tradeAddedToFundOrderEvent = new TradeAddedToFundOrderEvent {
                FundId = 1001,
                FundOrderTrade = TestDataDefaults.FundOrderTrade
            };

            // when...
            await denormalizer.ExecuteAsync(tradeAddedToFundOrderEvent);

            // then...
            sql = "select * from fund_order_trade where OrderId = 1002 and TradeId = 1003";
            var fundOrderTrade = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderTradeReadModel>();
            fundOrderTrade.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().NotBe(Guid.Empty);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().NotBe(Guid.Empty);
        }

       
        [Fact]
        public async Task ExecuteTradeRemovedFromFundOrderEvent()
        {
            // given...
            var sql = $"delete from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeAddedToFundOrderCompleteEvent>()));
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            await denormalizer.ExecuteAsync(new TradeAddedToFundOrderEvent
            {
                CommandId = Guid.NewGuid(),
                EventId = 1,
                FundId = 1001,
                FundOrderTrade = TestDataDefaults.FundOrderTrade
            });
            sql = $"select * from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            var fundOrderTrade = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderTradeReadModel>();
            fundOrderTrade.Should().NotBeNull();

            // when...
            var completeEvent = default(TradeRemovedFromFundOrderCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeRemovedFromFundOrderCompleteEvent>()))
                .Callback<TradeRemovedFromFundOrderCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var tradeRemovedFromFundOrderEvent = new TradeRemovedFromFundOrderEvent
            {
                CommandId = Guid.NewGuid(),
                FundId = 1001,
                OrderId = TestDataDefaults.FundOrderTrade.OrderId,
                TradeId = TestDataDefaults.FundOrderTrade.TradeId,
                RemovedOn = new DateTime(2019, 8, 15),
                RemovedBy = "basil"
            };
            await denormalizer.ExecuteAsync(tradeRemovedFromFundOrderEvent);

            // then...
            sql = $"select * from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            fundOrderTrade = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderTradeReadModel>();
            fundOrderTrade.Should().BeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(tradeRemovedFromFundOrderEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(tradeRemovedFromFundOrderEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteTradeRemovedFromFundOrderEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeAddedToFundOrderCompleteEvent>()));
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            await denormalizer.ExecuteAsync(new TradeAddedToFundOrderEvent
            {
                CommandId = Guid.NewGuid(),
                EventId = 1,
                FundId = 1001,
                FundOrderTrade = TestDataDefaults.FundOrderTrade
            });
            sql = $"select * from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            var fundOrderTrade = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderTradeReadModel>();
            fundOrderTrade.Should().NotBeNull();

            // when...
            var failedEvent = default(TradeRemovedFromFundOrderFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeRemovedFromFundOrderFailEvent>()))
                .Callback<TradeRemovedFromFundOrderFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var tradeRemovedFromFundOrderEvent = new TradeRemovedFromFundOrderEvent
            {
                 FundId = 1001,
                OrderId = TestDataDefaults.FundOrderTrade.OrderId,
                TradeId = TestDataDefaults.FundOrderTrade.TradeId,
                RemovedOn = new DateTime(2019, 8, 15),
                RemovedBy = "basil"
            };
            await denormalizer.ExecuteAsync(tradeRemovedFromFundOrderEvent);

            // then...
            sql = $"select * from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            fundOrderTrade = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderTradeReadModel>();
            fundOrderTrade.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(tradeRemovedFromFundOrderEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(tradeRemovedFromFundOrderEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteFundOrderTradeStateChangedEvent()
        {
            // given...
            var sql = $"delete from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeAddedToFundOrderCompleteEvent>()));
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            await denormalizer.ExecuteAsync(new TradeAddedToFundOrderEvent
            {
                CommandId = Guid.NewGuid(),
                EventId = 1,
                FundId = 1001,
                FundOrderTrade = TestDataDefaults.FundOrderTrade
            });
            sql = $"select * from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            var fundOrderTrade = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderTradeReadModel>();
            fundOrderTrade.Should().NotBeNull();

            // when...
            var completeEvent = default(FundOrderTradeStateChangedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FundOrderTradeStateChangedCompleteEvent>()))
                .Callback<FundOrderTradeStateChangedCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var fundOrderTradeStateChangedEvent = new FundOrderTradeStateChangedEvent
            {
                CommandId = Guid.NewGuid(),
                FundId = 1001,
                OrderId = TestDataDefaults.FundOrderTrade.OrderId,
                TradeId = TestDataDefaults.FundOrderTrade.TradeId,
                TradeState = TradeState.OrderCancelled,
                UpdatedOn = new DateTime(2019, 8, 19),
                UpdatedBy = "basilt"
            };
            await denormalizer.ExecuteAsync(fundOrderTradeStateChangedEvent);

            // then...
            sql = $"select * from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            fundOrderTrade = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderTradeReadModel>();
            fundOrderTrade.Should().NotBeNull();
            fundOrderTrade.TradeState.Should().Be(TradeState.OrderCancelled);
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(fundOrderTradeStateChangedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(fundOrderTradeStateChangedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteFundOrderTradeStateChangedEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeAddedToFundOrderCompleteEvent>()));
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            await denormalizer.ExecuteAsync(new TradeAddedToFundOrderEvent
            {
                CommandId = Guid.NewGuid(),
                EventId = 1,
                FundId = 1001,
                FundOrderTrade = TestDataDefaults.FundOrderTrade
            });
            sql = $"select * from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            var fundOrderTrade = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderTradeReadModel>();
            fundOrderTrade.Should().NotBeNull();

            // when...
            var failedEvent = default(FundOrderTradeStateChangedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FundOrderTradeStateChangedFailEvent>()))
                .Callback<FundOrderTradeStateChangedFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var fundOrderTradeStateChangedEvent = new FundOrderTradeStateChangedEvent
            {
                FundId = 1001,
                OrderId = TestDataDefaults.FundOrderTrade.OrderId,
                TradeId = TestDataDefaults.FundOrderTrade.TradeId,
                TradeState = TradeState.OrderCancelled,
                UpdatedOn = new DateTime(2019, 8, 19),
                UpdatedBy = "basilt"
            };
            await denormalizer.ExecuteAsync(fundOrderTradeStateChangedEvent);

            // then...
            sql = $"select * from fund_order_trade where OrderId = {TestDataDefaults.FundOrderTrade.OrderId} and TradeId = {TestDataDefaults.FundOrderTrade.TradeId}";
            fundOrderTrade = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundOrderTradeReadModel>();
            fundOrderTrade.Should().NotBeNull();
            fundOrderTrade.TradeState.Should().Be(TestDataDefaults.FundOrderTrade.TradeState);
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(fundOrderTradeStateChangedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(fundOrderTradeStateChangedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteOpeningTradeFundTransactionCreatedEvent()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(OpeningTradeFundTransactionCreatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OpeningTradeFundTransactionCreatedCompleteEvent>()))
                .Callback<OpeningTradeFundTransactionCreatedCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var openingTradeFundTransactionCreatedEvent = new OpeningTradeFundTransactionCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(openingTradeFundTransactionCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().NotBeNull();
            fundTransaction.TradeStatus.Should().Be(TestDataDefaults.FundTransaction.TradeStatus);
            fundTransaction.TradeId.Should().Be(TestDataDefaults.FundTransaction.TradeId);
            fundTransaction.TradeType.Should().Be(TestDataDefaults.FundTransaction.TradeType);
            fundTransaction.TransactionType.Should().Be(TestDataDefaults.FundTransaction.TransactionType);
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(openingTradeFundTransactionCreatedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(openingTradeFundTransactionCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteOpeningTradeFundTransactionCreatedEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(OpeningTradeFundTransactionCreatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OpeningTradeFundTransactionCreatedFailEvent>()))
                .Callback<OpeningTradeFundTransactionCreatedFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var openingTradeFundTransactionCreatedEvent = new OpeningTradeFundTransactionCreatedEvent
            {
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(openingTradeFundTransactionCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(openingTradeFundTransactionCreatedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(openingTradeFundTransactionCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteOpeningTradeFundTransactionAdjustmentCreatedEvent()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent>()))
                .Callback<OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var openingTradeFundTransactionAdjustmentCreatedEvent = new OpeningTradeFundTransactionAdjustmentCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(openingTradeFundTransactionAdjustmentCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().NotBeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(openingTradeFundTransactionAdjustmentCreatedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(openingTradeFundTransactionAdjustmentCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteOpeningTradeFundTransactionAdjustmentCreatedEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(OpeningTradeFundTransactionAdjustmentCreatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<OpeningTradeFundTransactionAdjustmentCreatedFailEvent>()))
                .Callback<OpeningTradeFundTransactionAdjustmentCreatedFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var openingTradeFundTransactionAdjustmentCreatedEvent = new OpeningTradeFundTransactionAdjustmentCreatedEvent
            {
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(openingTradeFundTransactionAdjustmentCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(openingTradeFundTransactionAdjustmentCreatedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(openingTradeFundTransactionAdjustmentCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteRealizedTradePnlFundTransactionCreatedEvent()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(RealizedTradePnlFundTransactionCreatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<RealizedTradePnlFundTransactionCreatedCompleteEvent>()))
                .Callback<RealizedTradePnlFundTransactionCreatedCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var realizedTradePnlFundTransactionCreatedEvent = new RealizedTradePnlFundTransactionCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(realizedTradePnlFundTransactionCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().NotBeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(realizedTradePnlFundTransactionCreatedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(realizedTradePnlFundTransactionCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteRealizedTradePnlFundTransactionCreatedEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(RealizedTradePnlFundTransactionCreatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<RealizedTradePnlFundTransactionCreatedFailEvent>()))
                .Callback<RealizedTradePnlFundTransactionCreatedFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var realizedTradePnlFundTransactionCreatedEvent = new RealizedTradePnlFundTransactionCreatedEvent
            {
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(realizedTradePnlFundTransactionCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(realizedTradePnlFundTransactionCreatedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(realizedTradePnlFundTransactionCreatedEvent.CommandId);
        }

      
        [Fact]
        public async Task ExecuteRealizedTradePnlFundTransactionAdjustmentCreatedEvent()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(RealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<RealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent>()))
                .Callback<RealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var realizedTradePnlFundTransactionAdjustmentCreatedEvent = new RealizedTradePnlFundTransactionAdjustmentCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(realizedTradePnlFundTransactionAdjustmentCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().NotBeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(realizedTradePnlFundTransactionAdjustmentCreatedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(realizedTradePnlFundTransactionAdjustmentCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteRealizedTradePnlFundTransactionAdjustmentCreatedEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(RealizedTradePnlFundTransactionAdjustmentCreatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<RealizedTradePnlFundTransactionAdjustmentCreatedFailEvent>()))
                .Callback<RealizedTradePnlFundTransactionAdjustmentCreatedFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var realizedTradePnlFundTransactionAdjustmentCreatedEvent = new RealizedTradePnlFundTransactionAdjustmentCreatedEvent
            {
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(realizedTradePnlFundTransactionAdjustmentCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(realizedTradePnlFundTransactionAdjustmentCreatedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(realizedTradePnlFundTransactionAdjustmentCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteTradeCommissionFundTransactionCreatedEvent()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(TradeCommissionFundTransactionCreatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeCommissionFundTransactionCreatedCompleteEvent>()))
                .Callback<TradeCommissionFundTransactionCreatedCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var tradeCommissionFundTransactionCreatedEvent = new TradeCommissionFundTransactionCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(tradeCommissionFundTransactionCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().NotBeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(tradeCommissionFundTransactionCreatedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(tradeCommissionFundTransactionCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteTradeCommissionFundTransactionCreatedEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(TradeCommissionFundTransactionCreatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeCommissionFundTransactionCreatedFailEvent>()))
                .Callback<TradeCommissionFundTransactionCreatedFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var tradeCommissionFundTransactionCreatedEvent = new TradeCommissionFundTransactionCreatedEvent
            {
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(tradeCommissionFundTransactionCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(tradeCommissionFundTransactionCreatedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(tradeCommissionFundTransactionCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteTradeCommissionFundTransactionAdjustmentCreatedEvent()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(TradeCommissionFundTransactionAdjustmentCreatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeCommissionFundTransactionAdjustmentCreatedCompleteEvent>()))
                .Callback<TradeCommissionFundTransactionAdjustmentCreatedCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var tradeCommissionFundTransactionAdjustmentCreatedEvent = new TradeCommissionFundTransactionAdjustmentCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(tradeCommissionFundTransactionAdjustmentCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().NotBeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(tradeCommissionFundTransactionAdjustmentCreatedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(tradeCommissionFundTransactionAdjustmentCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteTradeCommissionFundTransactionAdjustmentCreatedEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(TradeCommissionFundTransactionAdjustmentCreatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeCommissionFundTransactionAdjustmentCreatedFailEvent>()))
                .Callback<TradeCommissionFundTransactionAdjustmentCreatedFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var tradeCommissionFundTransactionAdjustmentCreatedEvent = new TradeCommissionFundTransactionAdjustmentCreatedEvent
            {
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(tradeCommissionFundTransactionAdjustmentCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(tradeCommissionFundTransactionAdjustmentCreatedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(tradeCommissionFundTransactionAdjustmentCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteUnrealizedTradePnlFundTransactionCreatedEvent()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(UnrealizedTradePnlFundTransactionCreatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<UnrealizedTradePnlFundTransactionCreatedCompleteEvent>()))
                .Callback<UnrealizedTradePnlFundTransactionCreatedCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var unrealizedTradePnlFundTransactionCreatedEvent = new UnrealizedTradePnlFundTransactionCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(unrealizedTradePnlFundTransactionCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().NotBeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(unrealizedTradePnlFundTransactionCreatedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(unrealizedTradePnlFundTransactionCreatedEvent.CommandId);
        }

       
        [Fact]
        public async Task ExecuteUnrealizedTradePnlFundTransactionCreatedEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(UnrealizedTradePnlFundTransactionCreatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<UnrealizedTradePnlFundTransactionCreatedFailEvent>()))
                .Callback<UnrealizedTradePnlFundTransactionCreatedFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var unrealizedTradePnlFundTransactionCreatedEvent = new UnrealizedTradePnlFundTransactionCreatedEvent
            {
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(unrealizedTradePnlFundTransactionCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(unrealizedTradePnlFundTransactionCreatedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(unrealizedTradePnlFundTransactionCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteUnrealizedTradePnlFundTransactionAdjustmentCreatedEvent()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var completeEvent = default(UnrealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<UnrealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent>()))
                .Callback<UnrealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent>(e => completeEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var unrealizedTradePnlFundTransactionAdjustmentCreatedEvent = new UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent
            {
                CommandId = Guid.NewGuid(),
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(unrealizedTradePnlFundTransactionAdjustmentCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().NotBeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(unrealizedTradePnlFundTransactionAdjustmentCreatedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(unrealizedTradePnlFundTransactionAdjustmentCreatedEvent.CommandId);
        }

        
        [Fact]
        public async Task ExecuteUnrealizedTradePnlFundTransactionAdjustmentCreatedEventWithEmptyCommandId()
        {
            // given...
            var sql = $"delete from fund_transaction";
            await _fixture.Db.Use(sql).ExecuteCommandAsync();
            var failedEvent = default(UnrealizedTradePnlFundTransactionAdjustmentCreatedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockFundEventProducer = new Mock<IFundEventProducer>();
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<UnrealizedTradePnlFundTransactionAdjustmentCreatedFailEvent>()))
                .Callback<UnrealizedTradePnlFundTransactionAdjustmentCreatedFailEvent>(e => failedEvent = e);
            mockFundEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new FundEventDenormalizer(_fixture.Db, mockFundEventProducer.Object, GetLogger<FundEventDenormalizer>());
            var unrealizedTradePnlFundTransactionAdjustmentCreatedEvent = new UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent
            {
                CreatedBy = "basil",
                FundTransaction = TestDataDefaults.FundTransaction
            };

            // when...
            await denormalizer.ExecuteAsync(unrealizedTradePnlFundTransactionAdjustmentCreatedEvent);

            // then...
            sql = $"select top 1 * from fund_transaction";
            var fundTransaction = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FundTransactionReadModel>();
            fundTransaction.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(unrealizedTradePnlFundTransactionAdjustmentCreatedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(unrealizedTradePnlFundTransactionAdjustmentCreatedEvent.CommandId);
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
