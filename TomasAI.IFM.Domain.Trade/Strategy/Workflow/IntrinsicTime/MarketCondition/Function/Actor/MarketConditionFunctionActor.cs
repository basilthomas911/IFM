using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;

/// <summary>Hosts the assessment-only, completed-only Market Condition lifecycle.</summary>
public sealed class MarketConditionFunctionActor(IFunctionActorContext<MarketConditionFunctionActor> actorContext)
    : IFunctionActor<MarketConditionFunctionActor>
{
    readonly IMarketConditionFunctionContext _context = actorContext as IMarketConditionFunctionContext
        ?? throw new ArgumentException($"Context must implement {nameof(IMarketConditionFunctionContext)}.", nameof(actorContext));
    IActorSupervisor? _supervisor;
    int _lifecycle;
    public const string ActorName = ExecuteMarketConditionAssessmentCommand.Actor;
    public ActorMailboxId Id => _context.ActorId;
    public IActorMailbox Mailbox { get; private set; } = default!;
    public bool IsRunning => Volatile.Read(ref _lifecycle) == 2;

    public ValueTask StartAsync(IActorSupervisor supervisor) => StartAsync(supervisor, CancellationToken.None);
    public async ValueTask StartAsync(IActorSupervisor supervisor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(supervisor);
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _lifecycle, 1, 0) != 0) return;
        try
        {
            _supervisor = supervisor;
            Mailbox = supervisor.CreateMailbox(Id);
            await supervisor.GetProducer(Id).StartAsync(Id, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _lifecycle, 2);
        }
        catch
        {
            Volatile.Write(ref _lifecycle, 0);
            throw;
        }
    }

    public ValueTask StopAsync() => StopAsync(CancellationToken.None);
    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _lifecycle, 3, 2) != 2) return;
        try
        {
            if (_supervisor is not null)
                await _supervisor.GetProducer(Id).StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { Volatile.Write(ref _lifecycle, 0); }
    }

    public ValueTask HandleMessageAsync(IActorMessage message)
        => HandleMessageAsync(message, message.Subject.ThreadId, CancellationToken.None);
    public ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
        => HandleMessageAsync(message, threadId, CancellationToken.None);
    public ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId, CancellationToken cancellationToken)
        => _context.AssessmentHandler.HandleAsync(_context, message, threadId, cancellationToken);
}
