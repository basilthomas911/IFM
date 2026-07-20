using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.Events;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Storage;


namespace TomasAI.IFM.Application.Event.Denormalizer.UnitTests
{
    public class ReferenceDatabaseFixture : IDisposable
    {
        
        public ReferenceDatabaseFixture()
        {
            var dbConn = new DbConnectionSettings()
                .Add("ReferenceDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=referencetestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, ReferenceDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            DbFactory = new DbContextFactory(dbResolver);
            var dbCache = new DbCache();
            diContainer.Add(typeof(IObjectRepository<ReferenceDbContext>), new ReferenceDbContext(dbConn, DbFactory));
            Db = DbFactory.ReferenceDb as ReferenceDbContext;
        }

        public ReferenceDbContext Db { get; }
        public IDbContextFactory DbFactory { get; }

        public void Dispose()
        {
        }
    }

    public class ReferenceEventDenormalizerTests : IClassFixture<ReferenceDatabaseFixture>
    {
        private ReferenceDatabaseFixture _fixture;

        public ReferenceEventDenormalizerTests(ReferenceDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ExecuteStrikePriceVolatilityAddedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteCommandAsync();
            var completedEvent = default(StrikePriceVolatilityAddedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            var mockOptionPricerEventProducer = new Mock<IReferenceEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<StrikePriceVolatilityAddedCompleteEvent>()))
                .Callback<StrikePriceVolatilityAddedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);
            var denormalizer = new ReferenceEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<ReferenceEventDenormalizer>());

            var strikePriceVolatilityCreated = new StrikePriceVolatilityAddedEvent
            {
                CommandId = Guid.NewGuid(),
                StrikePriceVolatility = TestDataDefaults.StrikePriceVolatility,
                CreatedOn = new DateTime(2018,10,10),
                CreatedBy = "basilt"
            };

            // when...
            await denormalizer.ExecuteAsync(strikePriceVolatilityCreated);

            // then...
            var strikePriceVolatility = await _fixture.Db.Use($"select *  from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteQueryAsync<StrikePriceVolatilityReadModel>();
            strikePriceVolatility.Should().NotBeNull();
            strikePriceVolatility.Count.Should().Be(1);
            strikePriceVolatility[0].Symbol.Should().Be(TestDataDefaults.StrikePriceVolatility.Symbol);
            strikePriceVolatility[0].TradeType.Should().Be(TestDataDefaults.StrikePriceVolatility.TradeType);
            strikePriceVolatility[0].MarketTrend.Should().Be(TestDataDefaults.StrikePriceVolatility.MarketTrend);
            strikePriceVolatility[0].MarketVolatility.Should().Be(TestDataDefaults.StrikePriceVolatility.MarketVolatility);
            strikePriceVolatility[0].Delta.Should().Be(TestDataDefaults.StrikePriceVolatility.Delta);
            strikePriceVolatility[0].StrikePriceOffset.Should().Be(TestDataDefaults.StrikePriceVolatility.StrikePriceOffset);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(strikePriceVolatilityCreated.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(strikePriceVolatilityCreated.CommandId);
        }

        [Fact]
        public async Task ExecuteStrikePriceVolatilityAddedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteCommandAsync();
            var failedEvent = default(StrikePriceVolatilityAddedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            var mockOptionPricerEventProducer = new Mock<IReferenceEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<StrikePriceVolatilityAddedFailEvent>()))
                .Callback<StrikePriceVolatilityAddedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);
            var denormalizer = new ReferenceEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<ReferenceEventDenormalizer>());

            var strikePriceVolatilityCreated = new StrikePriceVolatilityAddedEvent
            {
                StrikePriceVolatility = TestDataDefaults.StrikePriceVolatility,
                CreatedOn = new DateTime(2018, 10, 10),
                CreatedBy = "basilt"
            };

            // when...
            await denormalizer.ExecuteAsync(strikePriceVolatilityCreated);

            // then...
            var strikePriceVolatility = await _fixture.Db.Use($"select *  from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteQueryAsync<StrikePriceVolatilityReadModel>();

            strikePriceVolatility.Should().NotBeNull();
            strikePriceVolatility.Count.Should().Be(0);

            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(strikePriceVolatilityCreated.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(strikePriceVolatilityCreated.CommandId);
        }

        [Fact]
        public async Task ExecuteStrikePriceVolatilityChangedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteCommandAsync();
            var mockOptionPricerEventProducer = new Mock<IReferenceEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<StrikePriceVolatilityAddedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new ReferenceEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<ReferenceEventDenormalizer>());

            var strikePriceVolatilityCreated = new StrikePriceVolatilityAddedEvent
            {
                CommandId = Guid.NewGuid(),
                StrikePriceVolatility = TestDataDefaults.StrikePriceVolatility,
                CreatedOn = new DateTime(2018, 10, 10),
                CreatedBy = "basilt"
            };

            await denormalizer.ExecuteAsync(strikePriceVolatilityCreated);

            var completedEvent = default(StrikePriceVolatilityChangedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockOptionPricerEventProducer = new Mock<IReferenceEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<StrikePriceVolatilityChangedCompleteEvent>()))
                .Callback<StrikePriceVolatilityChangedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);

            denormalizer = new ReferenceEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<ReferenceEventDenormalizer>());

            // when...
            var strikePriceVolatilityChanged = new StrikePriceVolatilityChangedEvent
            {
                CommandId = Guid.NewGuid(),
                OriginalId = TestDataDefaults.StrikePriceVolatility.Id,
                StrikePriceVolatility = TestDataDefaults.StrikePriceVolatilityChanged,
                UpdatedOn = new DateTime(2018, 10, 10),
                UpdatedBy = "basilt"
            };
            await denormalizer.ExecuteAsync(strikePriceVolatilityChanged);

            // then...
            var strikePriceVolatility = await _fixture.Db.Use($"select *  from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteQueryAsync<StrikePriceVolatilityReadModel>();
            strikePriceVolatility.Should().NotBeNull();
            strikePriceVolatility.Count.Should().Be(1);
            strikePriceVolatility[0].Symbol.Should().Be(TestDataDefaults.StrikePriceVolatility.Symbol);
            strikePriceVolatility[0].TradeType.Should().Be(TestDataDefaults.StrikePriceVolatility.TradeType);
            strikePriceVolatility[0].MarketTrend.Should().Be(TestDataDefaults.StrikePriceVolatilityChanged.MarketTrend);
            strikePriceVolatility[0].MarketVolatility.Should().Be(TestDataDefaults.StrikePriceVolatility.MarketVolatility);
            strikePriceVolatility[0].Delta.Should().Be(TestDataDefaults.StrikePriceVolatility.Delta);
            strikePriceVolatility[0].StrikePriceOffset.Should().Be(TestDataDefaults.StrikePriceVolatility.StrikePriceOffset);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(strikePriceVolatilityChanged.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(strikePriceVolatilityChanged.CommandId);
        }

        [Fact]
        public async Task ExecuteStrikePriceVolatilityChangedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteCommandAsync();
            var mockOptionPricerEventProducer = new Mock<IReferenceEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<StrikePriceVolatilityAddedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new ReferenceEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<ReferenceEventDenormalizer>());

            var strikePriceVolatilityCreated = new StrikePriceVolatilityAddedEvent
            {
                CommandId = Guid.NewGuid(),
                StrikePriceVolatility = TestDataDefaults.StrikePriceVolatility,
                CreatedOn = new DateTime(2018, 10, 10),
                CreatedBy = "basilt"
            };

            await denormalizer.ExecuteAsync(strikePriceVolatilityCreated);

            var failedEvent = default(StrikePriceVolatilityChangedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockOptionPricerEventProducer = new Mock<IReferenceEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<StrikePriceVolatilityChangedFailEvent>()))
                .Callback<StrikePriceVolatilityChangedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);

            denormalizer = new ReferenceEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<ReferenceEventDenormalizer>());

            // when...
            var strikePriceVolatilityChanged = new StrikePriceVolatilityChangedEvent
            {
                OriginalId = TestDataDefaults.StrikePriceVolatility.Id,
                StrikePriceVolatility = TestDataDefaults.StrikePriceVolatilityChanged,
                UpdatedOn = new DateTime(2018, 10, 10),
                UpdatedBy = "basilt"
            };
            await denormalizer.ExecuteAsync(strikePriceVolatilityChanged);

            // then...
            var strikePriceVolatility = await _fixture.Db.Use($"select *  from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteQueryAsync<StrikePriceVolatilityReadModel>();
            strikePriceVolatility.Should().NotBeNull();
            strikePriceVolatility.Count.Should().Be(1);
            strikePriceVolatility[0].Symbol.Should().Be(TestDataDefaults.StrikePriceVolatility.Symbol);
            strikePriceVolatility[0].TradeType.Should().Be(TestDataDefaults.StrikePriceVolatility.TradeType);
            strikePriceVolatility[0].MarketTrend.Should().Be(TestDataDefaults.StrikePriceVolatility.MarketTrend);
            strikePriceVolatility[0].MarketVolatility.Should().Be(TestDataDefaults.StrikePriceVolatility.MarketVolatility);
            strikePriceVolatility[0].Delta.Should().Be(TestDataDefaults.StrikePriceVolatility.Delta);
            strikePriceVolatility[0].StrikePriceOffset.Should().Be(TestDataDefaults.StrikePriceVolatility.StrikePriceOffset);
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(strikePriceVolatilityChanged.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(strikePriceVolatilityChanged.CommandId);
        }

        [Fact]
        public async Task ExecuteStrikePriceVolatilityRemovedEvent()
        {
            // given...
            await _fixture.Db.Use($"delete from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteCommandAsync();
            var mockOptionPricerEventProducer = new Mock<IReferenceEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<StrikePriceVolatilityAddedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new ReferenceEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<ReferenceEventDenormalizer>());

            var strikePriceVolatilityCreated = new StrikePriceVolatilityAddedEvent
            {
                CommandId = Guid.NewGuid(),
                StrikePriceVolatility = TestDataDefaults.StrikePriceVolatility,
                CreatedOn = new DateTime(2018, 10, 10),
                CreatedBy = "basilt"
            };
            await denormalizer.ExecuteAsync(strikePriceVolatilityCreated);

            var completedEvent = default(StrikePriceVolatilityRemovedCompleteEvent);
            var denormalizerCompletedEvent = default(DenormalizerCompletedEvent);
            mockOptionPricerEventProducer = new Mock<IReferenceEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<StrikePriceVolatilityRemovedCompleteEvent>()))
                .Callback<StrikePriceVolatilityRemovedCompleteEvent>(e => completedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()))
                .Callback<DenormalizerCompletedEvent>(e => denormalizerCompletedEvent = e);

            denormalizer = new ReferenceEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<ReferenceEventDenormalizer>());

            // when...
            var strikePriceVolatilityRemoved = new StrikePriceVolatilityRemovedEvent
            {
                CommandId = Guid.NewGuid(),
                StrikePriceVolatilityId = TestDataDefaults.StrikePriceVolatility.Id,
                DeletedOn = new DateTime(2018, 10, 10),
                DeletedBy = "basilt"
            };
            await denormalizer.ExecuteAsync(strikePriceVolatilityRemoved);

            // then...
            var strikePriceVolatility = await _fixture.Db.Use($"select *  from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteQueryAsync<StrikePriceVolatilityReadModel>();
            strikePriceVolatility.Should().NotBeNull();
            strikePriceVolatility.Count.Should().Be(0);
            completedEvent.Should().NotBeNull();
            completedEvent.CommandId.Should().Be(strikePriceVolatilityRemoved.CommandId);
            denormalizerCompletedEvent.Should().NotBeNull();
            denormalizerCompletedEvent.CommandId.Should().Be(strikePriceVolatilityRemoved.CommandId);
        }

        [Fact]
        public async Task ExecuteStrikePriceVolatilityRemovedEventWithEmptyCommandId()
        {
            // given...
            await _fixture.Db.Use($"delete from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteCommandAsync();
            var mockOptionPricerEventProducer = new Mock<IReferenceEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<StrikePriceVolatilityAddedCompleteEvent>()));
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerCompletedEvent>()));
            var denormalizer = new ReferenceEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<ReferenceEventDenormalizer>());

            var strikePriceVolatilityCreated = new StrikePriceVolatilityAddedEvent
            {
                CommandId = Guid.NewGuid(),
                StrikePriceVolatility = TestDataDefaults.StrikePriceVolatility,
                CreatedOn = new DateTime(2018, 10, 10),
                CreatedBy = "basilt"
            };
            await denormalizer.ExecuteAsync(strikePriceVolatilityCreated);

            var failedEvent = default(StrikePriceVolatilityRemovedFailEvent);
            var denormalizerExceptionEvent = default(DenormalizerExceptionEvent);
            mockOptionPricerEventProducer = new Mock<IReferenceEventProducer>();
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<StrikePriceVolatilityRemovedFailEvent>()))
                .Callback<StrikePriceVolatilityRemovedFailEvent>(e => failedEvent = e);
            mockOptionPricerEventProducer
                .Setup(e => e.PostEventAsync(It.IsAny<DenormalizerExceptionEvent>()))
                .Callback<DenormalizerExceptionEvent>(e => denormalizerExceptionEvent = e);

            denormalizer = new ReferenceEventDenormalizer(_fixture.Db, mockOptionPricerEventProducer.Object, GetLogger<ReferenceEventDenormalizer>());

            // when...
            var strikePriceVolatilityRemoved = new StrikePriceVolatilityRemovedEvent
            {
                StrikePriceVolatilityId = TestDataDefaults.StrikePriceVolatility.Id,
                DeletedOn = new DateTime(2018, 10, 10),
                DeletedBy = "basilt"
            };
            await denormalizer.ExecuteAsync(strikePriceVolatilityRemoved);

            // then...
            var strikePriceVolatility = await _fixture.Db.Use($"select *  from strike_price_volatility where Symbol = '{TestDataDefaults.StrikePriceVolatility.Symbol}' " +
                $"and TradeType = '{TestDataDefaults.StrikePriceVolatility.TradeType}'").ExecuteQueryAsync<StrikePriceVolatilityReadModel>();
            strikePriceVolatility.Should().NotBeNull();
            strikePriceVolatility.Count.Should().Be(1);
            failedEvent.Should().NotBeNull();
            failedEvent.CommandId.Should().Be(strikePriceVolatilityRemoved.CommandId);
            denormalizerExceptionEvent.Should().NotBeNull();
            denormalizerExceptionEvent.CommandId.Should().Be(strikePriceVolatilityRemoved.CommandId);
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
