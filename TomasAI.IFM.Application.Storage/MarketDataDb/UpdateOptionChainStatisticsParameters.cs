using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateOptionChainStatisticsParameters(
    string UnderlyingContractId, DateOnly ExpiryDate, DateOnly ValueDate, string ContractId,
    long? Volume, long? OpenInterest, DateOnly? VolumeValueDate, DateOnly? OpenInterestValueDate) : IBindValue
{
    public object Bind() => new object?[] { Volume, OpenInterest, VolumeValueDate, OpenInterestValueDate,
        UnderlyingContractId, ExpiryDate, ValueDate, ContractId };
}
