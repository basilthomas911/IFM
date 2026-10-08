using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.ScheduledTask.Shared;
/// <summary>Subscribes before submission and waits for the exact command's terminal business event.</summary>
public sealed class ScheduledTaskEventCompletion(NatsEventListenerOptions options, ILogger<ScheduledTaskEventCompletion> logger)
{
    /// <summary>Confirms a correlated completion; acknowledgement, another command's event and timeout cannot satisfy it.</summary>
    /// <typeparam name="TComplete">The concrete successful business notification.</typeparam>
    /// <typeparam name="TFail">The concrete failed business notification.</typeparam>
    /// <param name="actor">The public event actor mailbox.</param>
    /// <param name="completeVerb">The successful notification verb.</param>
    /// <param name="failVerb">The failure notification verb.</param>
    /// <param name="submit">The typed command submission performed after subscription.</param>
    /// <param name="cancellationToken">Bounds command submission and completion observation.</param>
    /// <returns>The correlated successful business event.</returns>
    public async Task<TComplete> ExecuteAsync<TComplete,TFail>(string actor, string completeVerb, string failVerb,
        Func<Task<ServiceResult<Guid>>> submit, CancellationToken cancellationToken)
        where TComplete : class, ICompleteEvent where TFail : class, IErrorEvent
    {
        var waiter = new CorrelatedScheduledTaskCompletion<TComplete>();
        var listener = new NatsActorEventListener(options, logger);
        await listener.StartAsync("scheduled-completion-" + Guid.NewGuid().ToString("N"),
            new Dictionary<ActorMailboxId,List<string>> { [new(ActorType.Event,actor)] = [completeVerb,failVerb] },
            (verb,message) =>
            {
                if (verb == completeVerb && message.AsEvent<TComplete>() is { } complete) waiter.Observe(complete);
                if (verb == failVerb && message.AsEvent<TFail>() is { } fail) waiter.ObserveFailure(fail);
                return ValueTask.CompletedTask;
            });
        try
        {
            var accepted = await submit().WaitAsync(cancellationToken);
            if (!accepted.Success || accepted.Value == Guid.Empty) throw new InvalidOperationException("Business command rejected: " + accepted.ErrorMessage);
            waiter.Expect(accepted.Value);
            return await waiter.Completion.WaitAsync(cancellationToken);
        }
        finally { await listener.StopAsync(); }
    }
}
/// <summary>Retains only a bounded set of terminal events arriving before the command reply identifies the correlation.</summary>
public sealed class CorrelatedScheduledTaskCompletion<TComplete> where TComplete : class,ICompleteEvent
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid,IEvent> _early = [];
    private readonly TaskCompletionSource<TComplete> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Guid _expected;
    /// <summary>Gets the single correlated outcome.</summary>
    public Task<TComplete> Completion => _completion.Task;
    /// <summary>Sets the accepted command identity and examines bounded early notifications.</summary>
    public void Expect(Guid commandId)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("A command identity is required.",nameof(commandId));
        lock (_gate) { _expected = commandId; if (_early.TryGetValue(commandId,out var value)) Complete(value); _early.Clear(); }
    }
    /// <summary>Observes a concrete successful business event.</summary>
    public void Observe(TComplete value) => ObserveEvent(value);
    /// <summary>Observes a concrete failed business event.</summary>
    public void ObserveFailure(IErrorEvent value) => ObserveEvent(value);
    /// <summary>Matches the expected identity without allowing unrelated traffic to consume unbounded memory.</summary>
    private void ObserveEvent(IEvent value)
    {
        lock (_gate)
        {
            if (_expected != Guid.Empty) { if (value.CommandId == _expected) Complete(value); return; }
            if (_early.Count < 256 || _early.ContainsKey(value.CommandId)) _early[value.CommandId] = value;
        }
    }
    /// <summary>Completes or faults the waiter using the correlated terminal event.</summary>
    private void Complete(IEvent value)
    {
        if (value is IErrorEvent failed) _completion.TrySetException(new InvalidOperationException(failed.ErrorMessage));
        else if (value is TComplete complete) _completion.TrySetResult(complete);
    }
}
