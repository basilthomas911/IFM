using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

public interface IOptionChainStateStore
{
    bool TryGet(
        OptionChainSessionKey session,
        string futuresOptionContractId,
        out OptionChainContractState state);
    IReadOnlyList<OptionChainContractState> GetSession(OptionChainSessionKey session);
    /// <summary>Returns the session count without materializing or sorting its state.</summary>
    int GetCount(OptionChainSessionKey session);
    /// <summary>Copies the requested contracts in order at one coherent instant. The caller owns the destination.</summary>
    bool TryCopyContracts(OptionChainSessionKey session, IReadOnlyList<string> contractIds,
        Span<OptionChainContractState> destination);
}
