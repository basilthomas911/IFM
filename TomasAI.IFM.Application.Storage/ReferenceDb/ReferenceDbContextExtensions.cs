using System.Text.Json;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;


using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;
/// <summary>Owns Reference persistence helpers and typed writes enlisted in a caller-owned PostgreSQL transaction.</summary>
public static class ReferenceDbContextExtensions
{
    extension(ReferenceDbContext context)
    {
        /// <summary>Creates the standalone Reference transaction owner for the current database.</summary>
        /// <returns>The owner of the Reference persistence transaction.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "The C# 14 extension member reads its declared DbContext receiver.")]
        internal TomasAI.IFM.Application.Storage.EventSourceDb.PostgresEventTransaction LookupTransaction() => new(new DbConnectionSettings().Add(TomasAI.IFM.Application.Storage.EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection, context.ConnectionString, "System.Data.Postgres"));
        /// <summary>Checks the technical batch bound for the compatibility maintenance API.</summary>
        internal void ValidatePositiveBatchSize(int batchSize) => ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        /// <summary>Validates a required binding argument before context persistence.</summary>
        /// <param name = "value">The required domain value.</param>
        /// <param name = "argumentName">The original caller argument name.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Validation belongs to the established DbContext binding extension contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the established DbContext binding extension contract.")]
        internal void ValidateRequiredArgument<T>([System.Diagnostics.CodeAnalysis.NotNull] T value, [System.Runtime.CompilerServices.CallerArgumentExpression("value")] string? argumentName = null)
        {
            if (value is null)
                throw new ArgumentNullException(argumentName);
        }

        /// <summary>
        /// Performs the ValidateMdiForwardLossRatioLogicalKeys Reference persistence helper operation.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void ValidateMdiForwardLossRatioLogicalKeys(IEnumerable<MDIForwardLossRatioReadModel> mdiForwardLossRatios)
        {
            var logicalKeys = new HashSet<MdiForwardLossRatioLogicalKey>();
            foreach (var ratio in mdiForwardLossRatios)
            {
                var key = new MdiForwardLossRatioLogicalKey(ratio.TrendDirection.ToStringFast(), ratio.TradeType.ToStringFast(), ratio.MDI);
                if (!logicalKeys.Add(key))
                {
                    throw new ArgumentException($"MDI forward-loss-ratio batch contains duplicate key " + $"({key.TrendDirection}, {key.TradeType}, {key.Mdi}).", nameof(mdiForwardLossRatios));
                }
            }
        }

        /// <summary>Reads definitions from the retained catalog document without executing its requests.</summary>
        internal async Task<IReadOnlyList<TradeStrategyFamilyReadModel>> ReadCatalogDefinitionsAsync(CancellationToken cancellationToken)
        {
            var snapshot = await context.GetTradeStrategyFamilyCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
            if (snapshot is null)
                return[];
            using var json = JsonDocument.Parse(snapshot.Value.Payload);
            return json.RootElement.EnumerateArray().Select(entry => entry.GetProperty("Definition").Deserialize<TradeStrategyFamilyReadModel>() ?? throw new InvalidDataException("Invalid family catalog definition.")).ToArray();
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        /// <summary>Serializes mutations for a lookup name, including empty partitions.</summary>
        private async Task LockLookupAsync(string name, CancellationToken token) => await (transaction.ExecuteAsync(ReferenceDbSql.LockLookup, ["reference.lookup_type:" + name], token))
            .ConfigureAwait(false);
        /// <summary>Upserts the accepted row, preserving its creation metadata during rebuilding.</summary>
        public async Task InsertLookupAsync(LookupTypeReadModel row, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(transaction);
            await (transaction.LockLookupAsync(row.LookupTypeName, token))
            .ConfigureAwait(false);
            await (transaction.ExecuteAsync(ReferenceDbSql.InsertAcceptedLookup, [row.LookupTypeName, row.ShortCode, row.OrderId, row.Description, row.CreatedOn, row.CreatedBy], token))
            .ConfigureAwait(false);
        }

        /// <summary>Atomically replaces the old identifier with the accepted row. Locks names in stable order.</summary>
        public async Task UpdateLookupAsync(LookupTypeId oldId, LookupTypeReadModel row, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(oldId);
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(transaction);
            foreach (var name in new[]
            {
                oldId.LookupTypeName,
                row.LookupTypeName
            }.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                await (transaction.LockLookupAsync(name, token))
            .ConfigureAwait(false);
            await (transaction.ExecuteAsync(ReferenceDbSql.DeleteLookupById, [oldId.LookupTypeName, oldId.OrderId], token))
            .ConfigureAwait(false);
            await (ReferenceDbContextExtensions.InsertLookupAsync(transaction, row, token))
            .ConfigureAwait(false);
        }

        /// <summary>Deletes and renumbers the remaining ordered rows in one transaction, without changing creation metadata.</summary>
        /// <remarks>The source-event checkpoint must prevent repeated application of this positional deletion.</remarks>
        public async Task DeleteLookupAsync(LookupTypeId id, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(transaction);
            await (transaction.LockLookupAsync(id.LookupTypeName, token))
            .ConfigureAwait(false);
            var survivors = await (transaction.QueryAsync(ReferenceDbSql.ReadLookupSurvivors, [id.LookupTypeName, id.OrderId], ReferenceDbContext.MapToLookupReader, token))
            .ConfigureAwait(false);
            await (transaction.ExecuteAsync(ReferenceDbSql.DeleteLookupPartition, [id.LookupTypeName], token))
            .ConfigureAwait(false);
            var ordinal = 0;
            foreach (var row in survivors)
                await (ReferenceDbContextExtensions.InsertLookupAsync(transaction, row with { OrderId = ordinal++ }, token))
            .ConfigureAwait(false);
        }

        internal Task<IReadOnlyList<ScheduledJobReadModel>> ReadScheduledJobsAsync(CancellationToken token) => transaction.QueryAsync(ReferenceDbSql.ReadScheduledJobs + " ORDER BY j.jobId;", [], ReferenceDbContext.MapToScheduledJobJoined, token);
        internal async Task<int> ReadScheduledJobIdAsync(string name, CancellationToken token) => (int? )await transaction.ScalarAsync(ReferenceDbSql.GetScheduledJobId, [name], token)
            .ConfigureAwait(false) ?? 0;
        Task<int> LockScheduledJobAsync(int id, CancellationToken token) => transaction.ExecuteAsync(ReferenceDbSql.LockScheduledJob, ["reference.scheduled_job:" + id.ToString(System.Globalization.CultureInfo.InvariantCulture)], token);
        /// <summary>Inserts one definition and its optional weekdays atomically. Duplicate IDs or exact names are rejected.</summary>
        internal async Task InsertScheduledJobAsync(ScheduledJobReadModel row, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (row.JobId <= 0)
                throw new ArgumentException("A positive job identifier is required.", nameof(row));
            await transaction.LockScheduledJobAsync(row.JobId, token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(ReferenceDbSql.InsertScheduledJob, row.ScheduledJobValues(), token)
            .ConfigureAwait(false);
            await transaction.WriteScheduledJobDaysAsync(row, token)
            .ConfigureAwait(false);
        }

        /// <summary>Updates accepted facts and weekdays atomically. A conflicting rename leaves both jobs unchanged.</summary>
        internal async Task UpdateScheduledJobAsync(ScheduledJobReadModel row, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(row);
            if (row.JobId <= 0)
                throw new ArgumentException("A positive job identifier is required.", nameof(row));
            await transaction.LockScheduledJobAsync(row.JobId, token)
            .ConfigureAwait(false);
            var affected = await transaction.ExecuteAsync(ReferenceDbSql.UpdateScheduledJob, row.ScheduledJobValues(), token)
            .ConfigureAwait(false);
            if (affected != 1)
                throw new StorageException($"Scheduled job {row.JobId} does not exist; update was not submitted.");
            await transaction.WriteScheduledJobDaysAsync(row, token)
            .ConfigureAwait(false);
        }

        /// <summary>Deletes definition and weekdays together; repeated deletion is harmless.</summary>
        internal async Task DeleteScheduledJobAsync(int id, CancellationToken token)
        {
            await transaction.LockScheduledJobAsync(id, token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(ReferenceDbSql.DeleteScheduledJobDays, [id], token)
            .ConfigureAwait(false);
            await transaction.ExecuteAsync(ReferenceDbSql.DeleteScheduledJob, [id], token)
            .ConfigureAwait(false);
        }

        async Task WriteScheduledJobDaysAsync(ScheduledJobReadModel row, CancellationToken token)
        {
            if (row.DaysOfWeek is not { } days)
            {
                await transaction.ExecuteAsync(ReferenceDbSql.DeleteOptionalScheduledJobDays, [row.JobId], token)
            .ConfigureAwait(false);
                return;
            }

            await transaction.ExecuteAsync(ReferenceDbSql.UpsertScheduledJobDays, [row.JobId, days.Monday, days.Tuesday, days.Wednesday, days.Thursday, days.Friday, days.Saturday, days.Sunday], token)
            .ConfigureAwait(false);
        }
    }

    extension(ScheduledJobReadModel row)
    {
        internal object? [] ScheduledJobValues() => [row.JobId, row.JobName, row.JobSchedule.ToString(), row.JobScheduleDate, row.JobScheduleInterval, row.TaskName, row.TaskEnabled, row.CreatedOn, row.CreatedBy, row.UpdatedOn, row.UpdatedBy];
    }
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct MdiForwardLossRatioLogicalKey(string TrendDirection, string TradeType, int Mdi);
