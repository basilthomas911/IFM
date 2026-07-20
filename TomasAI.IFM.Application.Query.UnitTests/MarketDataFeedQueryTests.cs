using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Moq;
using FluentAssertions;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Query;

namespace TomasAI.IFM.Application.Query.UnitTests
{
    public class MarketDataFeedFixture : IDisposable
    {
        private MarketDataDbContext _db;
        private IDbContextFactory _dbFactory;

        public MarketDataDbContext Db => _db;
        public IDbContextFactory DbFactory => _dbFactory;
        
        public MarketDataFeedFixture()
        {
            var dbConn = new DbConnectionSettings()
                .Add("MarketDataDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=marketdatatestdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, MarketDataDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            _dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, _dbFactory));
            _db = _dbFactory.MarketDataDb as MarketDataDbContext;
        }

        public void Dispose()
        {
        }
    }

    /*
        IAsyncQueryHandler<GetLastFuturesTickDataQuery, FuturesTickDataViewModel>,
        IAsyncQueryHandler<GetLastFuturesOptionTickDataQuery, FuturesOptionTickDataViewModel>,
        IAsyncQueryHandler<GetFuturesEodDataQuery, FuturesEodDataViewModel>,
        IAsyncQueryHandler<GetFuturesEodDataByDateRangeQuery, FuturesEodDataViewModel[]>,
        IAsyncQueryHandler<GetIronCondorMarketDataFeedQuery, IronCondorMarketDataFeedReadModel>,
        IAsyncQueryHandler<GetFuturesEodDataParametersQuery, FuturesEodDataParametersReadModel>,
        IAsyncQueryHandler<GetFuturesOptionContractQuery, FuturesOptionContractReadModel>,
        IAsyncQueryHandler<GetFuturesOptionSpreadDataQuery, FuturesOptionSpreadDataReadModel>

     */
    public class MarketDataFeedQueryTests : IClassFixture<MarketDataFeedFixture>
    {
        private MarketDataFeedFixture _fixture;

        public MarketDataFeedQueryTests(MarketDataFeedFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetIronCondorMarketDataFeedQueryOk()
        {
            // given...
            var db = _fixture.Db;
            await db.Use($"delete from futures_tick_data where ContractId = '{SampleData.FuturesTickData[0].ContractId}'").ExecuteCommandAsync();
            await db.InsertFuturesTickDataAsync(SampleData.FuturesTickData[0]);
            await db.Use($"delete from futures_option_tick_data where ContractId = '{SampleData.FuturesShortPutOptionTickData.ContractId}'").ExecuteCommandAsync();
            await db.InsertFuturesOptionTickDataAsync(SampleData.FuturesShortPutOptionTickData);
            await db.Use($"delete from futures_option_tick_data where ContractId = '{SampleData.FuturesLongPutOptionTickData.ContractId}'").ExecuteCommandAsync();
            await db.InsertFuturesOptionTickDataAsync(SampleData.FuturesLongPutOptionTickData);
            await db.Use($"delete from futures_option_tick_data where ContractId = '{SampleData.FuturesShortCallOptionTickData.ContractId}'").ExecuteCommandAsync();
            await db.InsertFuturesOptionTickDataAsync(SampleData.FuturesShortCallOptionTickData);
            await db.Use($"delete from futures_option_tick_data where ContractId = '{SampleData.FuturesLongCallOptionTickData.ContractId}'").ExecuteCommandAsync();
            await db.InsertFuturesOptionTickDataAsync(SampleData.FuturesLongCallOptionTickData);
            var mdfQuery = new MarketDataFeedQueries(_fixture.DbFactory, null);

            // when...
            var query = new GetIronCondorMarketDataFeedQuery {
                UnderlyingContractId = $"{SampleData.FuturesTickData[0].ContractId}",
                ShortPutOptionContractId = $"{SampleData.FuturesShortPutOptionTickData.ContractId}",
                LongPutOptionContractId = $"{SampleData.FuturesLongPutOptionTickData.ContractId}",
                ShortCallOptionContractId = $"{SampleData.FuturesShortCallOptionTickData.ContractId}",
                LongCallOptionContractId = $"{SampleData.FuturesLongCallOptionTickData.ContractId}",
            };
            var viewModel = await mdfQuery.ExecuteAsync(query);

            // then...
            viewModel.Should().NotBeNull();
            $"{viewModel.AssetPrice:F2}".Should().Be($"{SampleData.FuturesTickData[0].Price:F2}");
            viewModel.LongCallOptionData.Should().NotBeNull();
            viewModel.LongPutOptionData.Should().NotBeNull();
            viewModel.ShortCallOptionData.Should().NotBeNull();
            viewModel.ShortPutOptionData.Should().NotBeNull();
            viewModel.LongCallOptionData.ContractId.Should().Be(SampleData.FuturesLongCallOptionTickData.ContractId);
            viewModel.LongPutOptionData.ContractId.Should().Be(SampleData.FuturesLongPutOptionTickData.ContractId);
            viewModel.ShortCallOptionData.ContractId.Should().Be(SampleData.FuturesShortCallOptionTickData.ContractId);
            viewModel.ShortPutOptionData.ContractId.Should().Be(SampleData.FuturesShortPutOptionTickData.ContractId);
        }

        [Fact]
        public async Task GetLastFuturesTickDataQueryOk()
        {
            var db = _fixture.Db;
            await db.Use($"delete from futures_tick_data where ContractId = '{SampleData.FuturesTickData[0].ContractId}'").ExecuteCommandAsync();
            await db.InsertFuturesTickDataAsync(SampleData.FuturesTickData[0]);
            var mdfQuery = new MarketDataFeedQueries(_fixture.DbFactory, null);
            var query = new GetLastFuturesTickDataQuery { ContractId = $"{SampleData.FuturesTickData[0].ContractId}" };
            var viewModel = await mdfQuery.ExecuteAsync(query);
            viewModel.Should().NotBeNull();
            viewModel.ContractId.Should().Be(SampleData.FuturesTickData[0].ContractId);
            $"{viewModel.TickDate:yyyy-MM-dd}".Should().Be($"{SampleData.FuturesTickData[0].TickDate:yyyy-MM-dd}");
            viewModel.TickTime.Should().Be(SampleData.FuturesTickData[0].TickTime);
            $"{viewModel.Price:F2}".Should().Be($"{SampleData.FuturesTickData[0].Price:F2}");
            viewModel.Size.Should().Be(SampleData.FuturesTickData[0].Size);
            $"{viewModel.ValueDate:yyyy-MM-dd}".Should().Be($"{SampleData.FuturesTickData[0].ValueDate:yyyy-MM-dd}");
        }

        [Fact]
        public async Task GetLastFuturesOptionTickDataQueryOk()
        {
            var db = _fixture.Db;
            await db.Use($"delete from futures_option_tick_data where ContractId = '{SampleData.FuturesOptionTickData.ContractId}'").ExecuteCommandAsync();
            await db.InsertFuturesOptionTickDataAsync(SampleData.FuturesOptionTickData);
            var mdfQuery = new MarketDataFeedQueries(_fixture.DbFactory, null);
            var query = new GetLastFuturesOptionTickDataQuery { ContractId = $"{SampleData.FuturesOptionTickData.ContractId}" };
            var viewModel = await mdfQuery.ExecuteAsync(query);
            viewModel.Should().NotBeNull();
            viewModel.ContractId.Should().Be(SampleData.FuturesOptionTickData.ContractId);
            $"{viewModel.TickDate:yyyy-MM-dd}".Should().Be($"{SampleData.FuturesOptionTickData.TickDate:yyyy-MM-dd}");
            viewModel.TickTime.Should().Be(SampleData.FuturesOptionTickData.TickTime);
            $"{viewModel.OptionPrice:F2}".Should().Be( $"{SampleData.FuturesOptionTickData.OptionPrice:F2}");
            $"{viewModel.BidPrice:F2}".Should().Be($"{SampleData.FuturesOptionTickData.BidPrice:F2}");
            $"{viewModel.AskPrice:F2}".Should().Be($"{SampleData.FuturesOptionTickData.AskPrice:F2}");
            viewModel.BidSize.Should().Be(SampleData.FuturesOptionTickData.BidSize);
            viewModel.AskSize.Should().Be(SampleData.FuturesOptionTickData.AskSize);
            $"{viewModel.ImpliedVolatility:F4}".Should().Be($"{SampleData.FuturesOptionTickData.ImpliedVolatility:F4}");
            $"{viewModel.Delta:F4}".Should().Be($"{SampleData.FuturesOptionTickData.Delta:F4}");
            $"{viewModel.Gamma:F4}".Should().Be($"{SampleData.FuturesOptionTickData.Gamma:F4}");
            $"{viewModel.Vega:F4}".Should().Be($"{SampleData.FuturesOptionTickData.Vega:F4}");
            $"{viewModel.Theta:F4}".Should().Be($"{SampleData.FuturesOptionTickData.Theta:F4}");
            $"{viewModel.Rho:F4}".Should().Be($"{SampleData.FuturesOptionTickData.Rho:F4}");
        }

        [Fact]
        public async Task GetFuturesEodDataQueryOk()
        {
            var db = _fixture.Db;
            await db.Use($"delete from futures_eod_data").ExecuteCommandAsync();
            foreach (var futuresEodData in SampleData.FuturesEodDataValues)
                await db.InsertFuturesEodDataAsync(futuresEodData);
            var mdfQuery = new MarketDataFeedQueries(_fixture.DbFactory, null);
            var query = new GetFuturesEodDataQuery
            {
                ContractId = SampleData.FuturesEodDataValues[0].ContractId,
                ValueDate = SampleData.FuturesEodDataValues[0].ValueDate
            };
            var viewModel = await mdfQuery.ExecuteAsync(query);
            viewModel.ContractId.Should().Be(SampleData.FuturesEodDataValues[0].ContractId);
            $"{viewModel.ValueDate:yyyy-MM-dd}".Should().Be( $"{SampleData.FuturesEodDataValues[0].ValueDate:yyyy-MM-dd}");
            $"{viewModel.OpenPrice:F2}".Should().Be( $"{SampleData.FuturesEodDataValues[0].OpenPrice:F2}");
            $"{viewModel.HighPrice:F2}".Should().Be( $"{SampleData.FuturesEodDataValues[0].HighPrice:F2}");
            $"{viewModel.LowPrice:F2}".Should().Be( $"{SampleData.FuturesEodDataValues[0].LowPrice:F2}");
            $"{viewModel.ClosePrice:F2}".Should().Be( $"{SampleData.FuturesEodDataValues[0].ClosePrice:F2}");
            viewModel.Volume.Should().Be(SampleData.FuturesEodDataValues[0].Volume);
            $"{viewModel.DailyPercentChange:F6}".Should().Be( $"{SampleData.FuturesEodDataValues[0].DailyPercentChange:F6}");
            $"{viewModel.DailyStdDev:F6}".Should().Be( $"{SampleData.FuturesEodDataValues[0].DailyStdDev:F6}");
            $"{viewModel.UpperBand:F2}".Should().Be( $"{SampleData.FuturesEodDataValues[0].UpperBand:F2}");
            $"{viewModel.Mean:F2}".Should().Be( $"{SampleData.FuturesEodDataValues[0].Mean:F2}");
            $"{viewModel.LowerBand:F2}".Should().Be( $"{SampleData.FuturesEodDataValues[0].LowerBand:F2}");
            viewModel.MarketDirection.Should().Be(SampleData.FuturesEodDataValues[0].MarketDirection);
            viewModel.PriceDirection.Should().Be(SampleData.FuturesEodDataValues[0].PriceDirection);
        }

        [Fact]
        public async Task GetFuturesEodDataByDateRangeQueryOk()
        {
            var db = _fixture.Db;
            await db.Use($"delete from futures_eod_data").ExecuteCommandAsync();
            foreach (var futuresEodData in SampleData.FuturesEodDataValues)
                await db.InsertFuturesEodDataAsync(futuresEodData);
            var mdfQuery = new MarketDataFeedQueries(_fixture.DbFactory, null);
            var query = new GetFuturesEodDataByDateRangeQuery
            {
                ContractId = SampleData.FuturesEodDataValues[0].ContractId,
                StartDate = SampleData.FuturesEodDataValues[0].ValueDate.AddDays(-60),
                EndDate = SampleData.FuturesEodDataValues[0].ValueDate.AddDays(60)
            };
            var viewModel = await mdfQuery.ExecuteAsync(query);
            Assert.NotNull(viewModel);
            Assert.True(viewModel.Length > 0);
            Assert.Equal(viewModel[0].ContractId, SampleData.FuturesEodDataValues[0].ContractId);
        }

        [Fact]
        public async Task GetFuturesEodDataParametersQueryOk()
        {
            var futuresEodDataParam = new FuturesEodDataParametersReadModel
            (
                FuturesEodDataToday: SampleData.FuturesEodDataValues[0],
                FuturesEodDataRange: SampleData.FuturesEodDataValues,
                NormalCurveTable: new NormalCurveTableReadModel
                (
                    NormalCurveTable: new NormalCurveDataReadModel[] {
                        new NormalCurveDataReadModel
                        (
                            Index: 1,
                            Percent: 0.45
                        )
                   }
                )
            );
            var db = _fixture.Db;
            await db.Use($"delete from futures_eod_data").ExecuteCommandAsync();
            foreach (var futuresEodData in SampleData.FuturesEodDataValues)
                await db.InsertFuturesEodDataAsync(futuresEodData);
            var mdfQuery = new MarketDataFeedQueries(_fixture.DbFactory, null);
            var query = new GetFuturesEodDataParametersQuery
            {
                ContractId = "ES20190920",
                ValueDate = new DateTime(2019, 8, 1)
            };
            var viewModel = await mdfQuery.ExecuteAsync(query);
            Assert.NotNull(viewModel);
        }

        [Fact]
        public async Task GetFuturesOptionContractQueryOk()
        {
            // given...
            var mockMarketDataSnapshotApi = new Mock<IMarketDataSnapshotApi>(MockBehavior.Strict);
            var streamIds = new StreamIdCollection();
            var futuresOptionContract = default(FuturesOptionContractReadModel);
            mockMarketDataSnapshotApi.SetupGet(e => e.StreamIds).Returns(streamIds);
            mockMarketDataSnapshotApi.Setup(e => e.Start(It.IsAny<Action<int, string>>()));
            mockMarketDataSnapshotApi.Setup(e => e.Stop());
            mockMarketDataSnapshotApi
                .Setup(e => e.GetFuturesOptionContractAsync(It.IsAny<int>(), It.IsAny<FuturesOptionContractReadModel>()))
                .ReturnsAsync(SampleData.FuturesOptionContract)
                .Callback<int,FuturesOptionContractReadModel>( (_, qfContract) => futuresOptionContract = qfContract );
            var mdfQuery = new MarketDataFeedQueries(null, mockMarketDataSnapshotApi.Object);

            // when...
            var q = new GetFuturesOptionContractQuery
            {
                ContractId = SampleData.FuturesOptionContract.ContractId,
                QueryForContract = SampleData.FuturesOptionContract
            };
            var viewModel = await mdfQuery.ExecuteAsync(q);

            // then..
            streamIds.Count.Should().Be(0);
            futuresOptionContract.Should().NotBeNull();
            viewModel.Should().NotBeNull();
            viewModel.ContractId.Should().Be(SampleData.FuturesOptionContract.ContractId);
        }

        [Fact]
        public void GetFuturesOptionContractQueryThrowsException()
        {
            // given...
            var mockMarketDataSnapshotApi = new Mock<IMarketDataSnapshotApi>(MockBehavior.Strict);
            var streamIds = new StreamIdCollection();
            mockMarketDataSnapshotApi.SetupGet<IStreamIdCollection>(e => e.StreamIds).Returns(streamIds);
            mockMarketDataSnapshotApi.Setup(e => e.Start(It.IsAny<Action<int, string>>()));
            mockMarketDataSnapshotApi.Setup(e => e.Stop());
            mockMarketDataSnapshotApi
                .Setup(e => e.GetFuturesOptionContractAsync(It.IsAny<int>(), It.IsAny<FuturesOptionContractReadModel>()))
                .Throws<InvalidOperationException>();
            var mdfQuery = new MarketDataFeedQueries(null, mockMarketDataSnapshotApi.Object);

            // when...
            var q = new GetFuturesOptionContractQuery {
                ContractId = SampleData.FuturesOptionContract.ContractId,
                QueryForContract = SampleData.FuturesOptionContract
            };

            var result = default(FuturesOptionContractReadModel);
            Action throwAction =  () =>  result = mdfQuery.ExecuteAsync(q).Result;

            // then..
            throwAction.Should().Throw<QueryException>();
        }

        [Fact]
        public async Task GetFuturesOptionSpreadDataQueryOk()
        {
            // given...
            var mockMarketDataSnapshotApi = new Mock<IMarketDataSnapshotApi>(MockBehavior.Strict);
            var streamIds = new StreamIdCollection();
            var futuresOptionShortPutContract = default(FuturesOptionContractReadModel);
            var futuresOptionLongPutContract = default(FuturesOptionContractReadModel);
            var tickOptionComputation = new TickOptionComputation("put", 0.12, 0.09, 2.34, 0.0, 0.04, 0.55, 0.44, 0.33, 2895.25);
            mockMarketDataSnapshotApi.SetupGet(e => e.StreamIds).Returns(streamIds);
            mockMarketDataSnapshotApi.Setup(e => e.Start(It.IsAny<Action<int, string>>()));
            mockMarketDataSnapshotApi.Setup(e => e.Stop());
            mockMarketDataSnapshotApi
                .Setup(e => e.GetFuturesOptionSpreadAsync(It.IsAny<FuturesOptionContractReadModel>(), It.IsAny<FuturesOptionContractReadModel>()))
                .ReturnsAsync( (SampleData.FuturesOptionShortPutContract, SampleData.FuturesOptionShortPutContract))
                .Callback<FuturesOptionContractReadModel, FuturesOptionContractReadModel>((shortContract, longContract) => { futuresOptionShortPutContract = shortContract; futuresOptionLongPutContract = longContract; });
            mockMarketDataSnapshotApi
                .Setup(e => e.GetFuturesOptionPriceAsync(It.IsAny<int>(), It.IsAny<FuturesOptionContractReadModel>(), It.IsAny<Action<FuturesOptionTickDataViewModel>>()))
                .Returns(Task.CompletedTask)
                .Callback<int, FuturesOptionContractReadModel, Action<FuturesOptionTickDataViewModel>>( (_, _, handler) => { handler(SampleData.FuturesOptionTickData); });
            mockMarketDataSnapshotApi
                .Setup(e => e.GetFuturesOptionGreeks(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<FuturesOptionContractReadModel>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .Returns(tickOptionComputation);

            var mdfQuery = new MarketDataFeedQueries(null, mockMarketDataSnapshotApi.Object);

            // when...
            var q = new GetFuturesOptionSpreadDataQuery
            {
                QueryForShortOptionContract = SampleData.FuturesOptionShortPutContract,
                QueryForLongOptionContract = SampleData.FuturesOptionLongPutContract
            };
            var viewModel = await mdfQuery.ExecuteAsync(q);

            // then..
            streamIds.Count.Should().Be(0);
            viewModel.Should().NotBeNull();
            viewModel.ShortLeg.Should().NotBeNull();
            $"{viewModel.ShortLeg.BidPrice:F2}".Should().Be($"{SampleData.FuturesOptionTickData.BidPrice:F2}");
            $"{viewModel.ShortLeg.AskPrice:F2}".Should().Be($"{SampleData.FuturesOptionTickData.AskPrice:F2}");
            $"{viewModel.ShortLeg.Delta:F4}".Should().Be( $"{tickOptionComputation.Delta:F4}");
            $"{viewModel.ShortLeg.Gamma:F4}".Should().Be( $"{tickOptionComputation.Gamma:F4}");
            $"{viewModel.ShortLeg.Theta:F4}".Should().Be( $"{tickOptionComputation.Theta:F4}");
        }
    }
}
