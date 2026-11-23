using System.Collections.Concurrent;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Strategy.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;

/// <summary>Shared immutable versioned configuration; only workflow identities/times are rebound on a hit.</summary>
public sealed class WorkflowSelectionConfigurationCache(TimeProvider? clock = null)
{
    readonly TimeProvider time = clock ?? TimeProvider.System;
    readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, Lazy<Task<Entry>>> loads = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, Model.PreparedTradeSelectionConfiguration> compiled = new(StringComparer.Ordinal);
    sealed record Entry(long Generation, DateTimeOffset LoadedAt, TradeSelectionBinding Template);

    internal Model.PreparedTradeSelectionConfiguration? TryGetCompiled(TradeSelectionBinding binding) =>
        compiled.GetValueOrDefault(Model.PreparedTradeSelectionConfiguration.Key(binding));

    internal void PublishCompiled(TradeSelectionBinding template)
    {
        var prepared = new Model.PreparedTradeSelectionConfiguration(template);
        if (compiled.Count >= 64) compiled.Clear();
        compiled.TryAdd(Model.PreparedTradeSelectionConfiguration.Key(template), prepared);
    }

    /// <summary>Live admission only: acquire a published generation without starting or awaiting a load.</summary>
    public bool TryResolveReady(IConfigurationDbContext database, IntrinsicTimeStrategyWorkflowOptions options,
        IntrinsicTimeStrategyWorkflowView view, out TradeSelectionBinding? binding)
    {
        binding = null;
        if (!TryFindReady(database, options, view, out var entry)) return false;
        binding = Bind(entry!.Template, view);
        WorkflowReadCacheDiagnostics.Log.ConfigurationReuse();
        return true;
    }

    /// <summary>Background readiness probe; does not bind/hash a throwaway workflow on each timer tick.</summary>
    public bool IsPrepared(IConfigurationDbContext database, IntrinsicTimeStrategyWorkflowOptions options,
        IntrinsicTimeStrategyWorkflowView view) => TryFindReady(database, options, view, out var entry)
            && time.GetUtcNow() - entry!.LoadedAt < TimeSpan.FromSeconds(50);

    internal TradeSelectionBinding? PreparedTemplate(IConfigurationDbContext database, IntrinsicTimeStrategyWorkflowOptions options,
        IntrinsicTimeStrategyWorkflowView view) => TryFindReady(database, options, view, out var entry) ? entry!.Template : null;

    bool TryFindReady(IConfigurationDbContext database, IntrinsicTimeStrategyWorkflowOptions options,
        IntrinsicTimeStrategyWorkflowView view, out Entry? entry)
    {
        entry = null;
        var reference = options.Activations.SingleOrDefault(x => x.Horizon == view.TriggerEvent.EntityId.TimePeriod);
        if (reference is null || !entries.TryGetValue(CacheKey(database, reference, view), out var ready)) return false;
        if (ready.Generation != ReadCacheInvalidation.Configuration || time.GetUtcNow() - ready.LoadedAt >= TimeSpan.FromMinutes(1)
            || ready.Template.FrozenAtUtc > view.StartedAtUtc || ready.Template.ValidUntilUtc <= view.StartedAtUtc) return false;
        entry = ready;
        return true;
    }

    static string CacheKey(IConfigurationDbContext database, WorkflowActivationReference reference, IntrinsicTimeStrategyWorkflowView view) =>
        $"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(database)}/{reference.Id:N}/{reference.Version}/{reference.PayloadSha256}/{view.EntityId.Assignment?.Deployment}";

    /// <summary>Background preparation or explicit offline replay only. Readers retain the old generation while refresh runs.</summary>
    public async Task<TradeSelectionBinding> ResolveAsync(IConfigurationDbContext database,
        IStrategyDeploymentReader deployments, IntrinsicTimeStrategyWorkflowOptions options,
        IntrinsicTimeStrategyWorkflowView view, CancellationToken token = default)
    {
        var reference = options.Activations.SingleOrDefault(x => x.Horizon == view.TriggerEvent.EntityId.TimePeriod)
            ?? throw new InvalidOperationException("TS.INIT.ACTIVATION_MISSING");
        var key = CacheKey(database, reference, view);
        if (IsPrepared(database, options, view) && TryResolveReady(database, options, view, out var cached)) return cached!;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var generation = ReadCacheInvalidation.Configuration;
            foreach (var expired in entries.Where(value => value.Value.Generation != generation).Select(value => value.Key))
                entries.TryRemove(expired, out _);
            if (entries.Count >= 64 && !entries.ContainsKey(key)) throw new InvalidOperationException("TS.CONFIG.CACHE_CAPACITY");
            var pending = loads.GetOrAdd(key, _ => new(() => LoadAsync(generation), LazyThreadSafetyMode.ExecutionAndPublication));
            Entry entry;
            try { entry = await pending.Value.WaitAsync(token).ConfigureAwait(false); }
            finally { if (pending.IsValueCreated && pending.Value.IsCompleted) loads.TryRemove(new(key, pending)); }
            if (entry.Generation != ReadCacheInvalidation.Configuration || entry.Template.FrozenAtUtc > view.StartedAtUtc
                || entry.Template.ValidUntilUtc <= view.StartedAtUtc) continue;
            entries[key] = entry;
            return Bind(entry.Template, view);
        }
        throw new InvalidOperationException("TS.CONFIG.CHANGED_DURING_CAPTURE");

        async Task<Entry> LoadAsync(long generation)
        {
            var at = view.StartedAtUtc;
            var activation = await database.GetEffectiveTradeSelectionActivationAsync(reference.Id, reference.Version, reference.PayloadSha256, at, token).ConfigureAwait(false);
            if (view.EntityId.Assignment is { } assignment)
            {
                if (!activation.DeploymentKeys.Contains(assignment.Deployment)) throw new InvalidOperationException("TS.CONFIG.ASSIGNMENT_DEPLOYMENT");
                activation = activation with { DeploymentKeys = [assignment.Deployment] };
            }
            var template = await new StrategySelectionUniverseResolver(database, deployments).ResolveAsync(activation,
                view.WorkflowId.Value, 1, view.CorrelationId, at, at.AddDays(1), DateOnly.FromDateTime(view.TriggerEvent.CreatedOn), token).ConfigureAwait(false);
            ValidateBinding(template);
            PublishCompiled(template);
            WorkflowReadCacheDiagnostics.Log.ConfigurationLoad();
            return new(generation, time.GetUtcNow(), template);
        }
    }

    public static TradeSelectionBinding Bind(TradeSelectionBinding template, IntrinsicTimeStrategyWorkflowView view)
    {
        var until = new[] { template.ValidUntilUtc, view.ExpiresAtUtc }.Min();
        var universe = template.StrategyUniverse! with { WorkflowId = view.WorkflowId.Value,
            WorkflowRevision = view.WorkflowRevision + 1, CorrelationId = view.CorrelationId,
            FrozenAtUtc = view.StartedAtUtc, ValidUntilUtc = until };
        universe = universe with { PayloadSha256 = EvidenceHash(universe with { PayloadSha256 = "" }) };
        var bound = Seal(template with { StrategyUniverse = universe, FrozenAtUtc = view.StartedAtUtc,
            ValidUntilUtc = until, RequestedTradeDate = DateOnly.FromDateTime(view.TriggerEvent.CreatedOn),
            DeploymentSnapshots = template.DeploymentSnapshots.Select(x => x with { AsOfUtc = view.StartedAtUtc }).ToArray() });
        ValidateBinding(bound);
        return bound;
    }
}
