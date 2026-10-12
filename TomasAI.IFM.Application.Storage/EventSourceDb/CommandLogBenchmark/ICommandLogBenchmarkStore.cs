using Newtonsoft.Json;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandLogBenchmark;

/// <summary>
/// Deliberately narrow comparison contract. It is not registered in application runtime dependency injection.
/// </summary>
public interface ICommandLogBenchmarkStore
{
    Task<bool> TryInsertAsync(
        CommandLogBenchmarkEntry entry,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid commandId, CancellationToken cancellationToken = default);
}
