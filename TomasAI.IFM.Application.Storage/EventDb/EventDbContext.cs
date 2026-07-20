using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.SystemAdmin;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.Storage.EventDb
{
    /// <summary>
    /// event database
    /// </summary>
    public class EventDbContext : ObjectDataRepository<EventDbContext>, IEventDbContext
    {
        readonly static SemaphoreSlim _semaphoreSlim = new(1, 1);
        readonly IDbCache _dbCache;
        readonly IDbContextFactory _dbFactory;

        public const string EventDbConnection = "EventDbConnection";
        public const string ERR_EventDbContext_LoadEventsAsync = "EventDbContext: Unable to execute LoadEventsAsync";
        public const string ERR_EventDbContext_SaveEventsAsync = "EventDbContext: Unable to execute SaveEventsAsync";

        /// <summary>
        /// event database constructor
        /// </summary>
        /// <param name="connectionSettings"></param>
        /// <param name="dbFactory"></param>
        /// <param name="dbCache"></param>
        /// <param name="logger"></param>
        public EventDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, IDbCache dbCache, ILogger<EventDbContext> logger)
           : base(connectionSettings[EventDbConnection], logger)
        {
            _dbCache = IsArgumentNull.Set(dbCache);
            _dbFactory = IsArgumentNull.Set(dbFactory);
        }

        /// <summary>
        /// initialize view model mappings
        /// </summary>
        /// <param name="model"></param>
        public override void OnCreateModel(DbModel<EventDbContext> model)
        {
            EventLog = Map(e => e.EventLog)
                .Parameters(e =>
                      e.Set(o => o.EventId)
                       .Set(o => o.EventSourceId)
                       .Set(o => o.EventSourceVersion)
                       .Set(o => o.EventTypeName)
                       .Set(o => o.EventData)
                       .Set(o => o.EventDate)
                  );

            EventSource =  Map(e => e.EventSource)
               .Parameters(e =>
                    e.Set(o => o.EventSourceId)
                     .Set(o => o.EntityId)
                     .Set(o => o.EntityTypeId)
                     .Set(o => o.EventSourceVersion)
                     .Set(o => o.EventSourceDate)
                );

            EventSourceETL = Map(e => e.EventSourceETL)
                .Parameters(e =>
                     e.Set(o => o.EventSourceId)
                      .Set(o => o.EntityTypeName)
                 );

            EventLogETL = Map(e => e.EventLogETL)
                .Parameters(e => 
                    e.Set(o => o.EventId)
                        .Set(o => o.EventTypeName));
        }
       
        public IDbCache DbCache => _dbCache;

        public DbMap<EventLog> EventLog { get; private set; }
        public DbMap<EventSource> EventSource { get; private set; }
        public DbMap<EventSourceETL> EventSourceETL { get; private set; }
        public DbMap<EventLogETL> EventLogETL { get; private set; }

        /// <summary>
        /// stored procedure names
        /// </summary>
        enum StoredProcedure
        {
            spDeleteEventLog,
            spGetEventLog,
            spGetEventLogFromSnapshot,
            spGetEventSource,
            spGetEntityTypeId,
            spGetEventSourceTypeId,
            spGetEventSourceVersion,
            spGetEntityId,
            spGetEventTypeId,
            spInsertEventLog,
            spInsertEventData,
            spInsertEventSource,
            spInsertCommandLog,
            spBackupDatabase,
            spGetEventLogFromLastNRange,
        }

        /// <summary>
        /// delete event log
        /// </summary>
        /// <returns></returns>
        public async Task DeleteEventLogAsync()
        {
            var db = _dbFactory.EventDb;
            await db.Use(EventDbSql.DeleteEventLog)
                .ExecuteCommandAsync();
        }

        /// <summary>
        /// return entityid from entityid string
        /// </summary>
        /// <param name="entityIdValue"></param>
        /// <returns></returns>
        public async Task<long> GetEntityIdAsync(string entityIdValue)
        {
            var db = _dbFactory.EventDb;
            return await db.Use(StoredProcedure.spGetEntityId)
                 .SetParameters(new { entityIdValue })
                 .ExecuteScalarAsync<long>("EntityId");
        }

        /// <summary>
        /// load list of domain events by entity id
        /// </summary>
        /// <typeparam name="TBoundedContext"></typeparam>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public async Task<DomainEventCollection> LoadEventsAsync<TBoundedContext>(long entityId) where TBoundedContext : IAggregateRoot
        {
            var db = _dbFactory.EventDb;
            try
            {
                var entityTypeName = GetEntityTypeName(typeof(TBoundedContext));
                var entityTypeId = this.GetEntityTypeId(entityTypeName, e => GetEntityTypeId(e));
                var events = await db.Use(StoredProcedure.spGetEventLog)
                     .SetParameters(new {
                         entityId,
                         entityTypeId })
                     .ExecuteQueryAsync<EventLog, IEvent>(e => e.ToDomainEvent(e.EventId));
                var eventSourceVersion = await GetEventSourceVersionAsync(entityId, entityTypeId);
                return new DomainEventCollection(eventSourceVersion, events);
            }
            catch (Exception ex)
            {
                throw new StorageException(ERR_EventDbContext_LoadEventsAsync, ex);
            }

            async Task<long> GetEventSourceVersionAsync(long entityId, long entityTypeId)
                => await db.Use(StoredProcedure.spGetEventSourceVersion)
                    .SetParameters(new {
                        entityId,
                        entityTypeId })
                    .ExecuteScalarAsync<long>("EventSourceVersion");

            long GetEntityTypeId(string entityTypeName)
                => db.Use(StoredProcedure.spGetEntityTypeId)
                    .SetParameters(new { entityTypeName })
                    .ExecuteScalarAsync<long>().Result;
        }

        /// <summary>
        /// load list of domain events by entity id from last N range
        /// </summary>
        /// <typeparam name="TBoundedContext"></typeparam>
        /// <typeparam name="TEvent">return only this type of event</typeparam>
        /// <param name="entityId"></param>
        /// <param name="lastNRange"></param>
        /// <returns></returns>
        public async Task<DomainEventCollection> LoadEventsAsync<TBoundedContext, TEvent>(long entityId, int lastNRange) where TBoundedContext : IAggregateRoot where TEvent : IEvent
        {
            var db = _dbFactory.EventDb;
            try
            {
                var entityTypeName = GetEntityTypeName(typeof(TBoundedContext));
                var entityTypeId = this.GetEntityTypeId(entityTypeName, e => GetEntityTypeId(e));
                var eventsName = typeof(TEvent).Name;
                var eventsRange = await db.Use(StoredProcedure.spGetEventLogFromLastNRange)
                     .SetParameters(new {
                         entityId,
                         entityTypeId })
                     .ExecuteQueryAsync<EventLog, IEvent>(e => e.ToDomainEvent(e.EventId), lastNRange * 4);
                var eventSourceVersion = await GetEventSourceVersionAsync(entityId, entityTypeId);
                var events = eventsRange.Where(e => e.GetType().Name == eventsName).Take(lastNRange).OrderBy(e => e.EventId).ToArray();
                return new DomainEventCollection(eventSourceVersion, events);
            }
            catch (Exception ex)
            {
                throw new StorageException(ERR_EventDbContext_LoadEventsAsync, ex);
            }

            async Task<long> GetEventSourceVersionAsync(long entityId, long entityTypeId)
                => await db.Use(StoredProcedure.spGetEventSourceVersion)
                    .SetParameters(new {
                        entityId,
                        entityTypeId })
                    .ExecuteScalarAsync<long>("EventSourceVersion");

            long GetEntityTypeId(string entityTypeName)
                => db.Use( StoredProcedure.spGetEntityTypeId)
                    .SetParameters(new { entityTypeName })
                    .ExecuteScalarAsync<long>().Result;

        }
        /// <summary>
        /// load list of domain events after last snapshot event
        /// </summary>
        /// <typeparam name="TBoundedContext"></typeparam>
        /// <typeparam name="TSnapshot"></typeparam>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public async Task<DomainEventCollection> LoadEventsAsync<TBoundedContext, TSnapshot>(long entityId) where TBoundedContext : IAggregateRoot where TSnapshot: IEvent
        {
            var db = _dbFactory.EventDb;
            try
            {
                var entityTypeName = GetEntityTypeName(typeof(TBoundedContext));
                var entityTypeId = this.GetEntityTypeId(entityTypeName, e => GetEntityTypeId(e));
                var eventSourceVersion = await GetEventSourceVersionAsync(entityId, entityTypeId);
                var snapshotEventTypeId = GetEventTypeId(typeof(TSnapshot).AssemblyQualifiedName);
                var events = (await db
                     .Use(StoredProcedure.spGetEventLogFromSnapshot)
                     .SetParameters(new {
                         entityId,
                         entityTypeId,
                         snapshotEventTypeId })
                     .ExecuteQueryAsync<EventLog, IEvent>(e => e.ToDomainEvent(e.EventId)));
                return new DomainEventCollection(eventSourceVersion, events);
            }
            catch (Exception ex)
            {
                throw new StorageException(ERR_EventDbContext_LoadEventsAsync, ex);
            }

            async Task<long> GetEventSourceVersionAsync(long entityId, long entityTypeId)
                => await db.Use(StoredProcedure.spGetEventSourceVersion)
                    .SetParameters(new {
                        entityId,
                        entityTypeId })
                    .ExecuteScalarAsync<long>("EventSourceVersion");

            long GetEntityTypeId(string entityTypeName)
                 => db.Use(StoredProcedure.spGetEntityTypeId)
                     .SetParameters(new { entityTypeName })
                     .ExecuteScalarAsync<long>().Result;
        }

        /// <summary>
        /// save domain events
        /// </summary>
        /// <param name="aggStateType"></param>
        /// <param name="entityId"></param>
        /// <param name="domainEvents"></param>
        /// <param name="command"></param>
        /// <param name="eventSourceAction"></param>
        public async Task<DomainEventCollection> SaveEventsAsync(Type aggStateType, long entityId, DomainEventCollection domainEvents, ICommand command , Action<EventSource> eventSourceAction = null)
        {
            DomainEventCollection savedEvents;
            var db = _dbFactory.EventDb;
            var tx = db.BeginTransaction();
            try
            {
                // update event source to next version...
                var eventDate = DateTime.Now;
                var eventSource = await UpdateEventSourceToNextVersionAsync(eventDate);

                // write events to event log...
                savedEvents = await WriteEventsToEventLogAsync(eventDate, eventSource, command.CommandId);
                tx.Commit();
            }
            catch (ConcurrencyException)
            {
                tx.Rollback();
                throw;
            }
            catch (StorageException)
            {
                tx.Rollback();
                throw;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                throw new StorageException(ERR_EventDbContext_SaveEventsAsync, ex);
            }
            return savedEvents;

            async Task<EventSource> UpdateEventSourceToNextVersionAsync(DateTime eventDate)
            {
                EventSource eventSource;
                await _semaphoreSlim.WaitAsync();
                try
                {
                    // check if next event version is valid...
                    var entityTypeName = GetEntityTypeName(aggStateType);
                    var entityTypeId = this.GetEntityTypeId(entityTypeName, e => GetEntityTypeId(e));
                    eventSource = await GetEventSourceAsync(entityTypeId);
                    if (eventSource is null)
                    {
                        await UpdateEventSourceVersionAsync(new EventSource(
                            eventSourceId: 0,
                            entityId: entityId,
                            entityTypeId: entityTypeId,
                            eventSourceVersion: await GetEventSourceVersionAsync(entityId, entityTypeId),
                            eventSourceDate: eventDate
                        ));
                        eventSource = await GetEventSourceAsync(entityTypeId);
                    }
                    else if (eventSource.EventSourceVersion < domainEvents.EventSourceVersion)
                        throw new ConcurrencyException($"ConcurrencyException: Unable to save events from aggregate {aggStateType.FullName}");

                    // update event source with next event source version...
                    await UpdateEventSourceVersionAsync(eventSource.IncrementEventSourceVersion(eventDate));
                    eventSourceAction?.Invoke(eventSource);
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
                return eventSource;
            }

            async Task<EventSource> GetEventSourceAsync(long entityTypeId)
               => await db.Use(StoredProcedure.spGetEventSource)
                      .SetParameters(new {
                          entityId,
                          entityTypeId })
                      .ExecuteSingleAsync<EventSource>();

            async Task UpdateEventSourceVersionAsync(EventSource e)
                => await db.Use(StoredProcedure.spInsertEventSource)
                    .SetParameters(new {
                        entityId = e.EntityId,
                        entityTypeId = e.EntityTypeId,
                        eventSourceVersion = e.EventSourceVersion,
                        eventSourceDate = e.EventSourceDate })
                    .ExecuteCommandAsync();

            async Task<long> GetEventSourceVersionAsync(long entityId, long entityTypeId)
                => await db.Use(StoredProcedure.spGetEventSourceVersion)
                     .SetParameters(new {
                         entityId,
                         entityTypeId })
                     .ExecuteScalarAsync<long>("EventSourceVersion");

            async Task <DomainEventCollection> WriteEventsToEventLogAsync(DateTime eventDate, EventSource eventSource, Guid commandId)
            {
                var eventIds = await db.Use(StoredProcedure.spInsertEventLog)
                     .SetParameters(domainEvents.Select(e => new
                     {
                         eventSource.EventSourceId,
                         eventSource.EventSourceVersion,
                         eventTypeId = GetEventTypeId(e.GetType().AssemblyQualifiedName),
                         eventData = e.ToEventData(),
                         eventDate,
                         commandId = $"{commandId}"
                     }))
                     .ExecuteCommandAsync();
                var savedEvents = new DomainEventCollection();
                for (var i = 0; i < domainEvents.Count; i++)
                {
                    domainEvents[i].EventId = eventIds[i];
                    savedEvents.Add(domainEvents[i]);
                }
                return savedEvents;
            }

            long GetEntityTypeId(string entityTypeName)
                 => db.Use(StoredProcedure.spGetEntityTypeId)
                     .SetParameters(new { entityTypeName })
                     .ExecuteScalarAsync<long>().Result;
        }

        public string GetEntityTypeName(Type entityType) => entityType.Name.Replace("State", "");

        /// <summary>
        /// return entity type id
        /// </summary>
        /// <param name="entityTypeName"></param>
        /// <returns></returns>
        public long GetEntityTypeId(string entityTypeName, Func<string, long> onCacheMiss)
            => _dbCache.Get(e => e.EntityTypeIdMap, entityTypeName, () => onCacheMiss(entityTypeName));

        /// <summary>
        /// backup database
        /// </summary>
        /// <param name="domainDataStorageUrl"></param>
        /// <param name="backupType"></param>
        /// <returns></returns>
        public async Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage)
        {
            var db = _dbFactory.EventDb;
            await db .Use(StoredProcedure.spBackupDatabase)
                .SetParameters(new { backupType = $"{backupType}" })
                .WithNoTransaction()
                .SetCommandTimeout(commandTimeout)
                .ExecuteCommandAsync(onInfoMessage);
        }

        public async Task InsertEventData(long eventId, string eventData)
        {
            var db = _dbFactory.EventDb;
            await  db .Use(StoredProcedure.spInsertEventData)
                .SetParameters(new {
                    eventId,
                    eventData })
                .ExecuteCommandAsync();
        }

        public async Task UpdateEventSourceEntityTypeIdAsync()
        {
            var db = _dbFactory.EventDb;
            var eventSources = await db.Use("select * from event_source").ExecuteQueryAsync<EventSourceETL>();
            var queuedCommands = new List<object>();    
            foreach (var e in eventSources)
            {
                var entityTypeId = GetEntityTypeId(e.EntityTypeName, e => 100L);
                queuedCommands.Add( db.Use($"update event_source set EntityTypeId = {entityTypeId} where EventSourceId = {e.EventSourceId}").QueueCommand());
            }
            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }

        public async Task UpdateEventLogEventTypeIdAsync()
        {
            var db = _dbFactory.EventDb;
            var startDate = (await db.Use("select min(EventDate) from event_log").ExecuteScalarAsync<DateTime>()).Date;
            var endDate = (await db.Use("select max(EventDate) from event_log").ExecuteScalarAsync<DateTime>()).Date;
            var eventTypeIdMap = new Dictionary<string, long>();
            foreach(var runDate in GetRunDates())
            {
                  var minDate = runDate.Date;
                var maxDate = minDate.Date.AddDays(1);
                var dateRangeQry = $"select * from event_log where EventDate between '{minDate:yyyy-MM-dd}' and '{maxDate:yyyy-MM-dd}'";
                var eventLogs = db.Use(dateRangeQry).ExecuteQueryAsync<EventLogETL>().Result;
                if (eventLogs != null && eventLogs.Count > 0)
                {
                    var queuedCommands = new List<object>();    
                    foreach (var e in eventLogs)
                    {
                        if (!eventTypeIdMap.ContainsKey(e.EventTypeName))
                            eventTypeIdMap.Add(e.EventTypeName, GetEventTypeId(e.EventTypeName));
                        var eventTypeId = eventTypeIdMap[e.EventTypeName];
                        queuedCommands.Add( db.Use($"update event_log2 set EventTypeId = {eventTypeId} where EventId = {e.EventId}").QueueCommand());
                    }
                    db.ExecuteQueuedCommandsAsync(queuedCommands).Wait();
                }
            }
            return;

            IEnumerable<DateTime> GetRunDates()
            {
                var valueDate = startDate;
                while (valueDate <= endDate)
                {
                    yield return valueDate;
                    valueDate = valueDate.AddDays(1);
                }
            }
        }

        
        /// <summary>
        /// return event type id
        /// </summary>
        /// <param name="eventTypeName"></param>
        /// <returns></returns>
        public long GetEventTypeId(string eventTypeName)
            => _dbCache.Get(e => e.EventTypeIdMap, eventTypeName,  
                () => _dbFactory.EventDb
                  .Use(StoredProcedure.spGetEventTypeId)
                  .SetParameters(new { eventTypeName })
                  .ExecuteScalarAsync<long>().Result);

    }

    /*
    public static class IDomainEventExtension
    {
        public static string ToEventData(this IEvent domainEvent) => JsonConvert.SerializeObject(domainEvent, Formatting.Indented);
    }
    */
    public static class ICommandExtension
    {
        public static string ToCommandData(this ICommand command) => JsonConvert.SerializeObject(command, Formatting.Indented);
    }

    public class EventSourceETL
    {

        public long EventSourceId { get; }
        public string EntityTypeName { get; }

        public EventSourceETL(long eventSourceId, string entityTypeName)
        {
            EventSourceId = eventSourceId;
            EntityTypeName = entityTypeName;
        }

    }

    public class EventLogETL
    {

        public long EventId { get; }
        public string EventTypeName { get; }

        public EventLogETL(long eventId, string eventTypeName)
        {
            EventId = eventId;
            EventTypeName = eventTypeName;
        }

    }

}
