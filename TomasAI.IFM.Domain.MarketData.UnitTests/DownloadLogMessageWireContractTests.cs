using System.Reflection;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

/// <summary>Locks the published DownloadLog actor-message schemas during convention migration.</summary>
public sealed class DownloadLogMessageWireContractTests
{
    /// <summary>Gets the exact key counts for the four DownloadLog command and query contracts.</summary>
    public static TheoryData<Type, int> Contracts => new()
    {
        { typeof(InsertMarketDataDownloadLogCommand), 8 },
        { typeof(GetMarketDataDownloadStatusQuery), 5 },
        { typeof(GetMarketDataDownloadLogQuery), 4 },
        { typeof(GetMarketDataDownloadHistoryQuery), 5 },
        { typeof(MarketDataDownloadLogInsertedEvent), 10 },
        { typeof(MarketDataDownloadLogInsertedCompleteEvent), 10 },
        { typeof(MarketDataDownloadLogInsertedFailEvent), 17 },
    };

    /// <summary>Checks direct numeric keys, the explicit attribute, and standard serializer round trips.</summary>
    [Theory, MemberData(nameof(Contracts))]
    public void Messages_keep_their_numeric_schema(Type contract, int keyCount)
    {
        Assert.True(contract.GetCustomAttribute<MessagePackObjectAttribute>()?.AllowPrivate);
        var keys = contract.GetProperties()
            .Select(property => property.GetCustomAttribute<KeyAttribute>())
            .OfType<KeyAttribute>()
            .Select(key => key.IntKey!.Value)
            .Order()
            .ToArray();
        Assert.Equal(Enumerable.Range(0, keyCount), keys);
        typeof(DownloadLogMessageWireContractTests)
            .GetMethod(nameof(RoundTrip), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(contract)
            .Invoke(null, [Activator.CreateInstance(contract)!]);
    }

    static void RoundTrip<T>(T message)
    {
        var bytes = MessagePackBinarySerializer.Shared.Serialize(message)!;
        var restored = MessagePackBinarySerializer.Shared.Deserialize<T>(bytes);
        Assert.NotNull(restored);
        Assert.Equal(bytes, MessagePackBinarySerializer.Shared.Serialize(restored));
    }
}
