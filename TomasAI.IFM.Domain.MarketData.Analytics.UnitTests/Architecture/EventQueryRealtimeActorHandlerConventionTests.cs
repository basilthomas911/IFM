using System.Text.RegularExpressions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Architecture;

/// <summary>Freezes Analytics Event, Query, and Realtime actor manifests and handler conventions.</summary>
public sealed class EventQueryRealtimeActorHandlerConventionTests
{
    static readonly IReadOnlyDictionary<string, string> ExpectedMessages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["FuturesAdxSignalEventActor"] = "FuturesAdxSignalStartedEvent,FuturesAdxSignalStoppedEvent,FuturesAdxSignalGeneratedCompleteEvent,FuturesAdxDailySignalGeneratedCompleteEvent",
        ["FuturesAdxSignalQueryActor"] = "GetFuturesAdxSignalQuery,GetFuturesAdxDailySignalQuery",
        ["FuturesAdxSignalRealtimeActor"] = "FuturesTradeSessionBarClosedRealtimeEvent",
        ["FuturesAtrSignalEventActor"] = "FuturesAtrSignalStartedEvent,FuturesAtrSignalStoppedEvent,FuturesAtrSignalGeneratedCompleteEvent,FuturesAtrDailySignalGeneratedCompleteEvent",
        ["FuturesAtrSignalQueryActor"] = "GetFuturesAtrSignalQuery,GetFuturesAtrDailySignalQuery",
        ["FuturesAtrSignalRealtimeActor"] = "FuturesTradeSessionBarClosedRealtimeEvent",
        ["FuturesBbSignalEventActor"] = "FuturesBbSignalGeneratedCompleteEvent",
        ["FuturesEmaSignalEventActor"] = "FuturesEmaSignalGeneratedCompleteEvent",
        ["FuturesEmaSignalRealtimeActor"] = "FuturesTradeSessionBarClosedRealtimeEvent",
        ["FuturesItiSignalEventActor"] = "FuturesItiSignalGeneratedCompleteEvent,FuturesItiSignalHoldTradeSetCompleteEvent,FuturesItiSignalHoldTradeClearedCompleteEvent",
        ["FuturesItiSignalQueryActor"] = "GetFuturesItiSignalDataQuery,GetFuturesItiSignalQuery,GetFuturesItiSignalHistoryQuery,GetFuturesItiTrendDirectionChangedSignalsQuery",
        ["FuturesItiSignalRealtimeActor"] = "FuturesMarketPriceUpdatedRealtimeEvent",
        ["FuturesMacdSignalEventActor"] = "FuturesMacdSignalStartedEvent,FuturesMacdSignalStoppedEvent,FuturesMacdSignalGeneratedCompleteEvent,FuturesMacdDailySignalGeneratedCompleteEvent",
        ["FuturesMacdSignalQueryActor"] = "GetFuturesMacdSignalQuery,GetFuturesMacdDailySignalQuery",
        ["FuturesMacdSignalRealtimeActor"] = "FuturesTradeSessionBarClosedRealtimeEvent",
        ["FuturesRsiSignalEventActor"] = "FuturesRsiSignalStartedEvent,FuturesRsiSignalStoppedEvent,FuturesRsiSignalGeneratedEvent,FuturesRsiSignalGeneratedCompleteEvent,FuturesRsiDailySignalGeneratedEvent,FuturesRsiDailySignalGeneratedCompleteEvent",
        ["FuturesRsiSignalQueryActor"] = "GetFuturesRsiSignalQuery,GetFuturesRsiDailySignalQuery,GetFuturesTrendDirectionFromRSISignalQuery",
        ["FuturesRsiSignalRealtimeActor"] = "FuturesTradeSessionBarClosedRealtimeEvent",
        ["FuturesTdiSignalEventActor"] = "FuturesTdiSignalGeneratedCompleteEvent,FuturesRsiSignalsGeneratedEvent",
        ["FuturesTdiSignalQueryActor"] = "GetFuturesTdiSignalQuery",
        ["FuturesTdiSignalRealtimeActor"] = "FuturesRsiSignalsGeneratedEvent,FuturesTdiSignalGeneratedFailEvent,FuturesTdiSignalGeneratedCompleteEvent,FuturesTdiSignalGeneratedEvent",
        ["FuturesTradeSessionBarSignalEventActor"] = "FuturesTradeSessionBarPublishedEvent,FuturesTradeSessionBarPublishedCompleteEvent,FuturesTradeSessionBarPublishedFailEvent",
        ["FuturesTradeSessionBarSignalRealtimeActor"] = "FuturesMarketPriceUpdatedRealtimeEvent,FuturesTradeSessionBarSignalBarrierRealtimeEvent",
        ["FuturesTradeSignalEventActor"] = "FuturesTradeSignalUpdatedCompleteEvent,FuturesItiSignalHoldTradeChangedEvent",
        ["FuturesTradeSignalQueryActor"] = "GetFuturesTradeSignalQuery,GetLastFuturesTradeSignalQuery,GetFuturesTradeSignalIdsQuery",
        ["FuturesVwapSignalEventActor"] = "FuturesVwapSignalUpdatedCompleteEvent,FuturesVwapSignalUpdatedFailEvent",
        ["FuturesVwapSignalQueryActor"] = "GetLatestFuturesVwapSignalQuery,GetFuturesVwapSignalHistoryQuery",
        ["FuturesVwapSignalRealtimeActor"] = "FuturesMarketPriceUpdatedRealtimeEvent",
        ["FuturesVxTermStructureSignalEventActor"] = "FuturesVxTermStructureSignalUpdatedCompleteEvent,FuturesVxTermStructureSignalUpdatedFailEvent",
        ["FuturesVxTermStructureSignalQueryActor"] = "GetLatestFuturesVxTermStructureSignalQuery",
        ["FuturesVxTermStructureSignalRealtimeActor"] = "FuturesMarketPriceUpdatedRealtimeEvent",
        ["FuturesAnalyticsHistoricalDataLoaderEventActor"] = "FuturesAnalyticsHistoricalDataLoaderRequestedEvent,FuturesAnalyticsHistoricalDataLoaderCompletedEvent,FuturesAnalyticsHistoricalDataLoaderFailedEvent",
        ["FuturesAnalyticsHistoricalDataLoaderQueryActor"] = "GetFuturesAnalyticsHistoricalDataLoaderQuery",
        ["MarketOutlookSnapshotQueryActor"] = "GetMarketOutlookSnapshotQuery",
        ["MarketOutlookSnapshotRealtimeActor"] = "MarketOutlookComponentChangedRealtimeEvent,MarketOutlookEodUpdatedRealtimeEvent,FuturesMarketPriceUpdatedRealtimeEvent,FuturesSessionStatisticsUpdatedRealtimeEvent,MarketOutlookSnapshotInsertedEvent"
    };

    /// <summary>Verifies every accepted message and its dedicated, thinly dispatched handler.</summary>
    [Fact]
    public void EveryActorMessage_HasOneMappedHandlerInItsRoleFolder()
    {
        var root = FindAnalyticsRoot();
        var actorFiles = Directory.EnumerateFiles(root, "*Actor.cs", SearchOption.AllDirectories)
            .Where(path => Regex.IsMatch(path,
                @"[\\/](Event|Query|Realtime)[\\/]Actor[\\/][^\\/]+Actor\.cs$"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(35, actorFiles.Length);

        foreach (var actorFile in actorFiles)
        {
            var actorName = Path.GetFileNameWithoutExtension(actorFile);
            Assert.True(ExpectedMessages.TryGetValue(actorName, out var expected),
                $"Unregistered Analytics actor: {actorName}");
            var source = File.ReadAllText(actorFile);
            var parseMap = GetMapSource(source, "_parseMap");
            var receiveMap = GetMapSource(source, "_receiveMap");
            var parsed = Regex.Matches(parseMap, @"\[(\w+)\.Verb\]\s*=")
                .Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal).ToArray();
            var received = Regex.Matches(receiveMap, @"\[typeof\((\w+)\)\]\s*=")
                .Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal).ToArray();
            var frozen = expected!.Split(',').Order(StringComparer.Ordinal).ToArray();
            Assert.Equal(frozen, received);
            Assert.Equal(received, parsed);

            var roleDirectory = Directory.GetParent(Directory.GetParent(actorFile)!.FullName)!.FullName;
            var role = Path.GetFileName(roleDirectory);
            Assert.Contains("ParseMapped", source, StringComparison.Ordinal);
            Assert.Contains("ResolveMapped", source, StringComparison.Ordinal);
            if (role == "Query")
            {
                Assert.Contains("CreateQueryExceptionMap(_receiveMap.Keys)", source, StringComparison.Ordinal);
                Assert.Contains("ExceptionMappedQueryAsync", source, StringComparison.Ordinal);
            }

            foreach (var message in received)
            {
                var handlerName = message.EndsWith("RealtimeEvent", StringComparison.Ordinal)
                    ? message[..^"RealtimeEvent".Length]
                    : message.EndsWith("Query", StringComparison.Ordinal)
                        ? message[..^"Query".Length]
                        : message[..^"Event".Length];
                var handlerPath = Path.Combine(roleDirectory, handlerName + ".cs");
                Assert.True(File.Exists(handlerPath), $"{actorName}: missing {role}/{handlerName}.cs");
                var handler = File.ReadAllText(handlerPath);
                Assert.Matches(@"\bclass\s+" + Regex.Escape(handlerName) + @"\b", handler);
                Assert.Matches(@"\bthis\s+" + Regex.Escape(message) + @"\b", handler);
                Assert.Single(Regex.Matches(handler,
                    @"public\s+static\s+[\s\S]{0,140}?\bExecute(?:Async)?\s*\(\s*this\s+"
                    + Regex.Escape(message) + @"\b"));

                var entryStart = receiveMap.IndexOf("[typeof(" + message + ")]", StringComparison.Ordinal);
                var nextStart = receiveMap.IndexOf("[typeof(", entryStart + 1, StringComparison.Ordinal);
                var entry = nextStart < 0 ? receiveMap[entryStart..] : receiveMap[entryStart..nextStart];
                Assert.Contains(".Execute", entry, StringComparison.Ordinal);
                Assert.DoesNotContain("ReplyAsync(", entry, StringComparison.Ordinal);
                Assert.DoesNotContain("PublishMarketOutlookComponentAsync(", entry, StringComparison.Ordinal);
                Assert.DoesNotContain("LogError(", entry, StringComparison.Ordinal);
            }
        }
        Assert.Equal(actorFiles.Length, ExpectedMessages.Count);
    }

    static string GetMapSource(string source, string mapName)
    {
        var start = source.IndexOf(mapName + " =", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing {mapName}");
        var end = Regex.Match(source[start..], @"(?m)^\s*\};");
        Assert.True(end.Success, $"Unterminated {mapName}");
        return source.Substring(start, end.Index + end.Length);
    }

    static string FindAnalyticsRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "TomasAI.IFM.Domain.MarketData.Analytics");
            if (File.Exists(Path.Combine(candidate, "TomasAI.IFM.Domain.MarketData.Analytics.csproj")))
                return candidate;
        }
        throw new DirectoryNotFoundException("Could not locate MarketData.Analytics source for actor architecture verification.");
    }
}
