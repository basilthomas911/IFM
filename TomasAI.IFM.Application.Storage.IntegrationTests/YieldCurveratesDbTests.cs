using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.MarketData.Contracts;

namespace TomasAI.IFM.Application.Storage.IntegrationTests;

[TestClass]
public class YieldCurveRatesDbTests
{
    [TestMethod]
    public async Task ProviderBackedCompatibilityFacadeMapsTreasuryCurve()
    {
        var valueDate = new DateOnly(2026, 8, 14);
        var treasury = Substitute.For<ITreasuryCurve>();
        treasury.GetRangeAsync(valueDate, valueDate, Arg.Any<CancellationToken>())
            .Returns([new TreasuryCurveSnapshot(
                valueDate,
                Enum.GetValues<TreasuryTenor>()
                    .Select((tenor, index) => new TreasuryRatePoint(tenor, 4m + index / 100m))
                    .ToArray(),
                DateTimeOffset.UtcNow,
                "test")]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        var db = new YieldCurveRatesDbContext(
            treasury,
            new ExternalMarketDataCompatibilityOptions(),
            TimeProvider.System,
            logger);

        var yieldCurveRates = await db.ReadAsync(valueDate, valueDate);

        Assert.AreEqual(1, yieldCurveRates.Count);
        Assert.AreEqual(4d, yieldCurveRates.Single().OneMonth);
    }

}
