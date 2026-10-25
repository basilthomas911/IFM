using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal sealed record ProjectionOperation(
    Guid OperationId,
    string ProjectionName,
    bool GlobalWasComplete,
    HashSet<string> CompletedSymbols,
    string[] AffectedSymbols);
