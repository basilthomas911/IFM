using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct TickContractDateParameters(string ContractId, DateOnly ValueDate) : IBindValue
{
    public object Bind() => new object?[] { ContractId, ValueDate };
}
