using Cassandra;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Retries only idempotent raw-tick upserts after a server write timeout; identities and evidence are never regenerated.</summary>
internal static class ScyllaTickWriteRetry
{
    /// <summary>Recreates each attempt's provider bindings and preserves the final failure after three attempts.</summary>
    internal static async Task ExecuteAsync(Func<Task> write, Func<TimeSpan, Task>? delay = null)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { await write().ConfigureAwait(false); return; }
            catch (Exception error) when (attempt < 2 && IsServerWriteTimeout(error))
            {
                await (delay ?? Task.Delay)(TimeSpan.FromMilliseconds(attempt == 0 ? 100 : 400)).ConfigureAwait(false);
            }
        }
    }

    static bool IsServerWriteTimeout(Exception error)
    {
        while (error.InnerException is { } inner) error = inner;
        return error is WriteTimeoutException;
    }
}
