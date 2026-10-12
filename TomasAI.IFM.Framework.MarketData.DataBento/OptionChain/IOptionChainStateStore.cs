using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

public interface IOptionChainStateStore
{
    bool TryGet(
        OptionChainSessionKey session,
        string futuresOptionContractId,
        out OptionChainContractState state);
    IReadOnlyList<OptionChainContractState> GetSession(OptionChainSessionKey session);
}
