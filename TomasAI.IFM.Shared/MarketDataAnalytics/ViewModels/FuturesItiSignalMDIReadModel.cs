using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalMDIV2ReadModel
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public DateTime IntrinsicTime { get; init; }
    [Key(3)] public IntrinsicTimeTrendType TrendType { get; init; }
    [Key(4)] public double MDI { get; init; }

    public FuturesItiSignalMDIV2ReadModel() { }

    [SerializationConstructor]
    public FuturesItiSignalMDIV2ReadModel(
        string contractId,
        DateOnly valueDate,
        DateTime intrinsicTime,
        IntrinsicTimeTrendType trendType,
        double mdi)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        IntrinsicTime = intrinsicTime;
        TrendType = trendType;
        MDI = mdi;
    }

    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
