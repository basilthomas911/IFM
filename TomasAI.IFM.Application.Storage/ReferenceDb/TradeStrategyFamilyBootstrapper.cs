using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Idempotently installs the authoritative typed trade-strategy-family definitions.</summary>
public sealed class TradeStrategyFamilyBootstrapper(IReferenceDbContext db, ISequenceIdGenerator sequenceIds)
{
    static readonly SemaphoreSlim BootstrapLock = new(1, 1);

    public async Task<IReadOnlyList<TradeStrategyFamilyReadModel>> EnsureV1Async(CancellationToken cancellationToken = default)
    {
        await BootstrapLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await db.GetTradeStrategyFamiliesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var definition in TradeStrategyFamilySeed.Definitions)
            {
                var current = existing.SingleOrDefault(x => x.TradeStrategySymbolId == 0 && x.DefinitionVersion == 1 && x.SystemKey == definition.SystemKey);
                if (current is not null)
                    continue;
                var id = checked((int)await sequenceIds.GetSequenceIdAsync(SequenceName.Reference_TradeStrategyFamilyId, cancellationToken).ConfigureAwait(false));
                if (existing.Any(x => x.TradeStrategyFamilyId == id))
                    throw new InvalidOperationException("The family ID is already bound to another definition.");
                await db.InsertTradeStrategyFamilyAsync(
                    definition.Create(id, DateTime.UtcNow, "ReferenceBootstrap"),
                    cancellationToken).ConfigureAwait(false);
                existing = await db.GetTradeStrategyFamiliesAsync(cancellationToken).ConfigureAwait(false);
            }
            TradeStrategyFamilySeed.Validate(existing.Where(x => x.TradeStrategySymbolId == 0 && x.DefinitionVersion == 1).ToArray());
            return existing.OrderBy(x => x.TradeStrategyFamilyId).ToArray();
        }
        finally { BootstrapLock.Release(); }
    }
}
