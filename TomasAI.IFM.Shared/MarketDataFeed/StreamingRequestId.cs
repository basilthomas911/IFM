using MessagePack;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Represents a streaming request identifier associated with a specific contract and value date.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record StreamingRequestId
{
    [Key(0)] public int RequestId { get; init; }
    [Key(1)] public FuturesOptionContractReadModel OptionContract { get; init; }
    [Key(2)] public FuturesContractV2ReadModel UnderlyingContract { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public DateOnly MaturityDate { get; init; }
    [Key(5)] public double RiskFreeRate { get; init; }
    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public StreamingRequestId() { }

    /// <summary>
    /// Full constructor initializing all serialized properties.
    /// </summary>
    [SerializationConstructor]
    public StreamingRequestId(
        int requestId, 
        FuturesOptionContractReadModel optionContract,
        FuturesContractV2ReadModel underlyingContract,
        DateOnly valueDate,
        DateOnly maturityDate,
        double riskFreeRate)
    {
        RequestId = requestId;
        OptionContract = optionContract ?? throw new ArgumentNullException(nameof(optionContract));
        UnderlyingContract = underlyingContract ?? throw new ArgumentNullException(nameof(underlyingContract));
        ValueDate = valueDate;
        MaturityDate = maturityDate;
        RiskFreeRate = riskFreeRate;
    }

    [IgnoreMember]
    public bool IsValid
        => RequestId > 0 && OptionContract.IsValid && UnderlyingContract.IsValid;
}
