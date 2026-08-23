using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides transitional operations for typed query contexts while <c>BaseQueryActor&lt;TActor&gt;</c> continues to own
/// the runtime query context.
/// </summary>
public static class QueryActorContextExtensions
{
    /// <summary>
    /// Mirrors the reply metadata for one query from the runtime context into a typed query context.
    /// </summary>
    /// <remarks>
    /// The returned scope removes the mirrored entry when disposed. The source entry remains available so the base
    /// context can send an error reply if query execution fails. This bridge can be removed after the base query actor
    /// uses the injected typed context as its runtime context.
    /// </remarks>
    /// <param name="source">The runtime context that parsed the incoming query.</param>
    /// <param name="target">The typed context that will execute the query and send its reply.</param>
    /// <param name="threadId">The query thread identifier.</param>
    /// <param name="verb">The query verb.</param>
    /// <returns>A scope that removes any metadata copied into <paramref name="target"/>.</returns>
    public static QueryActorMessageInfoScope MirrorMessageInfoTo(
        this IQueryActorContext source,
        IQueryActorContext target,
        ActorThreadId threadId,
        string verb)
    {
        IsArgumentNull.Check(source);
        IsArgumentNull.Check(target);
        IsArgumentNull.Check(threadId);
        IsArgumentNull.Check(verb);

        if (ReferenceEquals(source, target))
            return default;

        var messageInfo = source.GetMessageInfo(threadId, verb);
        var copied = messageInfo.HasValue
            && target.SetMessageInfo(threadId, verb, messageInfo.Value);
        return new QueryActorMessageInfoScope(target, threadId, verb, copied);
    }
}

/// <summary>
/// Removes query reply metadata mirrored into a typed query context.
/// </summary>
public readonly struct QueryActorMessageInfoScope : IDisposable
{
    readonly IQueryActorContext? _target;
    readonly ActorThreadId _threadId;
    readonly string? _verb;
    readonly bool _copied;

    internal QueryActorMessageInfoScope(
        IQueryActorContext target,
        ActorThreadId threadId,
        string verb,
        bool copied)
    {
        _target = target;
        _threadId = threadId;
        _verb = verb;
        _copied = copied;
    }

    /// <summary>
    /// Removes the mirrored reply metadata when a copy was created.
    /// </summary>
    public void Dispose()
    {
        if (_copied)
            _target!.RemoveMessageInfo(_threadId, _verb!);
    }
}
