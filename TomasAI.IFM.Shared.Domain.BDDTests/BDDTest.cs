using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.Domain.BDDTests
{
    public class BDDTest<TBoundedContext, TboundedContextState, TEntity>(Func<IEvent[], IEventRepository<TboundedContextState>> getRepository, Func<IEvent[]> getDenormalizerEvents, object? commandDecorator = null, object? exceptionDecorator = null)
        where TBoundedContext : IBoundedContext
        where TboundedContextState : IBoundedContextState<TboundedContextState>
        where TEntity : IActorEntityId
    {
        readonly Func<IEvent[], IEventRepository<TboundedContextState>> _getRepository = getRepository;
        readonly Func<IEvent[]> _getDenormalizerEvents = getDenormalizerEvents;
        readonly IValidationCommandDecorator<TboundedContextState>? _commandDecorator = commandDecorator as IValidationCommandDecorator<TboundedContextState>;
        readonly IExceptionCommandDecorator<TboundedContextState>? _exceptionDecorator = exceptionDecorator as IExceptionCommandDecorator<TboundedContextState>;
        Func<IEvent[]> _givenEvents = () => [];
        Func<ICommand<TEntity>> _whenCommand = () => throw new InvalidOperationException("When command was not configured.");
        object? _state;

        public object? State => _state;

        public BDDTest<TBoundedContext, TboundedContextState, TEntity> Given(Func<IEvent[]> givenEvents)
        {
            _givenEvents = givenEvents;
            return this;
        }

        public BDDTest<TBoundedContext, TboundedContextState, TEntity> Given(object state)
        {
            _givenEvents = () =>  new IEvent[] {};
            _state = state;
            return this;
        }


        public void Then(Func<IEvent[], bool> assertFunction)
        {
            ICommand<TEntity>? command = null;
            try
            {
                var currentState = _givenEvents();
                command = _whenCommand();
                _commandDecorator?.Validate(command);
                var eventRepo = _getRepository(currentState);
                var aggregateRoot = eventRepo.LoadBoundedContextAsync(command).Result;
                aggregateRoot.Execute((dynamic)command);
                var boundedContextState = (aggregateRoot as IBoundedContext<TboundedContextState>)!.State;
                eventRepo.SaveBoundedContextAsync(boundedContextState, command).Wait();
                var events = _getDenormalizerEvents();
                Assert.True(assertFunction(events));
            }
            catch (Exception ex)
            {
                IEvent[] exceptionEvent = _exceptionDecorator is not null && command is not null
                    ? [ _exceptionDecorator.ConvertExceptionToErrorEventAsync(command, ex).Result ]
                    : [ new ExceptionEvent { Exception = ex } ];
                Assert.True(assertFunction(exceptionEvent), ex.Message);
             }
        }
    }

    public class BDDTest<TBoundedContext, TboundedContextState>
        where TBoundedContext : IBoundedContext
        where TboundedContextState : IBoundedContextState<TboundedContextState>
    {
        readonly Func<TboundedContextState, IBoundedContext>? _getBoundedContext;
        readonly Func<TboundedContextState, IBoundedContextCommandResolver, IBoundedContext>? _getBoundedContextFromResolver;
        readonly Func<IEvent[], TboundedContextState>? _getBoundedContextState;
        readonly Func<IBoundedContextCommandResolver>? _getCommandHandlerResolver;
        readonly Func<IEvent[], IEventRepository<TboundedContextState>>? _getRepository;
        readonly IValidationCommandDecorator<TboundedContextState>? _commandDecorator;
        Func<IEvent[]> _givenEvents = () => [];
        Func<ICommand> _whenCommand = () => throw new InvalidOperationException("When command was not configured.");


        public BDDTest(Func<TboundedContextState, IBoundedContext> getBoundedContext, Func<IEvent[], TboundedContextState> getBoundedContextState, IValidationCommandDecorator<TboundedContextState>? commandDecorator = null)
        {
            _getBoundedContext = getBoundedContext;
            _getBoundedContextState = getBoundedContextState;
            _commandDecorator = commandDecorator;
        }

        public BDDTest(Func<TboundedContextState, IBoundedContextCommandResolver, IBoundedContext> getBoundedContext, Func<IEvent[], TboundedContextState> getBoundedContextState, Func<IBoundedContextCommandResolver> getCommandHandlerResolver, IValidationCommandDecorator<TboundedContextState>? commandDecorator = null)
        {
            _getBoundedContextFromResolver = getBoundedContext;
            _getBoundedContextState = getBoundedContextState;
            _getCommandHandlerResolver = getCommandHandlerResolver;
            _commandDecorator = commandDecorator;
        }

        public BDDTest(Func<IEvent[], IEventRepository<TboundedContextState>> getRepository, IValidationCommandDecorator<TboundedContextState>? commandDecorator = null)
        {
            _getRepository = getRepository;
            _commandDecorator = commandDecorator;
        }

        public BDDTest<TBoundedContext, TboundedContextState> Given(Func<IEvent[]> givenEvents)
        {
            _givenEvents = givenEvents;
            return this;
        }

        public BDDTest<TBoundedContext, TboundedContextState> When(Func<ICommand> whenCommand)
        {
            _whenCommand = whenCommand;
            return this;
        }

        public void Then(Func<IEvent[], bool> assertFunction)
        {
            var currentState = _givenEvents();
            if (_getRepository is not null)
                TestHarness1();
            else
                TestHarness2();
            return;

            void TestHarness1()
            {
                var eventRepo = _getRepository(currentState);

            }

            void TestHarness2()
            {
                var boundedContextState = _getBoundedContextState!(currentState);
                var aggregateRoot = _getBoundedContext is null
                    ? _getBoundedContextFromResolver!(boundedContextState, _getCommandHandlerResolver!())
                    : _getBoundedContext(boundedContextState);
                try
                {
                    var command = _whenCommand();
                    if (_commandDecorator != null)
                    {
                        dynamic validator = _commandDecorator;
                        validator.Validate((dynamic)command);
                    }
                    aggregateRoot.Execute((dynamic)command);
                    var events = boundedContextState.Events;
                    Assert.True(assertFunction(events.ToArray()));
                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null) ex = ex.InnerException;
                    var exceptionEvent = new IEvent[] { new ExceptionEvent { Exception = ex } };
                    Assert.True(assertFunction(exceptionEvent), ex.Message);
                }
            }
        }

    }

}
