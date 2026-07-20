using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Composite identifier for a batch of generated futures RSI signals, keyed by contract, value date and timestamp.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesRsiSignalsId
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public TimeOnly Timestamp { get; init; }

    public FuturesRsiSignalsId() { }

    public FuturesRsiSignalsId(string contractId, DateOnly valueDate, TimeOnly timestamp)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        Timestamp = timestamp;
    }

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
