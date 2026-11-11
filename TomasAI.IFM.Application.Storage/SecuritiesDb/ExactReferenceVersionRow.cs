namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal readonly record struct ExactReferenceVersionRow(
    (string ContractId, string Version) Key,
    (string Digest, bool Published, byte[] Payload) Value);
