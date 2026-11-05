using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Query.Model;

/// <summary>Provides shared reply behavior for Portfolio query handlers.</summary>
internal static class PortfolioQueryHandlerModel
{
    /// <summary>Awaits a typed service result and sends it to the query caller.</summary>
    /// <typeparam name="TActor">The owning Portfolio Query actor type.</typeparam>
    /// <typeparam name="TResult">The projection result type.</typeparam>
    /// <param name="context">The actor reply context.</param>
    /// <param name="query">The concrete query to reply to.</param>
    /// <param name="resultTask">The pending projection result.</param>
    /// <returns>The asynchronous reply operation.</returns>
    internal static async ValueTask ReplyAsync<TActor, TResult>(IQueryActorContext<TActor> context, IQuery query, Task<ServiceResult<TResult>> resultTask, CancellationToken cancellationToken = default)
        where TActor : IActor
        where TResult : class
    {
        var result = await resultTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
    }

}
