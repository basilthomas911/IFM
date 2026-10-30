using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesContractRolloverIfMissing(
    string symbol,
    DateTime createdOn,
    string createdBy) : IBindValue
{
    public object Bind() => new object?[] { Text(symbol), TimestampTz(createdOn), Text(createdBy) };
}
