using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>
/// Builder class for constructing and executing an event projector with specified event processing actions.
/// </summary>
/// <param name="eventProjector"></param>
public class EventProjectorBuilder(IEventProjector eventProjector)
{
    readonly IEventProjector _eventProjector = IsArgumentNull.Set(eventProjector);
    Func<IEvent, ValueTask> _processingEventAction = default;
    Func<IEvent, Task<ServiceResult>> _processingAction = default;
    Func<IEvent, ValueTask> _completedEventAction = default;
    Func<IEvent, string, ValueTask> _failedEventAction = default;

    /// <summary>
    /// Executes the event projector with the specified denormalize event and denormalizer action.
    /// </summary> 
    public async ValueTask<bool> RunAsync<TEvent, TComplete, TFail, TEntityId>(TEvent denormalizeEvent, Func<TEvent, Task> denormalizerAction, bool postDenormalizeEvent = true)
        where TEvent : class, IEvent<TEntityId>
        where TComplete : class, ICompleteEvent<TEntityId>
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        _eventProjector.DurableReplayQueue.SetMaxAttemptsReachedAction(_eventProjector.DurableReplayQueueName, e =>
        {
            var currentState = _eventProjector.BlackboardService.EventProjectorState.Get(e.EventId);
            currentState = currentState with
            {
                Outcome = EventProjectorOutcomeType.Retrying,
                ErrorMessage = $"Max {_eventProjector.DurableReplayQueue.GetMaxReplayAttemps(_eventProjector.DurableReplayQueueName)} attempts reached for event {e.EventId} of type {e.GetType().Name}"
            };
            _eventProjector.BlackboardService.EventProjectorState.Set(e.EventId, currentState);
            return _eventProjector.DbEventSource.InsertEventProjectorStateAsync(currentState);
        });

        SetProjectionProcessingEvent<TEvent, TEntityId>(e =>
        {
            e.CheckForEmptyCommandId();
            if (postDenormalizeEvent)
            {
                EventInitHelper.SetProperty(e, nameof(IEvent.Subject), new ActorSubject(ActorType.Event, e.Subject.Name
                    , e.Subject.Verb, e.EntityId.Format()));
                return _eventProjector.Context.SendAsync<TEvent, TEntityId>(e);
            }
            return ValueTask.CompletedTask;
        });

        SetProjectionProcessingAction<TEvent, TEntityId>(e => denormalizerAction(e));

        SetProjectionCompletedEventAction<TEntityId, TComplete>(e => 
        {
            var denormalizerEvent = e as IEvent<TEntityId>;
            var completedEvent = denormalizerEvent.ToCompleteEvent<TComplete, TEntityId>() as TComplete;
            if (completedEvent is not null)
            {
                return _eventProjector.Context.SendAsync<TComplete, TEntityId>(completedEvent);
            }
            return ValueTask.CompletedTask;
        });
        
        SetProjectionFailedEventAction<TEntityId, TFail>((e, errorMessage) => 
        {
            var denormalizerEvent = e as IEvent<TEntityId>;
            var failedEvent = denormalizerEvent.ToFailEvent<TFail, TEntityId>(new Exception(errorMessage)) as TFail;
            return (failedEvent is not null)
                ? _eventProjector.Context.SendAsync<TFail, TEntityId>(failedEvent)
                : ValueTask.CompletedTask;
        });

        await ExecuteAsync<TEvent, TEntityId>(denormalizeEvent);
        return true;
    }

    /// <summary>
    /// Executes the event projector with the specified domain event.
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <typeparam name="TEntityId"></typeparam>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    async ValueTask ExecuteAsync<TEvent, TEntityId>(TEvent domainEvent)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        // load current projector state...
        var currentState = _eventProjector.BlackboardService.EventProjectorState.Get(domainEvent.EventId);
        try
        {
            while (currentState.Stage != EventProjectorStageType.Completed)
            { 
                _ = currentState.Stage switch
                {
                    EventProjectorStageType.PublishProcessingEvent => await PublishProcessingEventAsync(),
                    EventProjectorStageType.ApplyProjection => await ApplyProjectionAsync(),
                    EventProjectorStageType.PublishCompletedEvent => await PublishCompletedEventAsync(),
                    EventProjectorStageType.PublishFailedEvent => await PublishFailedEventAsync(),
                    _ => throw new InvalidOperationException($"Invalid stage {currentState.Stage} for event {domainEvent.EventId}")
                };
            }
        }
        catch (Exception ex)
        {
            currentState = currentState with {
                Outcome = EventProjectorOutcomeType.Retrying,
                ErrorMessage = ex.Message };
            _eventProjector.BlackboardService.EventProjectorState.Set(domainEvent.EventId, currentState);
            await _eventProjector.DbEventSource.InsertEventProjectorStateAsync(currentState);
        }

        /// <summary>
        /// Publishes the processing event for the specified domain event.
        /// </summary>
        /// <returns></returns> 
        async ValueTask<EventProjectorStageType> PublishProcessingEventAsync()
        {
            await _processingEventAction(domainEvent);
            currentState = currentState with { Stage = EventProjectorStageType.ApplyProjection };
            _eventProjector.BlackboardService.EventProjectorState.Set(domainEvent.EventId, currentState);
            return currentState.Stage;
        }

        async ValueTask<EventProjectorStageType> ApplyProjectionAsync()
        {
            var serviceResult = await _processingAction(domainEvent);
            if (serviceResult.Success)
            {
                currentState = currentState with { Stage = EventProjectorStageType.PublishCompletedEvent , Outcome = EventProjectorOutcomeType.Processing };
                _eventProjector.BlackboardService.EventProjectorState.Set(domainEvent.EventId, currentState);
            }
            else
            {
                currentState = currentState with { Stage = EventProjectorStageType.PublishFailedEvent, Outcome = EventProjectorOutcomeType.Retrying, ErrorMessage = serviceResult.ErrorMessage };
                _eventProjector.BlackboardService.EventProjectorState.Set(domainEvent.EventId, currentState);
            }
            return currentState.Stage;
        }

        async ValueTask<EventProjectorStageType>  PublishCompletedEventAsync()
        {
            await _completedEventAction(domainEvent);
            currentState = currentState with { 
                Stage = EventProjectorStageType.Completed,
                Outcome = EventProjectorOutcomeType.Completed
            };
            _eventProjector.BlackboardService.EventProjectorState.Set(domainEvent.EventId, currentState);
            await _eventProjector.DbEventSource.InsertEventProjectorStateAsync(currentState);
            return currentState.Stage;
        }

        async ValueTask<EventProjectorStageType> PublishFailedEventAsync()
        {
            await _failedEventAction(domainEvent, currentState.ErrorMessage);
            currentState = currentState with
            {
                Stage = EventProjectorStageType.Completed,
                Outcome = EventProjectorOutcomeType.Failed 
            };
            _eventProjector.BlackboardService.EventProjectorState.Set(domainEvent.EventId, currentState);
            await _eventProjector.DbEventSource.InsertEventProjectorStateAsync(currentState);
            return currentState.Stage;
        }
    }

    /// <summary>
    /// Sets the projection processing event action for the specified event type and entity ID type.
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <typeparam name="TEntityId"></typeparam>
    /// <param name="processingEventAction"></param>
    void SetProjectionProcessingEvent<TEvent, TEntityId>(Func<TEvent, ValueTask> processingEventAction, bool overwrite = false)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (overwrite)
        {
            IsArgumentNull.Check(processingEventAction);
            _processingEventAction = e => processingEventAction(e as TEvent);
        }
        else if (_processingEventAction is null)
        {
            IsArgumentNull.Check(processingEventAction);
            _processingEventAction = e => processingEventAction(e as TEvent);
        }
    }

    /// <summary>
    /// Sets the projection processing action for the specified event type and entity ID type.
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <typeparam name="TEntityId"></typeparam>
    /// <param name="processingAction"></param>
    void SetProjectionProcessingAction<TEvent, TEntityId>(Func<TEvent, Task> processingAction, bool overwrite = false)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (overwrite)
        {
            IsArgumentNull.Check(processingAction);
            _processingAction = async e =>
            {
                var domainEvent = e as TEvent;
                try
                {
                    await processingAction(domainEvent);
                    return new ServiceResult(true, 0, string.Empty);
                }
                catch (Exception ex)
                {
                    return new ServiceResult(false, 9999, ex.Message);
                }
            };
        }
        else if (_processingAction is null)
        {
            IsArgumentNull.Check(processingAction);
            _processingAction = async e =>
            {
                var domainEvent = e as TEvent;
                try
                {
                    await processingAction(domainEvent);
                    return new ServiceResult(true, 0, string.Empty);
                }
                catch (Exception ex)
                {
                    return new ServiceResult(false, 9999, ex.Message);
                }
            };
        }
    }

    /// <summary>
    /// Sets the projection completed event action for the specified entity ID type and complete event type.
    /// </summary>
    /// <typeparam name="TEntityId"></typeparam>
    /// <typeparam name="TComplete"></typeparam>
    /// <param name="completedEventAction"></param>
    void SetProjectionCompletedEventAction<TEntityId,TComplete>(Func<IEvent, ValueTask> completedEventAction, bool overwrite = false)
        where TComplete : class, ICompleteEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (overwrite)
        {
            IsArgumentNull.Check(completedEventAction);
            _completedEventAction =  e => completedEventAction(e);
        }
        else if (_completedEventAction is null)
        {
            IsArgumentNull.Check(completedEventAction);
            _completedEventAction =  e => completedEventAction(e);
        }
    }

    /// <summary>
    /// Sets the projection failed event action for the specified entity ID type and fail event type.
    /// </summary>
    /// <typeparam name="TEntityId"></typeparam>
    /// <typeparam name="TFail"></typeparam>
    /// <param name="failedEventAction"></param>
    void SetProjectionFailedEventAction<TEntityId,TFail>(Func<IEvent, string, ValueTask> failedEventAction, bool overwrite = false)
        where TFail : class, IErrorEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        if (overwrite)
        {
            IsArgumentNull.Check(failedEventAction);
            _failedEventAction = (e, errorMessage) => failedEventAction(e, errorMessage);
        }
        else if (_failedEventAction is null)
        {
            IsArgumentNull.Check(failedEventAction);
            _failedEventAction = (e, errorMessage) => failedEventAction(e, errorMessage);
        }
    }

}
