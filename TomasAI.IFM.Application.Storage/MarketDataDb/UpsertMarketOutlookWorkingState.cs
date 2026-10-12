using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertMarketOutlookWorkingState(
    string contractId,
    DateOnly valueDate,
    long revision,
    DateTime updatedOn,
    string status,
    byte[] workingState) : IBindValue
{
    public object Bind() => new object?[]
    {
        contractId, valueDate, revision, updatedOn, status, workingState
    };
}
