using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateFuturesContractRollover(
    string contractId,
    DateOnly nextRolloverDate,
    DateTime updatedOn,
    string updatedBy,
    string symbol) : IBindValue
{
    public object Bind() => new object?[] { Text(contractId), Date(nextRolloverDate), TimestampTz(updatedOn), Text(updatedBy), Text(symbol) };
}
