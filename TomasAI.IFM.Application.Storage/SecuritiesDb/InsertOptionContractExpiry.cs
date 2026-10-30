using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertOptionContractExpiry(
    string symbol, Guid generation, DateOnly expiryDate, string providerRoot,
    string contractId, string underlyingContractId, string optionFamily,
    byte[] definitionPayload, DateTime refreshedAtUtc) : IBindValue
{
    public object Bind() => new object?[]
        { symbol, generation, expiryDate, providerRoot, contractId, underlyingContractId,
            optionFamily, definitionPayload, refreshedAtUtc };
}
