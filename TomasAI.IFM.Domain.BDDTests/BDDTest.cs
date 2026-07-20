using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.BDDTests;

/// <summary>
/// run BDD test framework setup
/// </summary>
/// <typeparam name="TBoundedContext"></typeparam>
/// <typeparam name="TboundedContextState"></typeparam>
/// <typeparam name="TEntity"></typeparam>
/// <typeparam name="TEventProducer"></typeparam>
public class RunBDDTest<TBoundedContext, TboundedContextState, TEntity, TEventProducer>
    where TBoundedContext : IBoundedContext
    where TboundedContextState : IBoundedContextState<TboundedContextState>
    where TEventProducer : class, IEventProducer
    where TEntity : IActorEntityId
{
    public RunBDDTest(
        Func<Type, object?> getCommandHandler,
        Func<TboundedContextState, IBoundedContextCommandResolver, IBoundedContext<TboundedContextState>> getBoundedContext,
        Func<object>? getCommandDecorator,
        Func<TEventProducer, ILogger, object>? getExceptionDecorator,
        Func<object[]>? getStateArgs = null,
        Func<TEventProducer, ILogger, IEventDenormalizer<TboundedContextState>>? getEventDenormalizer = null)
    {
        var logger = Substitute.For<ILogger>();
        logger.When(_ => { }).Do(_ => { });
        var eventProducer = Substitute.For<TEventProducer>();
        eventProducer.When(_ => { }).Do(_ => { });
        var resolver = Substitute.For<IBoundedContextCommandResolver>();
        resolver.Resolve(Arg.Any<Type>())
                .Returns(callInfo =>
                {
                    var handlerType = callInfo.ArgAt<Type>(0);
                    var cmdType = handlerType.GenericTypeArguments[0];
                    var cmdHandler =  getCommandHandler?.Invoke(cmdType);
                    if (cmdHandler is not null && cmdHandler.GetType().GetInterfaces()
                        .Where(e => e.IsGenericType && e == handlerType)
                        .FirstOrDefault() is not null)
                        return cmdHandler;
                    return null;
                });
        var aggregateFactory = Substitute.For<IBoundedContextFactory>();
        aggregateFactory
           .CreateBoundedContext<TboundedContextState>( Arg.Any<DomainEventCollection>())
           .Returns(callInfo =>
           {
               var domainEvents = callInfo.ArgAt<DomainEventCollection>(0);
               TboundedContextState? state = getStateArgs is not null
                    ? (TboundedContextState)Activator.CreateInstance(typeof(TboundedContextState), getStateArgs())!
                    : Activator.CreateInstance<TboundedContextState>();
               state?.ReplayEvents(domainEvents);
               return getBoundedContext(state!, resolver);
           });
        var dbEventSource = Substitute.For<IEventSourceDbContext>();
        var dataCacheService = Substitute.For<IDataCacheService>();
        List<IEvent> denormalizerEvents = [];
        var eventDenormalizer = Substitute.For<IEventDenormalizer<TboundedContextState>>();
        eventDenormalizer
            .When(e => e.ExecuteAsync(Arg.Any<DomainEventCollection>()))
            .Do(callInfo =>
            {
                var domainEvents = callInfo.ArgAt<DomainEventCollection>(0);
                if (domainEvents is not null)
                    denormalizerEvents.AddRange(domainEvents);
            });
        Value = new BDDTest<TBoundedContext, TboundedContextState, TEntity>(
                        (testEvents) => {
                            var eventRepo = Substitute.For<IEventRepository<TboundedContextState>>();
                            eventRepo
                                .LoadBoundedContextAsync(Arg.Any<ICommand<TEntity>>())
                                .Returns(_ =>
                                {
                                    var domainEvents = new DomainEventCollection(testEvents);
                                    return aggregateFactory.CreateBoundedContext<TboundedContextState>(domainEvents);
                                });
                            eventRepo
                                .SaveBoundedContextAsync(Arg.Any<IBoundedContextState<TboundedContextState>>(), Arg.Any<ICommand<TEntity>>())
                                .Returns(callInfo =>
                                {
                                    var boundedContextState = callInfo.ArgAt<IBoundedContextState<TboundedContextState>>(0);
                                    if (getEventDenormalizer is not null)
                                        getEventDenormalizer(eventProducer, logger).DenormalizeEventsAsync(boundedContextState.Events).Wait();
                                   return  eventDenormalizer.ExecuteAsync(boundedContextState.Events);
                                });
                            return eventRepo;
                        },
                       () => [.. denormalizerEvents.Cast<IEvent>()],
                        getCommandDecorator?.Invoke()!,
                        getExceptionDecorator?.Invoke(eventProducer, logger)!);
    }

    public BDDTest<TBoundedContext, TboundedContextState, TEntity> Value { get; }
}

/// <summary>
/// BDD test framework
/// </summary>
/// <typeparam name="TBoundedContext"></typeparam>
/// <typeparam name="TboundedContextState"></typeparam>
/// <typeparam name="TEntity"></typeparam>
public class BDDTest<TBoundedContext, TboundedContextState, TEntity>
    where TBoundedContext : IBoundedContext
    where TboundedContextState : IBoundedContextState<TboundedContextState>
    where TEntity : IActorEntityId
{
    readonly Func<IEvent[], IEventRepository<TboundedContextState>> _getRepository;
    readonly Func<IEvent[]> _getDenormalizerEvents;
    readonly IValidationCommandDecorator<TboundedContextState> _commandDecorator;
    readonly IExceptionCommandDecorator<TboundedContextState> _exceptionDecorator;
    Func<IEvent[]>? _givenEvents;
    Func<ICommand<TEntity>>? _whenCommand;
    object? _state;

    public BDDTest(Func<IEvent[], IEventRepository<TboundedContextState>> getRepository, Func<IEvent[]> getDenormalizerEvents, object commandDecorator = null, object exceptionDecorator = null)
    {
        _getRepository = getRepository;
        _getDenormalizerEvents = getDenormalizerEvents;
        _commandDecorator = (commandDecorator as IValidationCommandDecorator<TboundedContextState>)!;
        _exceptionDecorator = (exceptionDecorator as IExceptionCommandDecorator<TboundedContextState>)!;
    }

    /// <summary>
    /// /// state of the bounded context
    /// </summary>
    public object State => _state!;

    /// <summary>
    /// given events
    /// </summary>
    /// <param name="givenEvents"></param>
    /// <returns></returns>
    public BDDTest<TBoundedContext, TboundedContextState, TEntity> Given(Func<IEvent[]> givenEvents)
    {
        _givenEvents = givenEvents;
        return this;
    }

    /// <summary>
    /// set initial state events
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public BDDTest<TBoundedContext, TboundedContextState, TEntity> Given(object state)
    {
        _givenEvents = () => [];
        _state = state;
        return this;
    }

    /// <summary>
    /// set command action
    /// </summary>
    /// <param name="whenCommand"></param>
    /// <returns></returns>
    public BDDTest<TBoundedContext, TboundedContextState, TEntity> When(Func<ICommand<TEntity>> whenCommand)
    {
        _whenCommand = whenCommand;
        return this;
    }


    /// <summary>
    /// execute bdd test: give initial stat when command executed then assert final state
    /// </summary>
    /// <param name="assertFunction"></param>
    public void Then(Func<IEvent[], bool> assertFunction)
    {
        var command = default(ICommand<TEntity>);
        try
        {
            /// set initial state...
            var currentState = _givenEvents?.Invoke();

            /// get aggregate command...
            command = _whenCommand?.Invoke();

            /// validate command...
            _commandDecorator?.Validate(command!);

            /// load bounded context from repository...
            var eventRepo = _getRepository(currentState!);
            var aggregateRoot = eventRepo.LoadBoundedContextAsync(command!).Result;

            /// execute aggregate command...
            aggregateRoot.Execute((dynamic)command!);

            /// save bounded context state to repository...
            var boundedContextState = aggregateRoot.State;
            eventRepo.SaveBoundedContextAsync(boundedContextState, command!).Wait();

            /// get denormalizer events...
            var events = _getDenormalizerEvents();

            /// assert final state...
            Assert.True(assertFunction(events));
        }
        catch (Exception ex)
        {
            while (ex.InnerException is not null)
                ex = ex.InnerException;
            IEvent[] exceptionEvent = [ _exceptionDecorator is not null
                ? _exceptionDecorator.ConvertExceptionToErrorEventAsync(command!, ex).Result 
                :  new ExceptionEvent { Exception = ex }  ];
            Assert.True(assertFunction(exceptionEvent), ex.Message);
        }
    }
}

