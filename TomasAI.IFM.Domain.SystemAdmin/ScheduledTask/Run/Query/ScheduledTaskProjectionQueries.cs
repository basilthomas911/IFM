using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Query;

/// <summary>Prepares history selections and interprets completed-close facts from projected run rows.</summary>
public static class ScheduledTaskProjectionQueries
{
    extension(IScheduledTaskReadStore store)
    {
        /// <summary>Validates an opaque client continuation before calling persistence.</summary>
        /// <param name="environment">The requested environment scope.</param>
        /// <param name="host">The owning host identity.</param>
        /// <param name="schedule">The schedule identity.</param>
        /// <param name="pageSize">The requested page size, bounded to 1 through 100.</param>
        /// <param name="paging">The original opaque continuation, or null for the first page.</param>
        /// <param name="token">Cancels the persisted history query.</param>
        /// <returns>The requested projected run page, without diagnostic output tails.</returns>
        public ValueTask<ScheduledTaskRunPage> GetRunHistoryAsync(string environment, string host,
            ScheduledTaskId schedule, int pageSize, byte[]? paging, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentException.ThrowIfNullOrWhiteSpace(environment);
            ArgumentException.ThrowIfNullOrWhiteSpace(host);
            var scope = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { environment, host, schedule = schedule.Value })));
            var cursor = paging is null ? null : JsonSerializer.Deserialize<ScheduledTaskHistoryCursor>(paging)
                ?? throw new ArgumentException("Invalid scheduled-task history cursor.", nameof(paging));
            if (cursor is not null && (!string.Equals(cursor.Scope, scope, StringComparison.Ordinal) || cursor.Fire.Kind != DateTimeKind.Utc))
                throw new ArgumentException("Scheduled-task cursor belongs to another query or has an invalid timestamp.", nameof(paging));
            var selection = new ScheduledTaskHistorySelection(environment, host, schedule, Math.Clamp(pageSize, 1, 100), scope, cursor);
            return store.GetRunHistoryAsync(selection, token);
        }

        /// <summary>Returns the last successful API-owned close, never an incomplete position-finalization date.</summary>
        /// <param name="environment">The environment whose completed session is requested.</param>
        /// <param name="token">Cancels the entire history search.</param>
        /// <returns>The completed close date, or null when no successful close is projected.</returns>
        public async ValueTask<DateOnly?> GetCompletedMarketCloseAsync(string environment, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentException.ThrowIfNullOrWhiteSpace(environment);
            var host = ScheduledTaskKeys.ApiMarketCloseHost;
            var schedule = ScheduledTaskIdentities.Catalog(environment, host);
            byte[]? cursor = null;
            do
            {
                var page = await store.GetRunHistoryAsync(environment, host, schedule, 8, cursor, token).ConfigureAwait(false);
                foreach (var run in page.Runs)
                    if (run.GetCompletedMarketCloseDate() is { } completed) return completed;
                cursor = page.PagingState;
            } while (cursor is not null);
            return null;
        }
    }
}
