using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using MathNet.Numerics.Distributions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;

namespace TomasAI.IFM.UnitTests.LossProbability
{
    public class LossProbabilityTests
    {
        [Fact]
        public async Task CalculateLossProbabilityDistribution()
        {
            var connSet = new DbConnectionSettings()
               .Add("TradeDbConnection", "Data Source=DEV-SERVER\\QUERYDATA;Initial Catalog=tradedb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, TradeDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<TradeDbContext>), new TradeDbContext(connSet, dbFactory));
            var db = dbFactory.TradeDb as TradeDbContext;
            var endDate = new DateTime(2019, 10, 1);
            var startDate = endDate.AddDays(-30);
            var forwardLossRatios = await db.GetTradePlanForwardLossRatiosAsync(startDate, endDate);
            var normDist = Normal.Estimate(forwardLossRatios.Select(e => e.ForwardLossRatio));
            var forwardLossRatio = 0.8200;
            var lossProb = normDist.CumulativeDistribution(forwardLossRatio);
            Assert.True(forwardLossRatios.Count > 0);
        }
    }
}
