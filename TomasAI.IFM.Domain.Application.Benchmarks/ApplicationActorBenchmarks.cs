using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.Application.Actor.Command.State;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Application.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class ApplicationStateReplayBenchmarks
{
    IEvent[] _events = null!;

    [Params(32, 256, 2048)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _events = new IEvent[Count];
        for (var index = 0; index < _events.Length; index++)
        {
            _events[index] = (index & 1) == 0
                ? new ApplicationStartupEvent()
                : new ApplicationShutdownEvent();
        }
    }

    [Benchmark(Baseline = true)]
    public FormerApplicationCommandState FormerGuardedReplay()
    {
        var state = new FormerApplicationCommandState();
        state.ReplayEvents(_events);
        return state;
    }

    [Benchmark]
    public ApplicationCommandState CurrentTypedReplay()
    {
        var state = new ApplicationCommandState();
        state.ReplayEvents(_events);
        return state;
    }

    public sealed class FormerApplicationCommandState
        : BaseEventSourceActorState<FormerApplicationCommandState>,
          IEventSourceActorState<FormerApplicationCommandState>
    {
        public override ActorThreadId Id { get; set; } = default!;

        protected override bool Apply(IEvent domainEvent)
        {
            try
            {
                return domainEvent switch
                {
                    ApplicationStartupEvent startup => On(startup),
                    ApplicationShutdownEvent shutdown => On(shutdown),
                    _ => false
                };
            }
            catch
            {
                return false;
            }
        }

        static bool On(ApplicationStartupEvent _) => true;
        static bool On(ApplicationShutdownEvent _) => true;
    }
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 12)]
public class ApplicationCommandDispatchBenchmarks
{
    static readonly Dictionary<string, Func<ICommand, ICommand>> FormerRoutes = new()
    {
        [StartApplicationCommand.Verb] = static command => (StartApplicationCommand)command,
        [ShutdownApplicationCommand.Verb] = static command => (ShutdownApplicationCommand)command
    };

    ICommand[] _commands = null!;
    string[] _verbs = null!;

    [GlobalSetup]
    public void Setup()
    {
        _commands =
        [
            new StartApplicationCommand(),
            new ShutdownApplicationCommand()
        ];
        _verbs =
        [
            StartApplicationCommand.Verb,
            ShutdownApplicationCommand.Verb
        ];
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 2)]
    public int FormerDictionaryAndDelegate()
        => DispatchAll(DispatchWithDictionary);

    [Benchmark(OperationsPerInvoke = 2)]
    public int CurrentStringSwitch()
        => DispatchAll(DispatchWithSwitch);

    int DispatchAll(Func<string, ICommand, ICommand> dispatch)
        => dispatch(_verbs[0], _commands[0]).ErrorCode
         + dispatch(_verbs[1], _commands[1]).ErrorCode;

    static ICommand DispatchWithDictionary(string verb, ICommand command)
        => FormerRoutes.TryGetValue(verb, out var route)
            ? route(command)
            : throw new InvalidOperationException($"Unknown application command verb '{verb}'.");

    static ICommand DispatchWithSwitch(string verb, ICommand command)
        => verb switch
        {
            StartApplicationCommand.Verb => (StartApplicationCommand)command,
            ShutdownApplicationCommand.Verb => (ShutdownApplicationCommand)command,
            _ => throw new InvalidOperationException($"Unknown application command verb '{verb}'.")
        };
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 12)]
public class ApplicationCommandValidationBenchmarks
{
    readonly Guid _commandId = Guid.Parse("8d86137a-c2c3-49f3-b00f-0ec72c91058b");

    [Benchmark(Baseline = true)]
    public int FormerListValidation()
    {
        var errors = new List<ValidationError>()
            .ValidateCommandId(_commandId, nameof(StartApplicationCommand))
            .ThrowCommandValidationExceptionOnAnyError(StartApplicationCommand.ErrorId);
        return errors.Count;
    }

    [Benchmark]
    public int CurrentDirectValidation()
    {
        if (_commandId == Guid.Empty)
            throw new InvalidOperationException("CommandId is empty.");
        return 0;
    }
}
