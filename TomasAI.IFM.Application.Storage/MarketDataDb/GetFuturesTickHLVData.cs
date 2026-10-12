using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve the established public storage/serialized schema contract and identifiers consumed by callers.")]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetFuturesTickHLVData(string contractId, DateOnly valueDate) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate };
}
