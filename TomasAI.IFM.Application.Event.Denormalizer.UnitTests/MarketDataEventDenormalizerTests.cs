using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Storage;


namespace TomasAI.IFM.Application.Event.Denormalizer.UnitTests
{
    public class MarketDataDatabaseFixture : IDisposable
    {
        public MarketDataDatabaseFixture()
        {
             var dbConn = new DbConnectionSettings()
                .Add("MarketDataDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=marketdatatestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, MarketDataDbContext>();
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

    public class MarketDataEventDenormalizerTests : IClassFixture<MarketDataDatabaseFixture>
    {
        private MarketDataDatabaseFixture _fixture;

        public MarketDataEventDenormalizerTests(MarketDataDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        
        [Fact]
        public async Task ExecuteFuturesContractAddedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_contract").ExecuteCommandAsync();
            var completeEvent = default(FuturesContractAddedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesContractAddedCompleteEvent>()))
                .Callback<FuturesContractAddedCompleteEvent>(e => completeEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var futuresContractAdded = new FuturesContractAddedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesContract
            };

            // when...
            await denormalizer.ExecuteAsync(futuresContractAdded);

            // then...
            var sql = "select * from futures_contract";
            var futuresContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesContractViewModel>();
            futuresContract.Should().NotBeNull();
            futuresContract.ContractId.Should().Be(TestDataDefaults.FuturesContract.ContractId);
            futuresContract.Description.Should().Be(TestDataDefaults.FuturesContract.Description);
            futuresContract.Symbol.Should().Be(TestDataDefaults.FuturesContract.Symbol);
            futuresContract.LocalSymbol.Should().Be(TestDataDefaults.FuturesContract.LocalSymbol);
            futuresContract.SecurityType.Should().Be(TestDataDefaults.FuturesContract.SecurityType, futuresContract.SecurityType);
            futuresContract.Currency.Should().Be(TestDataDefaults.FuturesContract.Currency);
            futuresContract.Exchange.Should().Be(TestDataDefaults.FuturesContract.Exchange);
            futuresContract.Multiplier.Should().Be(TestDataDefaults.FuturesContract.Multiplier);
            futuresContract.LastTradeDate.Year.Should().Be(TestDataDefaults.FuturesContract.LastTradeDate.Year);
            futuresContract.LastTradeDate.Month.Should().Be(TestDataDefaults.FuturesContract.LastTradeDate.Month);
            futuresContract.LastTradeDate.Day.Should().Be(TestDataDefaults.FuturesContract.LastTradeDate.Day);
            futuresContract.CurrentlyTraded.Should().Be(TestDataDefaults.FuturesContract.CurrentlyTraded);
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(futuresContractAdded.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(futuresContractAdded.CommandId);
        }


        [Fact]
        public async Task ExecuteFuturesContractAddedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_contract").ExecuteCommandAsync();
            var failedEvent = default(FuturesContractAddedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesContractAddedFailEvent>()))
                .Callback<FuturesContractAddedFailEvent>(e => failedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var futuresContractAdded = new FuturesContractAddedEvent
            {
                Contract = TestDataDefaults.FuturesContract
            };

            // when...
            await denormalizer.ExecuteAsync(futuresContractAdded);

            // then...
            var sql = "select * from futures_contract";
            var futuresContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesContractViewModel>();
            futuresContract.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(futuresContractAdded.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(futuresContractAdded.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesContractRemovedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_contract").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesContractAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresContractAdded = new FuturesContractAddedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesContract
            };
            await denormalizer.ExecuteAsync(futuresContractAdded);

            var sql = "select * from futures_contract";
            var futuresContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesContractViewModel>();
            Assert.NotNull(futuresContract);

            var completeEvent = default(FuturesContractRemovedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesContractRemovedCompleteEvent>()))
                .Callback<FuturesContractRemovedCompleteEvent>(e => completeEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresContractRemoved = new FuturesContractRemovedEvent
            {
                CommandId = Guid.NewGuid(),
                ContractId = TestDataDefaults.FuturesContract.ContractId
            };

            // when...
            await denormalizer.ExecuteAsync(futuresContractRemoved);

            // then...
            sql = $"select * from futures_contract where ContractId = '{TestDataDefaults.FuturesContract.ContractId}'";
            futuresContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesContractViewModel>();
            futuresContract.Should().BeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(futuresContractRemoved.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(futuresContractRemoved.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesContractRemovedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_contract").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesContractAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresContractAdded = new FuturesContractAddedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesContract
            };
            await denormalizer.ExecuteAsync(futuresContractAdded);

            var sql = "select * from futures_contract";
            var futuresContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesContractViewModel>();
            futuresContract.Should().NotBeNull();

            var failedEvent = default(FuturesContractRemovedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesContractRemovedFailEvent>()))
                .Callback<FuturesContractRemovedFailEvent>(e => failedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);

            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresContractRemoved = new FuturesContractRemovedEvent
            {
                ContractId = TestDataDefaults.FuturesContract.ContractId
            };

            // when...
            await denormalizer.ExecuteAsync(futuresContractRemoved);

            // then...
            sql = $"select * from futures_contract where ContractId = '{TestDataDefaults.FuturesContract.ContractId}'";
            futuresContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesContractViewModel>();
            futuresContract.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(futuresContractRemoved.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(futuresContractRemoved.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesContractChangedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_contract").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesContractAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresContractAdded = new FuturesContractAddedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesContract
            };
            await denormalizer.ExecuteAsync(futuresContractAdded);

            var sql = "select * from futures_contract";
            var futuresContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesContractViewModel>();
            Assert.NotNull(futuresContract);

            var completeEvent = default(FuturesContractChangedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesContractChangedCompleteEvent>()))
                .Callback<FuturesContractChangedCompleteEvent>(e => completeEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var futuresContractChanged = new FuturesContractChangedEvent
            {
                CommandId = Guid.NewGuid(),
                OriginalContractId = TestDataDefaults.FuturesContract.ContractId,
                Contract = new FuturesContractViewModel (
                    contractId: TestDataDefaults.FuturesContract.ContractId,
                    description: "The Rain In Spain",
                    symbol: "ES",
                    localSymbol: "ESU9",
                    securityType: "FUT",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    lastTradeDate: new DateTime(2019, 9, 20),
                    currentlyTraded: true
                )
            };

            // when...
            await denormalizer.ExecuteAsync(futuresContractChanged);

            // then...
            sql = $"select * from futures_contract where ContractId = '{TestDataDefaults.FuturesContract.ContractId}'";
            futuresContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesContractViewModel>();
            futuresContract.Should().NotBeNull();
            futuresContract.Description.Should().Be("The Rain In Spain");
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(futuresContractChanged.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(futuresContractChanged.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesContractChangedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_contract").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesContractAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresContractAdded = new FuturesContractAddedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesContract
            };
            await denormalizer.ExecuteAsync(futuresContractAdded);

            var sql = "select * from futures_contract";
            var futuresContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesContractViewModel>();
            Assert.NotNull(futuresContract);

            var failedEvent = default(FuturesContractChangedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesContractChangedFailEvent>()))
                .Callback<FuturesContractChangedFailEvent>(e => failedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var futuresContractChanged = new FuturesContractChangedEvent
            {
                OriginalContractId = TestDataDefaults.FuturesContract.ContractId,
                Contract = new FuturesContractViewModel(
                    contractId: TestDataDefaults.FuturesContract.ContractId,
                    description: "The Rain In Spain",
                    symbol: "ES",
                    localSymbol: "ESU9",
                    securityType: "FUT",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    lastTradeDate: new DateTime(2019, 9, 20),
                    currentlyTraded: true
                )
            };

            // when...
            await denormalizer.ExecuteAsync(futuresContractChanged);

            // then...
            sql = $"select * from futures_contract where ContractId = '{TestDataDefaults.FuturesContract.ContractId}'";
            futuresContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesContractViewModel>();
            futuresContract.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(futuresContractChanged.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(futuresContractChanged.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesOptionContractAddedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_option_contract").ExecuteCommandAsync();
            var completeEvent = default(FuturesOptionContractAddedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionContractAddedCompleteEvent>()))
                .Callback<FuturesOptionContractAddedCompleteEvent>(e => completeEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var futuresOptionContractAdded = new FuturesOptionContractAddedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesOptionContract
            };

            // when...
            await denormalizer.ExecuteAsync(futuresOptionContractAdded);

            // then...
            var sql = "select * from futures_option_contract";
            var futuresOptionContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesOptionContractReadModel>();
            futuresOptionContract.Should().NotBeNull();
            futuresOptionContract.ContractId.Should().Be(TestDataDefaults.FuturesOptionContract.ContractId);
            futuresOptionContract.Description.Should().Be(TestDataDefaults.FuturesOptionContract.Description);
            futuresOptionContract.Symbol.Should().Be(TestDataDefaults.FuturesOptionContract.Symbol);
            futuresOptionContract.LocalSymbol.Should().Be(TestDataDefaults.FuturesOptionContract.LocalSymbol);
            futuresOptionContract.SecurityType.Should().Be(TestDataDefaults.FuturesOptionContract.SecurityType);
            futuresOptionContract.Currency.Should().Be(TestDataDefaults.FuturesOptionContract.Currency);
            futuresOptionContract.Exchange.Should().Be(TestDataDefaults.FuturesOptionContract.Exchange);
            futuresOptionContract.Multiplier.Should().Be(TestDataDefaults.FuturesOptionContract.Multiplier);
            futuresOptionContract.ContractMonth.Year.Should().Be(TestDataDefaults.FuturesOptionContract.ContractMonth.Year);
            futuresOptionContract.ContractMonth.Month.Should().Be(TestDataDefaults.FuturesOptionContract.ContractMonth.Month);
            futuresOptionContract.ContractMonth.Day.Should().Be(TestDataDefaults.FuturesOptionContract.ContractMonth.Day);
            futuresOptionContract.OptionType.Should().Be(TestDataDefaults.FuturesOptionContract.OptionType );
            futuresOptionContract.StrikePrice.ToString("F2").Should().Be(TestDataDefaults.FuturesOptionContract.StrikePrice.ToString("F2"));
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(futuresOptionContractAdded.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(futuresOptionContractAdded.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesOptionContractAddedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_option_contract").ExecuteCommandAsync();
            var failedEvent = default(FuturesOptionContractAddedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionContractAddedFailEvent>()))
                .Callback<FuturesOptionContractAddedFailEvent>(e => failedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var futuresOptionContractAdded = new FuturesOptionContractAddedEvent
            {
                Contract = TestDataDefaults.FuturesOptionContract
            };

            // when...
            await denormalizer.ExecuteAsync(futuresOptionContractAdded);

            // then...
            var sql = "select * from futures_option_contract";
            var futuresOptionContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesOptionContractReadModel>();
            futuresOptionContract.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(futuresOptionContractAdded.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(futuresOptionContractAdded.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesOptionContractRemovedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionContractAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresContractAdded = new FuturesOptionContractAddedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesOptionContract
            };
            await denormalizer.ExecuteAsync(futuresContractAdded);

            var sql = $"select * from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'";
            var futuresOptionContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesOptionContractReadModel>();
            futuresOptionContract.Should().NotBeNull();

            var completeEvent = default(FuturesOptionContractRemovedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionContractRemovedCompleteEvent>()))
                .Callback<FuturesOptionContractRemovedCompleteEvent>(e => completeEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var futuresOptionContractRemoved = new FuturesOptionContractRemovedEvent
            {
                CommandId = Guid.NewGuid(),
                ContractId = TestDataDefaults.FuturesOptionContract.ContractId
            };

            // when...
            await denormalizer.ExecuteAsync(futuresOptionContractRemoved);

            // then...
            sql = $"select * from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'";
            futuresOptionContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesOptionContractReadModel>();
            futuresOptionContract.Should().BeNull();
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(futuresOptionContractRemoved.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(futuresOptionContractRemoved.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesOptionContractRemovedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionContractAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresContractAdded = new FuturesOptionContractAddedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesOptionContract
            };
            await denormalizer.ExecuteAsync(futuresContractAdded);

            var sql = $"select * from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'";
            var futuresOptionContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesOptionContractReadModel>();
            futuresOptionContract.Should().NotBeNull();

            var failedEvent = default(FuturesOptionContractRemovedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionContractRemovedFailEvent>()))
                .Callback<FuturesOptionContractRemovedFailEvent>(e => failedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);

            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var futuresOptionContractRemoved = new FuturesOptionContractRemovedEvent
            {
                ContractId = TestDataDefaults.FuturesOptionContract.ContractId
            };

            // when...
            await denormalizer.ExecuteAsync(futuresOptionContractRemoved);

            // then...
            sql = $"select * from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'";
            futuresOptionContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesOptionContractReadModel>();
            futuresOptionContract.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(futuresOptionContractRemoved.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(futuresOptionContractRemoved.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesOptionContractChangedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionContractAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresContractAdded = new FuturesOptionContractAddedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesOptionContract
            };
            await denormalizer.ExecuteAsync(futuresContractAdded);

            var sql = $"select * from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'";
            var futuresOptionContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesOptionContractReadModel>();
            futuresOptionContract.Should().NotBeNull();

            var completeEvent = default(FuturesOptionContractChangedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionContractChangedCompleteEvent>()))
                .Callback<FuturesOptionContractChangedCompleteEvent>(e => completeEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var futuresOptionContractChanged = new FuturesOptionContractChangedEvent
            {
                CommandId = Guid.NewGuid(),
                OriginalContractId = TestDataDefaults.FuturesOptionContract.ContractId,
                Contract = new FuturesOptionContractReadModel (
                    contractId: TestDataDefaults.FuturesOptionContract.ContractId,
                    description: "The futures option was changed",
                    symbol: "ES",
                    localSymbol: "EW3Q9",
                    securityType: "FOP",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    contractMonth: new DateTime(2019, 8, 16),
                    strikePrice: 2790,
                    optionType: "PUT"
                )
            };

            // when...
            await denormalizer.ExecuteAsync(futuresOptionContractChanged);

            // then...
            sql = $"select * from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'";
            futuresOptionContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesOptionContractReadModel>();
            futuresOptionContract.Should().NotBeNull();
            futuresOptionContract.Description.Should().Be("The futures option was changed");
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(futuresOptionContractChanged.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(futuresOptionContractChanged.CommandId);
        }

        [Fact]
        public async Task ExecuteFuturesOptionContractChangedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionContractAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresContractAdded = new FuturesOptionContractAddedEvent
            {
                CommandId = Guid.NewGuid(),
                Contract = TestDataDefaults.FuturesOptionContract
            };
            await denormalizer.ExecuteAsync(futuresContractAdded);

            var sql = $"select * from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'";
            var futuresOptionContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesOptionContractReadModel>();
            futuresOptionContract.Should().NotBeNull();

            var failedEvent = default(FuturesOptionContractChangedFailEvent);
            var denormalizerFailedEvent = default(DenormalizerExceptionEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<FuturesOptionContractChangedFailEvent>()))
                .Callback<FuturesOptionContractChangedFailEvent>(e => failedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerFailedEvent = e);
            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var futuresOptionContractChanged = new FuturesOptionContractChangedEvent
            {
                OriginalContractId = TestDataDefaults.FuturesOptionContract.ContractId,
                Contract = new FuturesOptionContractReadModel(
                    contractId: TestDataDefaults.FuturesOptionContract.ContractId,
                    description: "The futures option was changed",
                    symbol: "ES",
                    localSymbol: "EW3Q9",
                    securityType: "FOP",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    contractMonth: new DateTime(2019, 8, 16),
                    strikePrice: 2790,
                    optionType: "PUT"
                )
            };

            // when...
            await denormalizer.ExecuteAsync(futuresOptionContractChanged);

            // then...
            sql = $"select * from futures_option_contract where ContractId = '{TestDataDefaults.FuturesOptionContract.ContractId}'";
            futuresOptionContract = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<FuturesOptionContractReadModel>();
            futuresOptionContract.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(futuresOptionContractChanged.CommandId);
            denormalizerFailedEvent.Should().NotBeNull();
            denormalizerFailedEvent.CommandId.Should().Be(futuresOptionContractChanged.CommandId);
        }

        [Fact]
        public async Task ExecuteYieldCurveRateAddedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}'").ExecuteCommandAsync();
            

            // when...
            var completeEvent = default(YieldCurveRateAddedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRateAddedCompleteEvent>()))
                .Callback<YieldCurveRateAddedCompleteEvent>(e => completeEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var yieldCurveRateAdded = new YieldCurveRateAddedEvent
            {
                CommandId = Guid.NewGuid(),
                YieldCurveRate = TestDataDefaults.YieldCurveRate
            };

            // when...
            await denormalizer.ExecuteAsync(yieldCurveRateAdded);

            // then...
            var sql = $"select * from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy - MM - dd")}'";
            var yieldCurveRate = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<YieldCurveRateReadModel>();
            yieldCurveRate.Should().NotBeNull();
            yieldCurveRate.OneMonth.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.OneMonth.ToString("F2"));
            yieldCurveRate.TwoMonth.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.TwoMonth.ToString("F2"));
            yieldCurveRate.ThreeMonth.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.ThreeMonth.ToString("F2"));
            yieldCurveRate.SixMonth.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.SixMonth.ToString("F2"));
            yieldCurveRate.OneYear.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.OneYear.ToString("F2"));
            yieldCurveRate.TwoYear.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.TwoYear.ToString("F2"));
            yieldCurveRate.ThreeYear.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.ThreeYear.ToString("F2"));
            yieldCurveRate.FiveYear.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.FiveYear.ToString("F2"));
            yieldCurveRate.SevenYear.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.SevenYear.ToString("F2"));
            yieldCurveRate.TenYear.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.TenYear.ToString("F2"));
            yieldCurveRate.TwentyYear.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.TwentyYear.ToString("F2"));
            yieldCurveRate.ThirtyYear.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRate.ThirtyYear.ToString("F2"));
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(yieldCurveRateAdded.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(yieldCurveRateAdded.CommandId);
        }

        [Fact]
        public async Task ExecuteYieldCurveRateAddedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}'").ExecuteCommandAsync();
            var failedEvent = default(YieldCurveRateAddedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRateAddedFailEvent>()))
                .Callback<YieldCurveRateAddedFailEvent>(e => failedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var yieldCurveRateAdded = new YieldCurveRateAddedEvent
            {
                YieldCurveRate = TestDataDefaults.YieldCurveRate
            };

            // when...
            await denormalizer.ExecuteAsync(yieldCurveRateAdded);
           
            // then...
            var sql = $"select * from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy - MM - dd")}'";
            var yieldCurveRate = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<YieldCurveRateReadModel>();
            yieldCurveRate.Should().BeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(yieldCurveRateAdded.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(yieldCurveRateAdded.CommandId);
        }

        [Fact]
        public async Task ExecuteYieldCurveRateRemovedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}'").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRateAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var yieldCurveRateAdded = new YieldCurveRateAddedEvent
            {
                CommandId = Guid.NewGuid(),
                YieldCurveRate = TestDataDefaults.YieldCurveRate
            };
            await denormalizer.ExecuteAsync(yieldCurveRateAdded);

            var sql = $"select * from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy - MM - dd")}'";
            var yieldCurveRate = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<YieldCurveRateReadModel>();
            yieldCurveRate.Should().NotBeNull();

            var completedEvent = default(YieldCurveRateRemovedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRateRemovedCompleteEvent>()))
                .Callback<YieldCurveRateRemovedCompleteEvent>(e => completedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
 
             // when...
             var yieldCurveRateRemoved = new YieldCurveRateRemovedEvent {
                 CommandId = Guid.NewGuid(),
                 ValueDate = TestDataDefaults.YieldCurveRate.ValueDate
             };
            await denormalizer.ExecuteAsync(yieldCurveRateRemoved);

            // then...
            sql = $"select * from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy - MM - dd")}'";
            yieldCurveRate = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<YieldCurveRateReadModel>();
            yieldCurveRate.Should().BeNull();
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(yieldCurveRateRemoved.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(yieldCurveRateRemoved.CommandId);
        }

        [Fact]
        public async Task ExecuteYieldCurveRateRemovedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}'").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRateAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var yieldCurveRateAdded = new YieldCurveRateAddedEvent
            {
                CommandId = Guid.NewGuid(),
                YieldCurveRate = TestDataDefaults.YieldCurveRate
            };
            await denormalizer.ExecuteAsync(yieldCurveRateAdded);

            var sql = $"select * from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy - MM - dd")}'";
            var yieldCurveRate = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<YieldCurveRateReadModel>();
            yieldCurveRate.Should().NotBeNull();

            var failedEvent = default(YieldCurveRateRemovedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRateRemovedFailEvent>()))
                .Callback<YieldCurveRateRemovedFailEvent>(e => failedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            // when...
            var yieldCurveRateRemoved = new YieldCurveRateRemovedEvent
            {
                ValueDate = TestDataDefaults.YieldCurveRate.ValueDate
            };
            await denormalizer.ExecuteAsync(yieldCurveRateRemoved);

            // then...
            sql = $"select * from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy - MM - dd")}'";
            yieldCurveRate = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<YieldCurveRateReadModel>();
            yieldCurveRate.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(yieldCurveRateRemoved.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(yieldCurveRateRemoved.CommandId);
        }

        [Fact]
        public async Task ExecuteYieldCurveRateChangedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}'").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRateAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var yieldCurveRateAdded = new YieldCurveRateAddedEvent
            {
                CommandId = Guid.NewGuid(),
                YieldCurveRate = TestDataDefaults.YieldCurveRate
            };
            await denormalizer.ExecuteAsync(yieldCurveRateAdded);

            var sql = $"select * from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy - MM - dd")}'";
            var yieldCurveRate = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<YieldCurveRateReadModel>();
            yieldCurveRate.Should().NotBeNull();

            var completeEvent = default(YieldCurveRateChangedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRateChangedCompleteEvent>()))
                .Callback<YieldCurveRateChangedCompleteEvent>(e => completeEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            // when...
            var yieldCurveRateChanged = new YieldCurveRateChangedEvent
            {
                CommandId = Guid.NewGuid(),
                YieldCurveRate = new YieldCurveRateReadModel(
                    valueDate: TestDataDefaults.YieldCurveRate.ValueDate,
                    oneMonth: 2.65,
                    twoMonth: 2.34,
                    threeMonth: 2.66,
                    sixMonth: 2.77,
                    oneYear: 2.78,
                    twoYear: 2.88,
                    threeYear: 2.99,
                    fiveYear: 3.11,
                    sevenYear: 3.22,
                    tenYear: 3.33,
                    twentyYear: 3.44,
                    thirtyYear: 3.55
                )
            };
            await denormalizer.ExecuteAsync(yieldCurveRateChanged);

            // then...
            sql = $"select * from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy - MM - dd")}'";
            yieldCurveRate = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<YieldCurveRateReadModel>();
            yieldCurveRate.Should().NotBeNull();
            yieldCurveRate.OneMonth.ToString("F2").Should().Be("2.65");
            yieldCurveRate.TwoMonth.ToString("F2").Should().Be("2.34");
            completeEvent.Should().NotBeNull();
            completeEvent.CommandId.Should().Be(yieldCurveRateChanged.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(yieldCurveRateChanged.CommandId);
        }

        [Fact]
        public async Task ExecuteYieldCurveRateChangedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}'").ExecuteCommandAsync();
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRateAddedCompleteEvent>()));
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var yieldCurveRateAdded = new YieldCurveRateAddedEvent
            {
                CommandId = Guid.NewGuid(),
                YieldCurveRate = TestDataDefaults.YieldCurveRate
            };
            await denormalizer.ExecuteAsync(yieldCurveRateAdded);

            var sql = $"select * from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy - MM - dd")}'";
            var yieldCurveRate = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<YieldCurveRateReadModel>();
            yieldCurveRate.Should().NotBeNull();

            var failedEvent = default(YieldCurveRateChangedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRateChangedFailEvent>()))
                .Callback<YieldCurveRateChangedFailEvent>(e => failedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            // when...
            var yieldCurveRateChanged = new YieldCurveRateChangedEvent
            {
                YieldCurveRate = new YieldCurveRateReadModel(
                    valueDate: TestDataDefaults.YieldCurveRate.ValueDate,
                    oneMonth: 2.65,
                    twoMonth: 2.34,
                    threeMonth: 2.66,
                    sixMonth: 2.77,
                    oneYear: 2.78,
                    twoYear: 2.88,
                    threeYear: 2.99,
                    fiveYear: 3.11,
                    sevenYear: 3.22,
                    tenYear: 3.33,
                    twentyYear: 3.44,
                    thirtyYear: 3.55
                )
            };
            await denormalizer.ExecuteAsync(yieldCurveRateChanged);

            // then...
            sql = $"select * from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy - MM - dd")}'";
            yieldCurveRate = await (_fixture.Db.Use(sql)).ExecuteSingleAsync<YieldCurveRateReadModel>();
            yieldCurveRate.Should().NotBeNull();
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(yieldCurveRateChanged.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(yieldCurveRateChanged.CommandId);
        }

        [Fact]
        public async Task ExecuteYieldCurveRatesImportedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}'").ExecuteCommandAsync();
            var completedEvent = default(YieldCurveRatesImportedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRatesImportedCompleteEvent>()))
                .Callback<YieldCurveRatesImportedCompleteEvent>(e => completedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());
            var yieldCurveRatesImported = new YieldCurveRatesImportedEvent
            {
                CommandId = Guid.NewGuid(),
                YieldCurveRates = TestDataDefaults.YieldCurveRates
            };

            // when...
            await denormalizer.ExecuteAsync(yieldCurveRatesImported);

            // then...
            var sql = $"select * from yield_curve_rates where ValueDate in('{TestDataDefaults.YieldCurveRates[0].ValueDate.ToString("yyyy - MM - dd")}','{TestDataDefaults.YieldCurveRates[1].ValueDate.ToString("yyyy - MM - dd")}')";
            var yieldCurveRates = await (_fixture.Db.Use(sql)).ExecuteQueryAsync<YieldCurveRateReadModel>();
            yieldCurveRates.Should().NotBeNull();
            yieldCurveRates.Count.Should().Equals(2);
            yieldCurveRates[0].OneMonth.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRates[0].OneMonth.ToString("F2"));
            yieldCurveRates[1].TwoMonth.ToString("F2").Should().Be(TestDataDefaults.YieldCurveRates[1].TwoMonth.ToString("F2"));
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(yieldCurveRatesImported.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(yieldCurveRatesImported.CommandId);
        }

        [Fact]
        public async Task ExecuteYieldCurveRatesImportedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from yield_curve_rates where ValueDate = '{TestDataDefaults.YieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}'").ExecuteCommandAsync();
            var failedEvent = default(YieldCurveRatesImportedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockMarketDataEventProducer = new Mock<IMarketDataEventProducer>();
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<YieldCurveRatesImportedFailEvent>()))
                .Callback<YieldCurveRatesImportedFailEvent>(e => failedEvent = e);
            mockMarketDataEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new MarketDataEventDenormalizer(_fixture.DbFactory, mockMarketDataEventProducer.Object, GetLogger<MarketDataEventDenormalizer>());

            var yieldCurveRatesImported = new YieldCurveRatesImportedEvent
            {
                YieldCurveRates = TestDataDefaults.YieldCurveRates
            };

            // when...
            await denormalizer.ExecuteAsync(yieldCurveRatesImported);

            // then...
            var sql = $"select * from yield_curve_rates where ValueDate in('{TestDataDefaults.YieldCurveRates[0].ValueDate.ToString("yyyy - MM - dd")}','{TestDataDefaults.YieldCurveRates[1].ValueDate.ToString("yyyy - MM - dd")}')";
            var yieldCurveRates = await (_fixture.Db.Use(sql)).ExecuteQueryAsync<YieldCurveRateReadModel>();
            yieldCurveRates.Should().NotBeNull();
            yieldCurveRates.Count.Should().Equals(0);
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(yieldCurveRatesImported.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(yieldCurveRatesImported.CommandId);
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
