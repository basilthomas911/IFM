using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesOptionContractById(string contractId, DateOnly contractMonth, string symbol, string optionType, double strikePrice) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId), Date(contractMonth), Text(symbol), Text(optionType), Double(strikePrice) };
}
