using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

/// <summary>
/// One coherent Market Outlook display snapshot. EOD is the publication clock;
/// the trade signal contains the newest asynchronous analytics observed before it.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketOutlookSnapshotReadModel
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public DateOnly ValueDate { get; init; }
    [Key(2)] public long Revision { get; init; }
    [Key(3)] public DateTime UpdatedOn { get; init; }
    [Key(4)] public FuturesEodDataV2ReadModel FuturesEodData { get; init; } = new();
    [Key(5)] public FuturesTradeSignalV2ReadModel? FuturesTradeSignal { get; init; }
    [Key(6)] public string MissingInputs { get; init; } = string.Empty;

    [IgnoreMember]
    public bool IsComplete => FuturesEodData.IsValid
        && FuturesTradeSignal is { IsValid: true };

    [IgnoreMember]
    public bool IsValid => !string.IsNullOrWhiteSpace(ContractId)
        && ValueDate != default
        && Revision > 0
        && FuturesEodData.IsValid;

    public MarketOutlookSnapshotReadModel() { }

    [SerializationConstructor]
    public MarketOutlookSnapshotReadModel(
        string contractId,
        DateOnly valueDate,
        long revision,
        DateTime updatedOn,
        FuturesEodDataV2ReadModel futuresEodData,
        FuturesTradeSignalV2ReadModel? futuresTradeSignal,
        string missingInputs)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
        Revision = revision;
        UpdatedOn = updatedOn;
        FuturesEodData = futuresEodData ?? new FuturesEodDataV2ReadModel();
        FuturesTradeSignal = futuresTradeSignal;
        MissingInputs = missingInputs ?? string.Empty;
    }
}
