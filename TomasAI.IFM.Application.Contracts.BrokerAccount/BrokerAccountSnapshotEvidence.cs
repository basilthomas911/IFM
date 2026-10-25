using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record BrokerAccountSnapshotEvidence
{
    [Key(0)] public string AccountAlias { get; init; } = string.Empty;
    [Key(1)] public string Currency { get; init; } = string.Empty;
    [Key(2)] public decimal CashBalance { get; init; }
    [Key(3)] public decimal AvailableFunds { get; init; }
    [Key(4)] public bool Complete { get; init; }
    [Key(5)] public bool NewRiskAllowed { get; init; }
    [Key(6)] public long Generation { get; init; }
    [Key(7)] public DateTime AsOfUtc { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the published broker message storage width and collection contract.")]
    [Key(8)] public BrokerAccountPositionEvidence[] Positions { get; init; } = [];

    /// <summary>Creates serializable evidence from the application broker contract.</summary>
    public static BrokerAccountSnapshotEvidence From(BrokerAccountSnapshot value) 
    {
        ArgumentNullException.ThrowIfNull(value);
        return new()
    {
        AccountAlias = value.AccountAlias,
        Currency = value.Currency,
        CashBalance = value.CashBalance,
        AvailableFunds = value.AvailableFunds,
        Complete = value.Complete,
        NewRiskAllowed = value.NewRiskAllowed,
        Generation = value.Generation,
        AsOfUtc = value.AsOfUtc,
        Positions = [.. value.Positions
            .OrderBy(static position => position.ContractId, StringComparer.Ordinal)
            .ThenBy(static position => position.SignedQuantity)
            .ThenBy(static position => position.AveragePrice)
            .Select(position => new BrokerAccountPositionEvidence
            {
                ContractId = position.ContractId,
                SignedQuantity = position.SignedQuantity,
                AveragePrice = position.AveragePrice
            })]
    };
    }
}
