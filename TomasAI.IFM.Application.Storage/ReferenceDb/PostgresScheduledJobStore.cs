using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.JobScheduler;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Native scheduled-job persistence. PostgreSQL constraints replace cross-table Scylla reservations.</summary>
/// <remarks>Mutations accept completed facts and can enlist in the source-event transaction. No sequence allocation,
/// external scheduling, timestamp generation or notifications occur inside this store.</remarks>
internal static class PostgresScheduledJobStore
{
    const string Read = """
        SELECT j.jobId,j.jobName,j.jobSchedule,j.jobScheduleDate,j.jobScheduleInterval,j.taskName,j.taskEnabled,
        j.createdOn,j.createdBy,j.updatedOn,j.updatedBy,d.jobId,d.monday,d.tuesday,d.wednesday,d.thursday,d.friday,d.saturday,d.sunday
        FROM reference.scheduled_job j LEFT JOIN reference.scheduled_job_days d ON d.jobId=j.jobId
        """;

    internal static Task<IReadOnlyList<ScheduledJobReadModel>> ReadAsync(IEnlistedPostgresTransaction transaction, CancellationToken token)
        => transaction.QueryAsync(Read + " ORDER BY j.jobId;", [], Map, token);

    internal static async Task<int> GetIdAsync(IEnlistedPostgresTransaction transaction, string name, CancellationToken token)
        => (int?)await transaction.ScalarAsync("SELECT jobId FROM reference.scheduled_job WHERE jobName COLLATE \"C\"=$1;", [name], token).ConfigureAwait(false) ?? 0;

    static ScheduledJobReadModel Map(NpgsqlDataReader reader)
    {
        var row = new ScheduledJobReadModel(reader.GetInt32(0),reader.GetString(1),Enum.Parse<JobScheduleType>(reader.GetString(2)),
            reader.GetDateTime(3),reader.GetDouble(4),reader.GetString(5),reader.GetBoolean(6),reader.GetDateTime(7),reader.GetString(8),
            reader.GetDateTime(9),reader.GetString(10));
        if (!reader.IsDBNull(11))
            row.DaysOfWeek = new ScheduledJobDaysOfWeekReadModel(row.JobId,reader.GetBoolean(12),reader.GetBoolean(13),
                reader.GetBoolean(14),reader.GetBoolean(15),reader.GetBoolean(16),reader.GetBoolean(17),reader.GetBoolean(18));
        return row;
    }

    static Task<int> LockAsync(IEnlistedPostgresTransaction transaction, int id, CancellationToken token)
        => transaction.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended($1,0));", ["reference.scheduled_job:" + id.ToString(System.Globalization.CultureInfo.InvariantCulture)], token);

    /// <summary>Inserts one definition and its optional weekdays atomically. Duplicate IDs or exact names are rejected.</summary>
    internal static async Task InsertAsync(IEnlistedPostgresTransaction transaction, ScheduledJobReadModel row, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.JobId <= 0) throw new ArgumentException("A positive job identifier is required.", nameof(row));
        await LockAsync(transaction,row.JobId,token).ConfigureAwait(false);
        await transaction.ExecuteAsync("""
            INSERT INTO reference.scheduled_job(jobId,jobName,jobSchedule,jobScheduleDate,jobScheduleInterval,taskName,taskEnabled,createdOn,createdBy,updatedOn,updatedBy)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11);
            """, Values(row), token).ConfigureAwait(false);
        await WriteDaysAsync(transaction,row,token).ConfigureAwait(false);
    }

    /// <summary>Updates accepted facts and weekdays atomically. A conflicting rename leaves both jobs unchanged.</summary>
    internal static async Task UpdateAsync(IEnlistedPostgresTransaction transaction, ScheduledJobReadModel row, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.JobId <= 0) throw new ArgumentException("A positive job identifier is required.", nameof(row));
        await LockAsync(transaction,row.JobId,token).ConfigureAwait(false);
        var affected = await transaction.ExecuteAsync("""
            UPDATE reference.scheduled_job SET jobName=$2,jobSchedule=$3,jobScheduleDate=$4,jobScheduleInterval=$5,taskName=$6,
            taskEnabled=$7,createdOn=$8,createdBy=$9,updatedOn=$10,updatedBy=$11 WHERE jobId=$1;
            """, Values(row), token).ConfigureAwait(false);
        if (affected != 1) throw new StorageException($"Scheduled job {row.JobId} does not exist; update was not submitted.");
        await WriteDaysAsync(transaction,row,token).ConfigureAwait(false);
    }

    /// <summary>Deletes definition and weekdays together; repeated deletion is harmless.</summary>
    internal static async Task DeleteAsync(IEnlistedPostgresTransaction transaction, int id, CancellationToken token)
    {
        await LockAsync(transaction,id,token).ConfigureAwait(false);
        await transaction.ExecuteAsync("DELETE FROM reference.scheduled_job_days WHERE jobId=$1;", [id], token).ConfigureAwait(false);
        await transaction.ExecuteAsync("DELETE FROM reference.scheduled_job WHERE jobId=$1;", [id], token).ConfigureAwait(false);
    }

    static object?[] Values(ScheduledJobReadModel row) => [row.JobId,row.JobName,row.JobSchedule.ToString(),row.JobScheduleDate,
        row.JobScheduleInterval,row.TaskName,row.TaskEnabled,row.CreatedOn,row.CreatedBy,row.UpdatedOn,row.UpdatedBy];

    static async Task WriteDaysAsync(IEnlistedPostgresTransaction transaction, ScheduledJobReadModel row, CancellationToken token)
    {
        if (row.DaysOfWeek is not { } days)
        {
            await transaction.ExecuteAsync("DELETE FROM reference.scheduled_job_days WHERE jobId=$1;", [row.JobId], token).ConfigureAwait(false);
            return;
        }
        await transaction.ExecuteAsync("""
            INSERT INTO reference.scheduled_job_days(jobId,monday,tuesday,wednesday,thursday,friday,saturday,sunday)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8) ON CONFLICT(jobId) DO UPDATE SET monday=EXCLUDED.monday,tuesday=EXCLUDED.tuesday,
            wednesday=EXCLUDED.wednesday,thursday=EXCLUDED.thursday,friday=EXCLUDED.friday,saturday=EXCLUDED.saturday,sunday=EXCLUDED.sunday;
            """, [row.JobId,days.Monday,days.Tuesday,days.Wednesday,days.Thursday,days.Friday,days.Saturday,days.Sunday], token).ConfigureAwait(false);
    }
}
