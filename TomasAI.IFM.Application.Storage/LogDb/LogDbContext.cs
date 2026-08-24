using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Telemetry.ViewModels;

namespace TomasAI.IFM.Application.Storage.LogDb;

public class LogDbContext(IDbConnectionSettings connectionSettings,
    IDbContextFactory dbFactory,
    ISequenceIdGenerator sequenceIdGenerator,
    ILogger<DbProvider> logger) : ObjectDataRepository<LogDbContext>(connectionSettings[LogDbConnection], logger), ILogDbContext
{
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);
    readonly ISequenceIdGenerator _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);
    public const string LogDbConnection = "LogDbConnection";

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override LogDbContext Database => this;

    internal static LogEventReadModel MapToLogEvent(IObjectDataRecord o)
        => new(
            timestamp: o.GetDateTime(0),
            logLevel: o.GetString(1),
            message: o.GetString(2),
            serviceId: o.GetString(3));

    /// <summary>
    /// get telemetry logs by date range
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<LogEventReadModel>> GetTelemetryLogsByDateRangeAsync(DateTime startDate, DateTime endDate)
        => await _dbFactory.LogDb
                .Use($"{nameof(LogDbSql)}.{nameof(LogDbSql.GetTelemtryLogsByDateRange)}", LogDbSql.GetTelemtryLogsByDateRange)
                .SetParameters(new GetTelemetryLogsByDateRange(startDate, endDate))
                .ExecuteQueryAsync(MapToLogEvent);

    /// <summary>
    /// insert telemetry log    
    /// </summary>
    /// <param name="logEvent"></param>
    /// <returns></returns>
    public async Task InsertTelemetryLogsAsync(LogEventReadModel[] logEvents)
    {
        var db = _dbFactory.LogDb;
        var queuedCommands = new List<object>(logEvents.Length);
        foreach (var e in logEvents)
        {
            var logId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.TelemetryLog_SequenceId);
            var queuedCommand = db
                .Use($"{nameof(LogDbSql)}.{nameof(LogDbSql.InsertTelemetryLog)}", LogDbSql.InsertTelemetryLog)
                .SetParameters(new InsertTelemetryLog(logId, e.Timestamp, e.LogLevel, e.Message, e.ServiceId))
                .QueueCommand();
            queuedCommands.Add(queuedCommand);
        }
      await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }
}
