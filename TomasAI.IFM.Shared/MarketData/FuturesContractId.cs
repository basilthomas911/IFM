using MessagePack;
using System.Xml.Linq;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketData;

/// <summary>
/// Represents the unique identifier for a futures contract, including its contract ID, symbol, and maturity date.
/// </summary>
/// <remarks>This record is used to uniquely identify a futures contract in a trading system.  It includes the
/// contract's identifier, the associated symbol, and the maturity date.</remarks>
/// <param name="ContractId"></param>
/// <param name="Symbol"></param>
/// <param name="MaturityDate"></param>
[MessagePackObject]
public record FuturesContractId : IActorEntityId
{
    [IgnoreMember] const string VixSymbol = "VX";

    [Key(0)]
    public string ContractId { get; init; }

    [Key(1)]
    public string Symbol { get; init; }

    [Key(2)]
    public DateOnly MaturityDate { get; init; }

    [IgnoreMember]
    public bool IsVixContract => !string.IsNullOrEmpty(Symbol) && Symbol == VixSymbol;

    public string Format()
        => string.Create(null, stackalloc char[64], $"{ContractId}.{Symbol}.{MaturityDate:yyyyMMdd}");

    [SerializationConstructor]
    public FuturesContractId(string contractId, string symbol, DateOnly maturityDate)
    {
        ContractId = contractId;
        Symbol = symbol;
        MaturityDate = maturityDate;
    }
}