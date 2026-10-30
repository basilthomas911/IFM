using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal sealed record ProjectionInventory(
    HashSet<FuturesContractProjectionKey> FuturesContractSourceKeys,
    HashSet<FuturesContractProjectionKey> FuturesContractTargetKeys,
    HashSet<FuturesOptionContractProjectionKey> FuturesOptionContractSourceKeys,
    HashSet<FuturesOptionContractProjectionKey> FuturesOptionContractTargetKeys,
    int FuturesContractSourceRows,
    int FuturesContractTargetRows,
    int FuturesOptionContractSourceRows,
    int FuturesOptionContractTargetRows);
