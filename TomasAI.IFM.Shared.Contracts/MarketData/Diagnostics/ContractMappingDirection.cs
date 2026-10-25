using TomasAI.IFM.Shared.MarketData.Diagnostics;
namespace TomasAI.IFM.Shared.MarketData.Diagnostics;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is an existing meaningful contract value; introducing an alias would change validation and serialization names.")]
public enum ContractMappingDirection
{
    ContractIdToInstrumentId = 1,
    InstrumentIdToContractId = 2
}
