// LEGACY: retained for migration/replay and UI comparison only. Active authoring uses ConfigurationDb.
// Removal criteria: Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md.
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Idempotently installs typed definitions, preserving identities from the read-only legacy catalog.</summary>
public sealed class TradeStrategyFamilyBootstrapper(IReferenceDbContext db, ISequenceIdGenerator sequenceIds)
{
    static readonly SemaphoreSlim BootstrapLock = new(1, 1);

    public async Task<IReadOnlyList<TradeStrategyFamilyReadModel>> EnsureV1Async(CancellationToken cancellationToken = default)
    {
        await BootstrapLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await db.GetTradeStrategyFamiliesAsync(cancellationToken).ConfigureAwait(false);
            var legacy = await db.GetLegacyTradeStrategyFamiliesAsync(cancellationToken).ConfigureAwait(false);
            if (legacy.Select(x => x.SystemKey).Distinct(StringComparer.Ordinal).Count() != legacy.Count ||
                legacy.Select(x => x.TradeStrategyFamilyId).Distinct().Count() != legacy.Count ||
                legacy.Any(x => x.TradeStrategyFamilyId <= 0 || x.DefinitionVersion != 1 || x.State != TradeStrategyFamilyState.Active ||
                    x.CreatedOnUtc == default || x.CreatedOnUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(x.CreatedBy) ||
                    !TradeStrategyFamilySeed.Definitions.Any(d => d.LegacySystemKey == x.SystemKey)))
                throw new InvalidOperationException("Legacy family catalog is non-canonical; migration requires review.");
            foreach (var definition in TradeStrategyFamilySeed.Definitions)
            {
                var old = legacy.SingleOrDefault(x => x.SystemKey == definition.LegacySystemKey);
                var current = existing.SingleOrDefault(x => x.TradeStrategySymbolId == 0 && x.DefinitionVersion == 1 && x.SystemKey == definition.SystemKey);
                if (current is not null)
                {
                    if (old is not null && (current.TradeStrategyFamilyId != old.TradeStrategyFamilyId || current.DefinitionVersion != old.DefinitionVersion))
                        throw new InvalidOperationException("Typed and legacy family identities conflict; no references were rewritten.");
                    continue;
                }
                var id = old?.TradeStrategyFamilyId ?? checked((int)await sequenceIds.GetSequenceIdAsync(SequenceName.Reference_TradeStrategyFamilyId, cancellationToken).ConfigureAwait(false));
                if (existing.Any(x => x.TradeStrategyFamilyId == id))
                    throw new InvalidOperationException("The family ID is already bound to another definition.");
                await db.InsertTradeStrategyFamilyAsync(definition.Create(id, old?.CreatedOnUtc ?? DateTime.UtcNow,
                    old?.CreatedBy ?? "ReferenceBootstrap"), cancellationToken).ConfigureAwait(false);
                existing = await db.GetTradeStrategyFamiliesAsync(cancellationToken).ConfigureAwait(false);
                if (old is not null && existing.Single(x => x.TradeStrategySymbolId == 0 && x.DefinitionVersion == 1 && x.SystemKey == definition.SystemKey).TradeStrategyFamilyId != old.TradeStrategyFamilyId)
                    throw new InvalidOperationException("Concurrent migration produced a conflicting family identity.");
            }
            TradeStrategyFamilySeed.Validate(existing.Where(x => x.TradeStrategySymbolId == 0 && x.DefinitionVersion == 1).ToArray());
            return existing.OrderBy(x => x.TradeStrategyFamilyId).ToArray();
        }
        finally { BootstrapLock.Release(); }
    }
}
