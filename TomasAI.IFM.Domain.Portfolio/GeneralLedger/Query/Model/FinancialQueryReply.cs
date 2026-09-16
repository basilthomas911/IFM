using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;

/// <summary>Validates financial query envelopes and sends bounded typed replies.</summary>
internal static class FinancialQueryReply
{
    /// <summary>Executes a financial read after validating its contract and response-size limits.</summary>
    internal static async ValueTask ExecuteAsync<TRequest, TResult, TActor>(FinancialQuery<TRequest, TResult> query,
        IQueryActorContext<TActor> context, string actor, string verb, Func<Task<FinancialRead<TResult>>> read, CancellationToken cancellationToken)
        where TResult : class where TActor : IActor
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query.SchemaVersion != 1 || query.Parameters is null || query.Scope is null || query.Scope.Access is null || query.Scope.PortfolioId <= 0 ||
            query.QueryEntityId.PortfolioId != query.Scope.PortfolioId || query.Subject.EntityId != query.QueryEntityId.Format() ||
            !query.Subject.Is(ActorType.Query, actor, verb) || query.CorrelationId == Guid.Empty || query.RequestedAtUtc.Kind != DateTimeKind.Utc ||
            MessagePackBinarySerializer.MeasureContent(query) > 1048576)
            throw new FinancialOperationException(FinancialReasons.InvalidContract, "Financial query contract/scope is invalid.");
        var result = await read();
        if (MessagePackBinarySerializer.MeasureContent(result) > 524288)
            throw new FinancialOperationException(FinancialReasons.InvalidContract, "Financial result exceeds 512 KiB; use a smaller page.");
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<FinancialRead<TResult>>(result));
    }
}