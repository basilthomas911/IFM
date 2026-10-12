using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;

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
