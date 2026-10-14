using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Persistence;
/// <summary>Commits command audit, complete accepted pipeline event and pure result projection on one connection.</summary>
internal static class TradePipelineAtomicPersistence
{
    internal static async ValueTask<TEvent> CommitAsync<TEvent>(ICommand request, TEvent candidate, IEventSourceActorDbContext source, IDbContextFactory? factory, IPostgresEventTransaction? transactions, CancellationToken token)
        where TEvent : class, IEvent
    {
        var target = factory?.TradeDb ?? throw new InvalidOperationException("Atomic pipeline persistence requires TradeDb.");
        if (!string.Equals(source.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) || !string.Equals(target.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) || PostgresDatabaseIdentity.Parse(source.ConnectionString) != PostgresDatabaseIdentity.Parse(target.ConnectionString))
            throw new InvalidOperationException("Pipeline source and projection must share the same PostgreSQL database/security identity.");
        var owner = transactions ?? throw new InvalidOperationException("Atomic pipeline persistence requires the PostgreSQL transaction service.");
        return await owner.ExecuteOnceAsync(async (db, ct) =>
        {
            await db.ReserveCommandAsync(request, ct).ConfigureAwait(false);
            await db.AppendAsync(request.StreamId, request.CommandId, candidate, 0, ct).ConfigureAwait(false);

            switch (candidate)
            {
                case RegimeDiscoveryPipelineCompletedEvent regime:
                    await TradeDbContextExtensions.RegimeProjectAsync(db, regime, ct).ConfigureAwait(false);
                    break;
                case OrderCompositionFunctionCompletedEvent composed:
                    await TradeDbContextExtensions.PipelineProjectAsync(db, composed, ct).ConfigureAwait(false);
                    break;
                case TradeSelectionFunctionCompletedEvent selected:
                    await TradeDbContextExtensions.PipelineProjectAsync(db, selected, ct).ConfigureAwait(false);
                    break;
                case MarketConditionAssessmentCompletedEvent assessed:
                    await TradeDbContextExtensions.PipelineProjectAsync(db, assessed, ct).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported atomic pipeline result: " + candidate.GetType().Name);
            }

            return candidate;
        }, token).ConfigureAwait(false);
    }
}
