using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NATS.Client.Core;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Benchmarks;

/// <summary>
/// Compares only the routing portion of YieldCurveRateCommandActor.ParseMessage.
/// Payload deserialization is deliberately replaced by a pre-materialized command
/// so its much larger cost does not conceal dispatch differences.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 12)]
public class YieldCurveRateParseDispatchBenchmarks
{
    const string ActorName = "YieldCurveRateCommand";

    static readonly Dictionary<string, Func<IActorMessage, ICommand>> DictionaryRoutes = new()
    {
        [AddYieldCurveRateCommand.Verb] = ParseAdd,
        [ChangeYieldCurveRateCommand.Verb] = ParseChange,
        [RemoveYieldCurveRateCommand.Verb] = ParseRemove,
        [ImportYieldCurveRatesCommand.Verb] = ParseImport
    };

    // Collision-free for the four current verbs:
    // Add -> 2, Remove -> 4, Change -> 5, Import -> 7.
    // The expected verb is still compared ordinally so unknown/colliding input
    // cannot be routed to the wrong command parser.
    static readonly ParseRoute[] JumpRoutes =
    [
        default,
        default,
        new(AddYieldCurveRateCommand.Verb, ParseAdd),
        default,
        new(RemoveYieldCurveRateCommand.Verb, ParseRemove),
        new(ChangeYieldCurveRateCommand.Verb, ParseChange),
        default,
        new(ImportYieldCurveRatesCommand.Verb, ParseImport)
    ];

    IActorMessage[] _messages = null!;

    [GlobalSetup]
    public void Setup()
    {
        _messages =
        [
            CreateMessage(new AddYieldCurveRateCommand(), AddYieldCurveRateCommand.Verb),
            CreateMessage(new ChangeYieldCurveRateCommand(), ChangeYieldCurveRateCommand.Verb),
            CreateMessage(new RemoveYieldCurveRateCommand(), RemoveYieldCurveRateCommand.Verb),
            CreateMessage(new ImportYieldCurveRatesCommand(), ImportYieldCurveRatesCommand.Verb)
        ];
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 4)]
    public int CurrentStringSwitch()
        => ParseAll(ParseWithSwitch);

    [Benchmark(OperationsPerInvoke = 4)]
    public int FormerDictionaryAndDelegate()
        => ParseAll(ParseWithDictionary);

    [Benchmark(OperationsPerInvoke = 4)]
    public int PerfectHashJumpTable()
        => ParseAll(ParseWithJumpTable);

    int ParseAll(Func<IActorMessage, ICommand> parser)
        => parser(_messages[0]).ErrorCode
         + parser(_messages[1]).ErrorCode
         + parser(_messages[2]).ErrorCode
         + parser(_messages[3]).ErrorCode;

    static ICommand ParseWithSwitch(IActorMessage message)
    {
        var subject = ValidateSubject(message);
        return subject.Verb switch
        {
            AddYieldCurveRateCommand.Verb => ParseAdd(message),
            ChangeYieldCurveRateCommand.Verb => ParseChange(message),
            RemoveYieldCurveRateCommand.Verb => ParseRemove(message),
            ImportYieldCurveRatesCommand.Verb => ParseImport(message),
            _ => throw UnknownVerb(subject.Verb)
        };
    }

    static ICommand ParseWithDictionary(IActorMessage message)
    {
        var subject = ValidateSubject(message);
        if (!DictionaryRoutes.TryGetValue(subject.Verb, out var parser))
            throw UnknownVerb(subject.Verb);
        return parser(message);
    }

    static ICommand ParseWithJumpTable(IActorMessage message)
    {
        var subject = ValidateSubject(message);
        var verb = subject.Verb;
        if (verb.Length == 0)
            throw UnknownVerb(verb);

        var route = JumpRoutes[(verb[0] ^ verb.Length) & 7];
        if (!string.Equals(route.Verb, verb, StringComparison.Ordinal))
            throw UnknownVerb(verb);
        return route.Parser!(message);
    }

    static ActorSubject ValidateSubject(IActorMessage message)
    {
        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Command, Name: ActorName })
            throw new InvalidOperationException($"Invalid subject: {subject}");
        return subject;
    }

    static ICommand ParseAdd(IActorMessage message)
        => message.AsCommand<AddYieldCurveRateCommand>()!;

    static ICommand ParseChange(IActorMessage message)
        => message.AsCommand<ChangeYieldCurveRateCommand>()!;

    static ICommand ParseRemove(IActorMessage message)
        => message.AsCommand<RemoveYieldCurveRateCommand>()!;

    static ICommand ParseImport(IActorMessage message)
        => message.AsCommand<ImportYieldCurveRatesCommand>()!;

    static InvalidOperationException UnknownVerb(string verb)
        => new($"Unknown yield-curve command verb: {verb}");

    static IActorMessage CreateMessage(ICommand command, string verb)
    {
        var subject = new ActorSubject(ActorType.Command, ActorName, verb, "2026");
        return new MaterializedCommandMessage(command, subject);
    }

    readonly record struct ParseRoute(
        string? Verb,
        Func<IActorMessage, ICommand>? Parser);

    sealed class MaterializedCommandMessage(
        ICommand command,
        ActorSubject subject) : IActorMessage
    {
        public ActorSubject Subject { get; } = subject;

        public ActorSubject ReplySubject { get; set; }

        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand
            => command as TCommand;

        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent
            => throw new NotSupportedException();

        public TQuery? AsQuery<TQuery, TResult>()
            where TQuery : class, IQuery<TResult>
            where TResult : class
            => throw new NotSupportedException();

        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class
            => throw new NotSupportedException();

        public void ReleasePayload() { }

        public NatsMsg<byte[]> GetMessage()
            => throw new NotSupportedException();

        public void Dispose() { }
    }
}
