using System;
using System.Linq;
using System.Reflection;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.ScyllaDb;

public sealed class ScyllaCqlPolicyTests
{
    static readonly string[] DeferredItiQueries =
    [
        "GetFuturesItiSignalsByDateRange",
        "GetFuturesItiSignalMaxTrendValueDate",
        "GetFuturesItiAvgTrendMDI",
        "GetFuturesItiSignalMaxTrendSequenceId",
        "GetFuturesItiSignalMDI",
        "GetFuturesItiSignalMaxValueDateByTrend",
        "GetFuturesItiSignalMaxTimeGroupId",
        "GetFuturesItiSignalMDIByTrend",
        "GetFuturesItiSignalTrendDeltaData",
        "GetFuturesItiSignalTrendClassData",
        "GetFuturesItiTrendDirectionChangedSignals",
        "GetLastFuturesItiSignal",
        "GetLastFuturesItiSignalTrendDirectionChange",
        "GetMaxFuturesItiSignalSequenceIdByTrendDirectionChanged",
        "GetLastFuturesItiSignalTrendExtremeChange",
        "GetLastFuturesItiSignalTrendReversalChange",
        "GetMaxFuturesItiSignalValueDate",
        "GetMaxFuturesItiSignalSequenceId",
        "GetFuturesItiSignalAverageInfo",
        "GetFuturesItiSignalAvgPredictedDelta"
    ];

    [Fact]
    public void AllowFiltering_IsRestrictedToDeferredItiQueries()
    {
        var cqlTypes = typeof(MarketDataDbContext).Assembly.GetTypes()
            .Where(static type => type.Name.EndsWith("Cql", StringComparison.Ordinal));

        var actual = cqlTypes
            .SelectMany(static type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
            .Where(static field => ((string?)field.GetRawConstantValue())?.Contains("ALLOW FILTERING", StringComparison.OrdinalIgnoreCase) == true)
            .Select(static field => field.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var expected = DeferredItiQueries
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }
}
