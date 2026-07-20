using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Event.Denormalizer.UnitTests
{
    public class DatabaseFixture : IDisposable
    {
        public DatabaseFixture()
        {
            var diContainer = new Dictionary<Type, MarketDataDbContext>();
            var dbConn = new DbConnectionSettings()
                .Add("MarketDataDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=marketdatatestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            DbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, DbFactory));
            Db = DbFactory.MarketDataDb as MarketDataDbContext;
        }

        public MarketDataDbContext Db { get; }
        public DbContextFactory DbFactory { get; }

        public void Dispose()
        {
        }
    }

    public class MarketDataFeedEventDenormalizerTests : IClassFixture<DatabaseFixture>
    {

        private DatabaseFixture _fixture;

        public MarketDataFeedEventDenormalizerTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ExecuteFuturesTickDataInsertedEvent()
        {
            // given...
            await _fixture.Db.Use("delete from futures_tick_data where ContractId = 'ES20190920'").ExecuteCommandAsync();
            var completedEvent = default(FuturesTickDataInsertedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockMarketDataFeedEventProducer = new Mock<IMarketDataFeedEventProducer>();
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesTickDataInsertedCompleteEvent>()))
                .Callback<FuturesTickDataInsertedCompleteEvent>(e => completedEvent = e);
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new MarketDataFeedEventDenormalizer(_fixture.DbFactory, mockMarketDataFeedEventProducer.Object, GetLogger<MarketDataFeedEventDenormalizer>());

            var futuresTickDataInserted = new FuturesTickDataInsertedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesContract,
                TickData = TestDataDefaults.FuturesTickData,
                CreatedOn = new DateTime(2019,8,12),
                CreatedBy = "basilt", 
            };

            // when...
            await denormalizer.ExecuteAsync(futuresTickDataInserted);

            // then...
            var futuresTickData = await (_fixture.Db.Use("select ContractId, ValueDate,TickDate, TickTime, Price, Size  from futures_tick_data where ContractId = 'ES20190920'")).ExecuteSingleAsync<FuturesTickDataViewModel>();
            futuresTickData.Should().NotBeNull();
            futuresTickData.ContractId.Should().Be(TestDataDefaults.FuturesTickData.ContractId);
            futuresTickData.TickDate.Should().Be(TestDataDefaults.FuturesTickData.TickDate);
            futuresTickData.TickTime.Should().Be(TestDataDefaults.FuturesTickData.TickTime);
            futuresTickData.Price.Should().Be(TestDataDefaults.FuturesTickData.Price);
            futuresTickData.Size.Should().Be(TestDataDefaults.FuturesTickData.Size);
            futuresTickData.ValueDate.Should().Be(TestDataDefaults.FuturesTickData.ValueDate);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(futuresTickDataInserted.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(futuresTickDataInserted.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesTickDataInsertedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use("delete from futures_tick_data where ContractId = 'ES20190920'").ExecuteCommandAsync();
            var failedEvent = default(FuturesTickDataInsertedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockMarketDataFeedEventProducer = new Mock<IMarketDataFeedEventProducer>();
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesTickDataInsertedFailEvent>()))
                .Callback<FuturesTickDataInsertedFailEvent>(e => failedEvent = e);
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new MarketDataFeedEventDenormalizer(_fixture.DbFactory, mockMarketDataFeedEventProducer.Object, GetLogger<MarketDataFeedEventDenormalizer>());

            var futuresTickDataInserted = new FuturesTickDataInsertedEvent
            {
                Contract = TestDataDefaults.FuturesContract,
                TickData = TestDataDefaults.FuturesTickData,
                CreatedOn = new DateTime(2019, 8, 12),
                CreatedBy = "basilt",
            };

            // when...
            await denormalizer.ExecuteAsync(futuresTickDataInserted);

            // then...
            var futuresTickData = await (_fixture.Db.Use("select ContractId, ValueDate,TickDate, TickTime, Price, Size  from futures_tick_data where ContractId = 'ES20190920'")).ExecuteSingleAsync<FuturesTickDataViewModel>();
            futuresTickData.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(futuresTickDataInserted.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(futuresTickDataInserted.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesOptionTickDataInsertedEvent()
        {
            // given...
            var contractId = TestDataDefaults.FuturesOptionTickData.ContractId;
            await _fixture.Db.Use($"delete from futures_option_tick_data where ContractId = '{contractId}'").ExecuteCommandAsync();
            var completedEvent = default(FuturesOptionTickDataInsertedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockMarketDataFeedEventProducer = new Mock<IMarketDataFeedEventProducer>();
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionTickDataInsertedCompleteEvent>()))
                .Callback<FuturesOptionTickDataInsertedCompleteEvent>(e => completedEvent = e);
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new MarketDataFeedEventDenormalizer(_fixture.DbFactory, mockMarketDataFeedEventProducer.Object, GetLogger<MarketDataFeedEventDenormalizer>());

            var futuresOptionTickDataInserted = new FuturesOptionTickDataInsertedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesContract,
                TickData = TestDataDefaults.FuturesOptionTickData,
                CreatedOn = new DateTime(2019, 8, 12),
                CreatedBy = "basilt"
            };

            // when...
            await denormalizer.ExecuteAsync(futuresOptionTickDataInserted);

            // then...
            var futuresOptionTickData = await (_fixture.Db.Use($"select * from futures_option_tick_data where ContractId = '{contractId}'")).ExecuteSingleAsync<FuturesOptionTickDataViewModel>();
            futuresOptionTickData.Should().NotBeNull();
            futuresOptionTickData.ContractId.Should().Be(TestDataDefaults.FuturesOptionTickData.ContractId);
            futuresOptionTickData.TickDate.Should().Be(TestDataDefaults.FuturesOptionTickData.TickDate);
            futuresOptionTickData.TickTime.Should().Be(TestDataDefaults.FuturesOptionTickData.TickTime);
            futuresOptionTickData.OptionPrice.Should().Be(TestDataDefaults.FuturesOptionTickData.OptionPrice);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(futuresOptionTickDataInserted.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(futuresOptionTickDataInserted.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesOptionTickDataInsertedEventWithEmptyCommandId()
        {
            // given...
            var contractId = TestDataDefaults.FuturesOptionTickData.ContractId;
            await _fixture.Db.Use($"delete from futures_option_tick_data where ContractId = '{contractId}'").ExecuteCommandAsync();

            var failedEvent = default(FuturesOptionTickDataInsertedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockMarketDataFeedEventProducer = new Mock<IMarketDataFeedEventProducer>();
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionTickDataInsertedFailEvent>()))
                .Callback<FuturesOptionTickDataInsertedFailEvent>(e => failedEvent = e);
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new MarketDataFeedEventDenormalizer(_fixture.DbFactory, mockMarketDataFeedEventProducer.Object, GetLogger<MarketDataFeedEventDenormalizer>());

            var futuresOptionTickDataInsertedEvent = new FuturesOptionTickDataInsertedEvent
            {
                Contract = TestDataDefaults.FuturesContract,
                TickData = TestDataDefaults.FuturesOptionTickData,
                CreatedOn = new DateTime(2019, 8, 12),
                CreatedBy = "basilt"
            };

            // when...
            await denormalizer.ExecuteAsync(futuresOptionTickDataInsertedEvent);

            // then...
            var futuresOptionTickData = await (_fixture.Db.Use($"select * from futures_option_tick_data where ContractId = '{contractId}'")).ExecuteSingleAsync<FuturesOptionTickDataViewModel>();
            futuresOptionTickData.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(futuresOptionTickDataInsertedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(futuresOptionTickDataInsertedEvent.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesEodDataInsertedEvent()
        {
            // given...
            var contractId = TestDataDefaults.FuturesTickData.ContractId;
            await _fixture.Db.Use($"delete from futures_eod_data where ContractId = '{contractId}'").ExecuteCommandAsync();

            var completedEvent = default(FuturesEodDataInsertedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockMarketDataFeedEventProducer = new Mock<IMarketDataFeedEventProducer>();
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesEodDataInsertedCompleteEvent>()))
                .Callback<FuturesEodDataInsertedCompleteEvent>(e => completedEvent = e);
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new MarketDataFeedEventDenormalizer(_fixture.DbFactory, mockMarketDataFeedEventProducer.Object, GetLogger<MarketDataFeedEventDenormalizer>());
            var futuresEodDataInsertedEvent = new FuturesEodDataInsertedEvent
            {
                CommandId = Guid.NewGuid(),
                FuturesEodData = TestDataDefaults.FuturesEodData,
                CreatedOn = new DateTime(2019, 8, 12),
                CreatedBy = "basilt"
            };

            // when...
            await denormalizer.ExecuteAsync(futuresEodDataInsertedEvent);

            // then...
            var futuresEodData = await (_fixture.Db.Use($"select * from futures_eod_data where ContractId = '{contractId}'")).ExecuteSingleAsync<FuturesEodDataViewModel>();
            futuresEodData.Should().NotBeNull();
            futuresEodData.ContractId.Should().Be(TestDataDefaults.FuturesEodData.ContractId);
            futuresEodData.ValueDate.Should().Be(TestDataDefaults.FuturesEodData.ValueDate);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(futuresEodDataInsertedEvent.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(futuresEodDataInsertedEvent.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesEodDataInsertedEventWithEmptyCommandId()
        {
            // given...
            var contractId = TestDataDefaults.FuturesTickData.ContractId;
            await _fixture.Db.Use($"delete from futures_eod_data where ContractId = '{contractId}'").ExecuteCommandAsync();

            var failedEvent = default(FuturesEodDataInsertedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockMarketDataFeedEventProducer = new Mock<IMarketDataFeedEventProducer>();
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesEodDataInsertedFailEvent>()))
                .Callback<FuturesEodDataInsertedFailEvent>(e => failedEvent = e);
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new MarketDataFeedEventDenormalizer(_fixture.DbFactory, mockMarketDataFeedEventProducer.Object, GetLogger<MarketDataFeedEventDenormalizer>());
            var futuresEodDataInsertedEvent = new FuturesEodDataInsertedEvent
            {
                FuturesEodData = TestDataDefaults.FuturesEodData,
                CreatedOn = new DateTime(2019, 8, 12),
                CreatedBy = "basilt"
            };

            // when...
            await denormalizer.ExecuteAsync(futuresEodDataInsertedEvent);

            // then...
            var futuresEodData = await (_fixture.Db.Use($"select * from futures_eod_data where ContractId = '{contractId}'")).ExecuteSingleAsync<FuturesEodDataViewModel>();
            futuresEodData.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(futuresEodDataInsertedEvent.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(futuresEodDataInsertedEvent.CommandId);
        }

        [Fact]
        public async Task ExecuteTradeLiveFeedAddedEvent()
        {
            // given...
            var orderId = 1200;
            var tradeId = 1278;
            await _fixture.Db.Use($"delete from trade_live_feed where OrderId = {orderId} and TradeId = {tradeId}").ExecuteCommandAsync();

            var completedEvent = default(TradeLiveFeedAddedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockMarketDataFeedEventProducer = new Mock<IMarketDataFeedEventProducer>();
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeLiveFeedAddedCompleteEvent>()))
                .Callback<TradeLiveFeedAddedCompleteEvent>(e => completedEvent = e);
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new MarketDataFeedEventDenormalizer(_fixture.DbFactory, mockMarketDataFeedEventProducer.Object, GetLogger<MarketDataFeedEventDenormalizer>());
            
            // when...
            var tradeLiveFeedAdded = new TradeLiveFeedAddedEvent
            {
                CommandId = Guid.NewGuid(),
                OrderId = 1200,
                TradeId = 1278,
                AddedBy = "basilt",
                AddedOn = new DateTime(2019, 8, 12)
            };
            await denormalizer.ExecuteAsync(tradeLiveFeedAdded);

            // then...
            var tradeLiveFeed = await (_fixture.Db.Use($"select * from trade_live_feed where OrderId = {orderId} and TradeId = {tradeId}")).ExecuteSingleAsync<TradeLiveFeedReadModel>();
            tradeLiveFeed.Should().NotBeNull();
            tradeLiveFeed.OrderId.Should().Be(orderId);
            tradeLiveFeed.TradeId.Should().Be(tradeId);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(tradeLiveFeedAdded.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(tradeLiveFeedAdded.CommandId);
        }

        [Fact]
        public async Task ExecuteTradeLiveFeedAddedEventWithEmptyCommandId()
        {
            // given...
            var orderId = 1200;
            var tradeId = 1278;
            await _fixture.Db.Use($"delete from trade_live_feed where OrderId = {orderId} and TradeId = {tradeId}").ExecuteCommandAsync();

            var failedEvent = default(TradeLiveFeedAddedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockMarketDataFeedEventProducer = new Mock<IMarketDataFeedEventProducer>();
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeLiveFeedAddedFailEvent>()))
                .Callback<TradeLiveFeedAddedFailEvent>(e => failedEvent = e);
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new MarketDataFeedEventDenormalizer(_fixture.DbFactory, mockMarketDataFeedEventProducer.Object, GetLogger<MarketDataFeedEventDenormalizer>());

            // when...
            var tradeLiveFeedAdded = new TradeLiveFeedAddedEvent
            {
                OrderId = 1200,
                TradeId = 1278,
                AddedBy = "basilt",
                AddedOn = new DateTime(2019, 8, 12)
            };
            await denormalizer.ExecuteAsync(tradeLiveFeedAdded);

            // then...
            var tradeLiveFeed = await (_fixture.Db.Use($"select * from trade_live_feed where OrderId = {orderId} and TradeId = {tradeId}")).ExecuteSingleAsync<TradeLiveFeedReadModel>();
            tradeLiveFeed.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(tradeLiveFeedAdded.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(tradeLiveFeedAdded.CommandId);
        }


        [Fact]
        public async Task ExecuteTradeLiveFeedRemovedEvent()
        {
            // given...
            var orderId = 1200;
            var tradeId = 1278;
            await _fixture.Db.Use($"delete from trade_live_feed where OrderId = {orderId} and TradeId = {tradeId}").ExecuteCommandAsync();

            var completedEvent = default(TradeLiveFeedRemovedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockMarketDataFeedEventProducer = new Mock<IMarketDataFeedEventProducer>();
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeLiveFeedRemovedCompleteEvent>()))
                .Callback<TradeLiveFeedRemovedCompleteEvent>(e => completedEvent = e);
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new MarketDataFeedEventDenormalizer(_fixture.DbFactory, mockMarketDataFeedEventProducer.Object, GetLogger<MarketDataFeedEventDenormalizer>());

            // when...
            var tradeLiveFeedRemoved = new TradeLiveFeedRemovedEvent
            {
                CommandId = Guid.NewGuid(),
                OrderId = 1200,
                TradeId = 1278,
                RemovedBy = "basilt",
                RemovedOn = new DateTime(2019, 8, 12)
            };
            await denormalizer.ExecuteAsync(tradeLiveFeedRemoved);

            // then...
            var tradeLiveFeed = await (_fixture.Db.Use($"select * from trade_live_feed where OrderId = {orderId} and TradeId = {tradeId}")).ExecuteSingleAsync<TradeLiveFeedReadModel>();
            tradeLiveFeed.Should().NotBeNull();
            tradeLiveFeed.OrderId.Should().Be(orderId);
            tradeLiveFeed.TradeId.Should().Be(tradeId);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(tradeLiveFeedRemoved.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(tradeLiveFeedRemoved.CommandId);
        }

        [Fact]
        public async Task ExecuteTradeLiveFeedRemovedEventWithEmptyCommandId()
        {
            // given...
            var orderId = 1200;
            var tradeId = 1278;
            await _fixture.Db.Use($"delete from trade_live_feed where OrderId = {orderId} and TradeId = {tradeId}").ExecuteCommandAsync();

            var failedEvent = default(TradeLiveFeedRemovedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockMarketDataFeedEventProducer = new Mock<IMarketDataFeedEventProducer>();
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<TradeLiveFeedRemovedFailEvent>()))
                .Callback<TradeLiveFeedRemovedFailEvent>(e => failedEvent = e);
            mockMarketDataFeedEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new MarketDataFeedEventDenormalizer(_fixture.DbFactory, mockMarketDataFeedEventProducer.Object, GetLogger<MarketDataFeedEventDenormalizer>());

            // when...
            var tradeLiveFeedRemoved = new TradeLiveFeedRemovedEvent
            {
                OrderId = 1200,
                TradeId = 1278,
                RemovedBy = "basilt",
                RemovedOn = new DateTime(2019, 8, 12)
            };
            await denormalizer.ExecuteAsync(tradeLiveFeedRemoved);

            // then...
            var tradeLiveFeed = await (_fixture.Db.Use($"select * from trade_live_feed where OrderId = {orderId} and TradeId = {tradeId}")).ExecuteSingleAsync<TradeLiveFeedReadModel>();
            tradeLiveFeed.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(tradeLiveFeedRemoved.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(tradeLiveFeedRemoved.CommandId);
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
