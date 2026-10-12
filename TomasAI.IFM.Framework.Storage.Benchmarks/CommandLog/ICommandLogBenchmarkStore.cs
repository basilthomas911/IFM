


namespace TomasAI.IFM.Framework.Storage.Benchmarks.CommandLog;

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
