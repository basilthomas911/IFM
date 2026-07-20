using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Telemetry.ViewModels;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.Storage.LogDb
{
    public class LogDbContext : ObjectDataRepository<LogDbContext>, ILogDbContext
    {
        public const string LogDbConnection = "LogDbConnection";
        readonly IDbContextFactory _dbFactory;

        /// <summary>
        /// log database constructor
        /// </summary>
        /// <param name="connectionSettings"></param>
        /// <param name="dbFactory"></param>
        public LogDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<LogDbContext> logger) 
            : base(connectionSettings[LogDbConnection], logger)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <summary>
        /// initialize view model mappings
        /// </summary>
        /// <param name="model"></param>
        public override void OnCreateModel(DbModel<LogDbContext> model)
        {
            CommandLog = model.Map(e => e.CommandLog)
              .Parameters(e =>
                  e.Set(o => o.CommandId)
                   .Set(o => o.CommandDate)
                   .Set(o => o.CommandName)
                   .Set(o => o.AggrgeateId)
                   .Set(o => o.RouteTo)
                   .Set(o => o.CommandData)
                   .Set(o => o.UserName)
                   .Set(o => o.ErrorMessage)
                   .Set(o => o.ErrorCode)
                   .Set(o => o.ErrorType, o => o.AsEnum<ErrorType>())
                   .Set(o => o.ErrorData)
              );

            QueryLog = model.Map(e => e.QueryLog)
                 .Parameters(e =>
                     e.Set(o => o.CommandId)
                      .Set(o => o.QueryDate)
                      .Set(o => o.QueryName)
                      .Set(o => o.QueryData)
                      .Set(o => o.UserName)
                      .Set(o => o.ErrorMessage)
                      .Set(o => o.ErrorCode)
                      .Set(o => o.ErrorType, o => o.AsEnum<ErrorType>())
                      .Set(o => o.ErrorData)
                 );

            EventServiceLog = model.Map(e => e.EventServiceLog)
              .Parameters(e =>
                  e.Set(o => o.CommandId)
                   .Set(o => o.EventDate)
                   .Set(o => o.EventName)
                   .Set(o => o.EventData)
                   .Set(o => o.UserName)
                   .Set(o => o.ErrorMessage)
                   .Set(o => o.ErrorCode)
                   .Set(o => o.ErrorType, o => o.AsEnum<ErrorType>())
                   .Set(o => o.ErrorData)
              );

            EventQueueLog = model.Map(e => e.EventQueueLog)
              .Parameters(e =>
                  e.Set(o => o.EventQueueId)
                   .Set(o => o.EventId)
                   .Set(o => o.EventTypeName)
                   .Set(o => o.EventQueueStatus, o => o.AsEnum<EventQueueStatus>())
                   .Set(o => o.EventQueueDate)
                   .Set(o => o.EventFailedMessage)
              );

            LogEvent = model.Map(e => e.LogEvent)
              .Parameters(e =>
                  e.Set(o => o.Timestamp)
                   .Set(o => o.LogLevel)
                   .Set(o => o.Message)
                   .Set(o => o.ServiceId)
              );

        }

        public DbMap<CommandLogReadModel> CommandLog { get; private set; }
        public DbMap<QueryLogViewModel> QueryLog { get; private set; }
        public DbMap<EventServiceLogViewModel> EventServiceLog { get; private set; }
        public DbMap<EventQueueLog> EventQueueLog { get; private set; }
        public DbMap<LogEventReadModel> LogEvent { get; private set; }

        /// <summary>
        /// stored procedure names
        /// </summary>
        public enum StoredProcedure
        {
            spBackupDatabase,
            spInsertEventQueueLog,
            spInsertCommandLog,
            spInsertQueryLog,
            spInsertEventServiceLog,
            spInsertDenormalizerLog,
            spInsertLogEvent,
            spDeleteCommandLog
        }

        /// <summary>
        /// insert event queue
        /// </summary>
        /// <param name="eventId"></param>
        /// <param name="eventTypeName"></param>
        /// <param name="eventQueueStatus"></param>
        /// <param name="eventQueueDate"></param>
        /// <param name="eventFailedMessage"></param>
        /// <returns></returns>
        public async Task InsertEventQueueLogAsync(
            long eventId,
            string eventTypeName,
            EventQueueStatus eventQueueStatus,
            DateTime eventQueueDate,
            string eventFailedMessage = null)
        {
            var db = _dbFactory.LogDb;
            await db.Use(StoredProcedure.spInsertEventQueueLog)
                .SetParameters(new {
                    eventId,
                    eventTypeName,
                    eventQueueStatus = $"{eventQueueStatus}",
                    eventQueueDate,
                    eventFailedMessage = eventFailedMessage ?? string.Empty })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert command log
        /// </summary>
        /// <param name="e">CommandLogReadModel</param>
        /// <returns></returns>
        public async Task InsertCommandLogAsync(CommandLogReadModel e)
        {
            var db = _dbFactory.LogDb;
            await db.Use(StoredProcedure.spInsertCommandLog)
                .SetParameters(new {
                    commandId = e.CommandId,
                    commandDate = e.CommandDate,
                    commandName = e.CommandName,
                    aggregateId = e.AggrgeateId,
                    routeTo = e.RouteTo,
                    commandData = e.CommandData ?? string.Empty,
                    userName = e.UserName,
                    errorMessage = e.ErrorMessage,
                    errorCode = e.ErrorCode,
                    errorType = $"{e.ErrorType}",
                    errorData = e.ErrorData ?? string.Empty })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// delete command log
        /// </summary>
        /// <param name="commandId"></param>
        /// <returns></returns>
        public async Task DeleteCommandLogAsync(string commandId)
        {
            var db = _dbFactory.LogDb;
            await db.Use(StoredProcedure.spInsertCommandLog)
                .SetParameters(new { commandId })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert query log
        /// </summary>
        /// <param name="e">QueryLogViewModel</param>
        /// <returns></returns>
        public async Task InsertQueryLogAsync(QueryLogViewModel e)
        {
            var db = _dbFactory.LogDb;
            await db.Use(StoredProcedure.spInsertQueryLog)
                .SetParameters(new {
                    commandId = e.CommandId,
                    queryDate = e.QueryDate,
                    queryName = e.QueryName,
                    queryData = e.QueryData ?? string.Empty,
                    userName = e.UserName,
                    errorMessage = e.ErrorMessage,
                    errorCode = e.ErrorCode,
                    errorType = $"{e.ErrorType}",
                    errorData = e.ErrorData ?? string.Empty })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert event service log
        /// </summary>
        /// <param name="e">EventServiceLogViewModel</param>
        /// <returns></returns>
        public async Task InsertEventServiceLogAsync(EventServiceLogViewModel e)
        {
            var db = _dbFactory.LogDb;
            await db.Use(StoredProcedure.spInsertEventServiceLog)
                .SetParameters(new {
                    commandId = e.CommandId,
                    eventDate = e.EventDate,
                    eventName = e.EventName,
                    eventData = e.EventData ?? string.Empty,
                    userName = e.UserName,
                    errorMessage = e.ErrorMessage,
                    errorCode = e.ErrorCode,
                    errorType = $"{e.ErrorType}",
                    errorData = e.ErrorData ?? string.Empty })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert denormalizer log
        /// </summary>
        /// <param name="e">DenormalizerLogViewModel</param>
        /// <returns></returns>
        public async Task InsertDenormalizerLogAsync(DenormalizerLogViewModel e)
        {
            var db = _dbFactory.LogDb;
            await db.Use(StoredProcedure.spInsertDenormalizerLog)
                .SetParameters(new {
                    commandId = e.CommandId,
                    denormalizerDate = e.DenormalizerDate,
                    denormalizerName = e.DenormalizerName,
                    denormalizerData = e.DenormalizerData ?? string.Empty,
                    userName = e.UserName,
                    errorMessage = e.ErrorMessage,
                    errorCode = e.ErrorCode,
                    errorType = $"{e.ErrorType}",
                    errorData = e.ErrorData ?? string.Empty })
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// backup log database
        /// </summary>
        /// <param name="backupType"></param>
        /// <param name="commandTimeout"></param>
        /// <param name="onInfoMessage"></param>
        /// <returns></returns>
        public async Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage)
        {
            var db = _dbFactory.LogDb;
            await db.Use(StoredProcedure.spBackupDatabase)
                .SetParameters(new { backupType = $"{backupType}" })
                .WithNoTransaction()
                .SetCommandTimeout(commandTimeout)
                .ExecuteCommandAsync(onInfoMessage);
        }

        /// <summary>
        /// save all log events
        /// </summary>
        /// <param name="logEvents"></param>
        /// <returns></returns>
        public async Task InsertLogEventsAsync(LogEventReadModel[] logEvents)
        {
            if (logEvents is null || logEvents.Length == 0)
                return;

            var queuedCommands = new List<object>();
            var db = _dbFactory.LogDb;
            foreach (var e in logEvents)
                queuedCommands.Add(db.Use(StoredProcedure.spInsertLogEvent)
                .SetParameters(new {
                    timestamp = e.Timestamp,
                    logLevel = e.LogLevel,
                    message = e.Message,
                    serviceId = e.ServiceId
                })
                .QueueCommand());
            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }
    }

}
