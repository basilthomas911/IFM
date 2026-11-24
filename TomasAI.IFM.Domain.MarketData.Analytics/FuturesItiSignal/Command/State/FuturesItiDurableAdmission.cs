using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;

[GeneratedMessagePackResolver]
internal partial class FuturesItiJournalResolver;

/// <summary>Unserializable capability issued only after the exact batch has been journaled by its fenced owner.</summary>
public sealed class FuturesItiDurableAdmission
{
    internal FuturesItiDurableAdmission(FuturesItiResidentStore store, GenerateFuturesItiSignalCommand[] inputs)
        => (Store, Inputs) = (store, inputs);
    internal FuturesItiResidentStore Store { get; }
    internal GenerateFuturesItiSignalCommand[] Inputs { get; }
    internal bool Matches(GenerateFuturesItiSignalCommand command)
        => ReferenceEquals(command.ResidentAdmission, this) && ReferenceEquals(command.ResidentBatch, Inputs);
}

/// <summary>Binary journal envelope preserves original message origins independently from command serialization.</summary>
[MessagePackObject]
public sealed record FuturesItiJournalEnvelope(
    [property: Key(0)] GenerateFuturesItiSignalCommand Command,
    [property: Key(1)] long OriginUtcTicks,
    [property: Key(2)] string? ClockDomain,
    [property: Key(3)] Guid? RunId,
    [property: Key(4)] Guid? Generation,
    [property: Key(5)] Guid? Causation)
{
    public static byte[] Encode(GenerateFuturesItiSignalCommand command)
    {
        var origin = MessageOrigins.Get(command);
        return MessagePackSerializer.Serialize(new FuturesItiJournalEnvelope(command, origin?.OriginatedAtUtc.UtcTicks ?? 0,
            origin?.ClockDomainId, origin?.RunId, origin?.Generation, origin?.CausationId));
    }
    public static GenerateFuturesItiSignalCommand Decode(byte[] payload, long sequence)
    {
        var input = MessagePackSerializer.Deserialize<FuturesItiJournalEnvelope>(payload);
        return MessageOrigins.Restore(input.Command with { JournalSequence = sequence, JournalPayload = payload }, input.OriginUtcTicks == 0 ? null
            : new MessageOrigin(new DateTimeOffset(input.OriginUtcTicks, TimeSpan.Zero), input.ClockDomain!,
                input.RunId, input.Generation, input.Causation));
    }
}

/// <summary>Materialized local dispatch; neither raw ticks nor acknowledgements make a NATS round trip.</summary>
internal sealed class FuturesItiLocalMessage(GenerateFuturesItiSignalCommand command) : IActorMessage
{
    public ServiceResult? Result { get; private set; }
    public ActorSubject Subject => command.Subject;
    public ActorSubject ReplySubject { get; set; } = default!;
    public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => command as TCommand;
    public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
    public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
    public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class { Result = result as ServiceResult; return ValueTask.CompletedTask; }
    public void ReleasePayload() { }
    public NatsMsg<byte[]> GetMessage() => default;
    public void Dispose() { }
}

public interface IFuturesItiResidentRepository : IResidentCommandStateRepository<FuturesItiSignalCommandState>
{
    FuturesItiResidentStore? ResidentStore { get; }
    ValueTask OpenResidentWriterAsync(ICommandActorContext context, CancellationToken token);
    ValueTask CloseResidentWriterAsync();
    void AdvanceProcessed(string stream, long sequence);
}
