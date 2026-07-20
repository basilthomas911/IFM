using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Query.UnitTests
{
    public class TradePlanFixture : IDisposable
    {
        TradeDbContext _db;
        IDbContextFactory _dbFactory;
        
        public TradePlanFixture()
        {
            var dbConn = new DbConnectionSettings()
                 .Add("TradeDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=tradedb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                 .Add("MarketDataDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, IObjectRepository>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            _dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<TradeDbContext>), new TradeDbContext(dbConn, _dbFactory));
            diContainer.Add(typeof(IObjectRepository<MarketDataDbContext>), new MarketDataDbContext(dbConn, _dbFactory));
            _db = _dbFactory.TradeDb as TradeDbContext;
        }

        public TradeDbContext TradeDb => _db;
        public IDbContextFactory DbFactory => _dbFactory;

        public void Dispose()
        {
        }
    }

    public class TradePlanQueryTests : IClassFixture<TradePlanFixture>
    {
        private TradePlanFixture _fixture;

        public TradePlanQueryTests(TradePlanFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetLossProbabilityOk()
        {
            var q = new GetLossProbabilityQuery
            {
                ForwardLossRatio = 0.30,
                ValueDate = new DateTime(2020, 3, 5)
            };
            var db =_fixture.DbFactory; 
            var tradePlanQuery = new TradePlanQueries(db,  new DataCacheService());
            _ = await tradePlanQuery.ExecuteAsync(q);
            var tradePlanLossProbability = await tradePlanQuery.ExecuteAsync(q);
            Assert.NotNull(tradePlanLossProbability);

        }

        [Fact]
        public async Task GetLossProbabilityDistributionOk()
        {
            var q = new GetLossProbabilityDistributionQuery
            {
                ValueDate = new DateTime(2022, 9, 26)
            };
            var db = _fixture.DbFactory;
            var tradePlanQuery = new TradePlanQueries(db, new DataCacheService());
            _ = await tradePlanQuery.ExecuteAsync(q);
            var tradePlanLossProbability = await tradePlanQuery.ExecuteAsync(q);
            Assert.NotNull(tradePlanLossProbability);

        }
        [Fact]
        public async Task GetIronCondorForwardDeltaOk()
        {
            var q = new GetIronCondorForwardDeltaQuery
            {
                ValueDate = DateTime.Now.Date,
                TradeType = Shared.Trade.TradeType.LongIronCondor,
                RiskPositionType = Shared.MarketDataFeed.RiskPositionType.High
            };
            var db = _fixture.DbFactory;
            var tradePlanQuery = new TradePlanQueries(db, new DataCacheService());
            var forwardDeltaVM = await tradePlanQuery.ExecuteAsync(q);
            Assert.NotNull(forwardDeltaVM);

        }
    }
}
