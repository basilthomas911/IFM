using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ReferenceVersionStorageRow(
    string Digest,
    bool Published,
    TomasAI.IFM.Domain.Reference.Shared.ViewModels.ReferenceContractVersion Value);
