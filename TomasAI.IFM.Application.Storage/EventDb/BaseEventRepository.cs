using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.EventQueue;
using MathNet.Numerics.Statistics.Mcmc;
using System.Diagnostics.Tracing;

namespace TomasAI.IFM.Application.Storage.EventDb
{

    /// <summary>
    /// aggregate event repository
    /// </summary>
    public abstract class BaseEventRepository 
    {
        readonly IAggregateFactory _aggregateFactory;
        readonly IEventDbContext _dbEventStore;
        readonly IEventDenormalizer _eventDenormalizer;
        readonly ILogger _logger;

        /// <summary>
        /// aggregate event repository constructor
        /// </summary>
        /// <param name="aggregateFactory"></param>
        /// <param name="dbEventStore"></param>
        /// <param name="eventDenormalizer"></param>
        /// <param name="logger"></param>
        public BaseEventRepository(
            IAggregateFactory aggregateFactory,  
            IEventDbContext dbEventStore,
            IEventDenormalizer eventDenormalizer, 
            ILogger logger)
        {
            _aggregateFactory = IsArgumentNull.Set(aggregateFactory);
            _dbEventStore = IsArgumentNull.Set(dbEventStore);
            _eventDenormalizer = IsArgumentNull.Set(eventDenormalizer);
            _logger = IsArgumentNull.Set(logger);
        }

        /// <summary>
        /// return entity id from entity id string value
        /// </summary>
        /// <param name="entityIdValue"></param>
        /// <returns></returns>
        protected async Task<long> GetEntityIdAsync(string entityIdValue)
        {
            try
            {
                return await _dbEventStore.GetEntityIdAsync(entityIdValue);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "EventRepository.GetEntityIdAsync failed");
                throw new StorageException("EventRepository.GetEntityIdAsync failed", ex);
            }
        }

        /// <summary>
        /// load aggregate from event storage
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        protected async Task<IAggregateRoot<TState>> LoadAggregateAsync<TAggregateRoot, TState, TEntity>(BaseCommand<TEntity> command) where TAggregateRoot : IAggregateRoot<TState> where TState : IAggregateState<TState>
        {
            try
            {
                // load domain events from event storage...
                var entityId = await _dbEventStore.GetEntityIdAsync($"{command.EntityId}");
                var domainEvents = await _dbEventStore.LoadEventsAsync<TAggregateRoot>(entityId);

                // return aggregate root entity from aggregate state...
                return _aggregateFactory.CreateAggregateRoot<TState>(entityId, domainEvents);
            }
            catch (Exception ex)
            {
                var errorMsg = $"EventRepository.LoadAggregateAsync failed for {typeof(TAggregateRoot).Name} {typeof(TState).Name}";
                _logger.LogError(ex, errorMsg);
                throw new StorageException(errorMsg, ex);
            }
        }

        /// <summary>
        /// load aggregate from event storage
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        protected async Task<IAggregateRoot<TState>> LoadAggregateAsync<TAggregateRoot, TState>(long entityId) where TAggregateRoot : IAggregateRoot<TState>  where TState: IAggregateState<TState>
        {
            try
            {
                // load domain events from event storage...
                var domainEvents = await _dbEventStore.LoadEventsAsync<TAggregateRoot>(entityId);
                
                // return aggregate root entity from aggregate state...
                return _aggregateFactory.CreateAggregateRoot<TState>(entityId, domainEvents);
            }
            catch (Exception ex)
            {
                var errorMsg = $"EventRepository.LoadAggregateAsync failed for {typeof(TAggregateRoot).Name} {typeof(TState).Name}";
                _logger.LogError(ex, errorMsg);
                throw new StorageException(errorMsg, ex);
            }
        }

        /// <summary>
        /// load aggregate from event storage from last N range of events
        /// </summary>
        /// <param name="entityId"></param>
        /// <param name="lastNRange"></param>
        /// <returns></returns>
        protected async Task<IAggregateRoot<TState>> LoadAggregateAsync<TAggregateRoot, TState, TEvent>(long entityId, int lastNRange) where TAggregateRoot : IAggregateRoot<TState> where TState : IAggregateState<TState> where TEvent : IEvent
        {
            try
            {
                // load domain events from event storage from last N range of events...
                var domainEvents = await _dbEventStore.LoadEventsAsync<TAggregateRoot, TEvent>(entityId, lastNRange);

                // return aggregate root entity from aggregate state...
                return _aggregateFactory.CreateAggregateRoot<TState>(entityId, domainEvents);
            }
            catch (Exception ex)
            {
                var errorMsg = $"EventRepository.LoadAggregateAsync - lastNRange failed for {typeof(TAggregateRoot).Name} {typeof(TState).Name}";
                _logger.LogError(ex, errorMsg);
                throw new StorageException(errorMsg, ex);
            }
        }

        /// <summary>
        /// load aggregate from event storage
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        protected async Task<IAggregateRoot<TState>> LoadEmptyAggregateAsync<TAggregateRoot, TState>(long entityId) where TAggregateRoot : IAggregateRoot<TState> where TState : IAggregateState<TState>
        {
            try
            {
                // no domian events to load...
                var domainEvents = new DomainEventCollection();

                // return aggregate root entity from aggregate state...
                return await Task.FromResult(_aggregateFactory.CreateAggregateRoot<TState>(entityId, domainEvents));
            }
            catch (Exception ex)
            {
                var errorMsg = $"EventRepository.LoadEmptyAggregateAsync failed for {typeof(TAggregateRoot).Name} {typeof(TState).Name}";
                _logger.LogError(ex, errorMsg);
                throw new StorageException(errorMsg, ex);
            }
        }

        /// <summary>
        /// load aggregate from event storage snapshot event
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        protected async Task<IAggregateRoot<TState>> LoadAggregateFromSnapshotAsync<TAggregateRoot, TState, TSnapshotEvent>(long entityId) where TAggregateRoot : IAggregateRoot<TState> where TState : IAggregateState<TState> where TSnapshotEvent : IEvent
        {
            try
            {
                // load domain events from most recent snapshot event storage...
                var domainEvents = await _dbEventStore.LoadEventsAsync<TAggregateRoot, TSnapshotEvent>(entityId);

                // return aggregate root entity from aggregate state...
                return _aggregateFactory.CreateAggregateRoot<TState>(entityId, domainEvents);
            }
            catch (Exception ex)
            {
                var errorMsg = $"EventRepository.LoadAggregateFromSnapshot failed for {typeof(TAggregateRoot).Name} {typeof(TState).Name} {typeof(TSnapshotEvent).Name}";
                _logger.LogError(ex, errorMsg);
                throw new StorageException(errorMsg, ex);
            }
        }

        /// <summary>
        /// save aggregate state changes to event storage
        /// </summary>
        /// <typeparam name="TEntity">entity type</typeparam>
        /// <param name="aggState">aggregate state</param>
        /// <param name="command">command</param>
        protected async Task SaveAggregateAsync<TState, TEntity>(IAggregateState<TState> aggState, BaseCommand<TEntity> command) where TState : IAggregateState<TState>
        {
            try
            {
                // check for any aggregate state change events...
                if ((aggState.Events?.Count ?? 0) > 0)
                {
                    var domainEvents = await _dbEventStore.SaveEventsAsync(aggState.GetType(), aggState.EntityId, aggState.Events, command, eventSource => aggState.UpdateEventSourceVersion(eventSource.EventSourceVersion));
                    if ((domainEvents?.Count ?? 0) > 0) 
                        await _eventDenormalizer?.ExecuteAsync(domainEvents);
                }
            }
            catch(ConcurrencyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMsg = $"EventRepository.SaveAggregateAsync failed for {typeof(TState).Name} {typeof(TEntity).Name}";
                _logger.LogError(ex, errorMsg);
                throw new StorageException(errorMsg, ex);
            }
        }

        /// <summary>
        /// post only aggregate state change events without saving events to event store
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="aggState"></param>
        /// <returns></returns>
        protected async Task PostEventsAsync<TState>(IAggregateState<TState> aggState) where TState : IAggregateState<TState>
        {
            try
            {
                // check for any aggregate state change events...
                if ((aggState.Events?.Count ?? 0) > 0)
                    await _eventDenormalizer?.ExecuteAsync(aggState.Events);
            }
            catch (StorageException ex)
            {
                _logger.LogError(ex, $"EventRepository.PostEventsAsync failed for {typeof(TState).Name}");
                throw;
            }
            catch (Exception ex)
            {
                var errorMsg = $"EventRepository.PostEventsAsync failed for {typeof(TState).Name}";
                _logger.LogError(ex, errorMsg);
                throw new StorageException(errorMsg, ex);
            }
        }

    }
}
