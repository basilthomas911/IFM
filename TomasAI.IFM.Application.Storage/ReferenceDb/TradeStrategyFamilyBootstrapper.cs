using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Idempotently installs the bounded read-only v1 family catalog.</summary>
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
                if (existing.Any(x => string.Equals(x.SystemKey, definition.SystemKey, StringComparison.Ordinal))) continue;
                var id = checked((int)await sequenceIds.GetSequenceIdAsync(SequenceName.Reference_TradeStrategyFamilyId, cancellationToken).ConfigureAwait(false));
                await db.InsertTradeStrategyFamilyAsync(new TradeStrategyFamilyReadModel
                {
                    TradeStrategyFamilyId = id,
                    DefinitionVersion = 1,
                    SystemKey = definition.SystemKey,
                    Name = definition.Name,
                    State = TradeStrategyFamilyState.Active,
                    CreatedOnUtc = DateTime.UtcNow,
                    CreatedBy = "ReferenceBootstrap",
                }, cancellationToken).ConfigureAwait(false);
                existing = await db.GetTradeStrategyFamiliesAsync(cancellationToken).ConfigureAwait(false);
            }
            TradeStrategyFamilySeed.Validate(existing);
            return existing.OrderBy(x => x.TradeStrategyFamilyId).ToArray();
        }
        finally { BootstrapLock.Release(); }
    }
}
