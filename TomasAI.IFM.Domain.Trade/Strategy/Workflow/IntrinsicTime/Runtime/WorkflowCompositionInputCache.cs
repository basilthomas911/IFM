using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>Configuration and reconstruction routes are prepared by the producer. Decisions only copy resident quotes.</summary>
public sealed class WorkflowCompositionInputCache(IMarketDataApi market, ICompositionRoutePlanStore routes,
    DatasetWorkerAdmissionRegistry admissions, TimeProvider? timeProvider = null)
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly ConcurrentDictionary<string, Template> templates = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<CompositionPreparationKey, Capture> captures = new();
    readonly ConcurrentDictionary<string, (PreparedCompositionConfiguration Value, DateTime Until)> configurations = new(StringComparer.Ordinal);
    sealed record Template(SelectionConstructionPolicy Construction, CompositionMarketDataPlan? Futures, string? RouteId, bool ExactSignalContract);
    sealed record Capture(DateTime Until, Lazy<Task<CompositionPreparationResult>> Result);

    /// <summary>Background only. No workflow decision can start or await this operation.</summary>
    public async Task PrepareAsync(TradeSelectionBinding binding, CancellationToken token)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (configurations.Count >= 128)
            foreach (var old in configurations.Where(value => value.Value.Until <= now).Select(value => value.Key)) configurations.TryRemove(old, out _);
        foreach (var candidate in binding.Candidates)
        {
            if (!configurations.TryGetValue(candidate.CandidateHash, out var compiled))
            {
                if (configurations.Count >= 256) throw new InvalidOperationException("OC.CONFIG.CACHE_CAPACITY");
                var variant = binding.CatalogDefinitions.Single(value => value.Key == candidate.VariantKey);
                var intent = new SelectionCandidateIntent { CandidateHash = candidate.CandidateHash, AssignmentVersion = candidate.AssignmentVersion,
                    DeploymentKey = candidate.DeploymentKey, StrategyKey = candidate.StrategyKey, StructureKey = candidate.StructureKey,
                    VariantKey = candidate.VariantKey, Product = candidate.Product, Side = variant.Side, Bias = variant.Bias, PremiumMode = variant.PremiumMode,
                    SelectionPolicyReference = candidate.SelectionPolicyReference, CompositionPolicyReference = candidate.CompositionPolicyReference,
                    SpecializedParameterBindings = candidate.SpecializedParameterBindings, FamilyKeys = candidate.FamilyKeys };
                compiled = (new PreparedCompositionConfiguration(binding, intent), default);
            }
            configurations[candidate.CandidateHash] = (compiled.Value, new[] { binding.ValidUntilUtc, now.AddSeconds(60) }.Min());
        }
        foreach (var policy in binding.PipelinePolicies.Where(value => value.Kind == CatalogPipelineParameterKind.OrderComposition))
        {
            if (templates.TryGetValue(policy.PayloadSha256, out var previous) && !previous.ExactSignalContract) continue;
            if (templates.Count >= 64 && previous is null) throw new InvalidOperationException("OC.CONFIG.CACHE_CAPACITY");
            var construction = previous?.Construction ?? SelectionConstructionPolicy.Read(policy.PayloadJson);
            CompositionMarketDataPlan? plan = null;
            string? routeId = null;
            var exactSignalContract = construction.SchemaVersion == 1 && binding.Candidates.Any(candidate =>
                candidate.CompositionPolicyReference.PayloadSha256 == policy.PayloadSha256
                && binding.CatalogDefinitions.Single(value => value.Key == candidate.StructureKey).Capabilities.Any(value => value.Role == "builder" && value.Code == "Future"));
            if (exactSignalContract)
            {
                var contracts = market.TryGetFuturesTermStructureContracts("ES", out var pair) ? new[] { pair.Front, pair.Back }
                    : market.TryGetOnTheRunFuturesContract("ES", out var front) ? [front] : [];
                contracts = contracts.Where(value => value.SchemaVersion >= 1 && value.Symbol == "ES"
                    && value.Dataset == "GLBX.MDP3" && value.LastTradingUtc is not null && value.MultiplierValue is > 0
                    && value.TickSize is > 0 && value.DefinitionDigest is { Length: 64 }).ToArray();
                if (contracts.Length == 0)
                    throw new CompositionMarketSourceException("OC.INPUT.FUTURE_METADATA_NOT_READY");
                var futures = contracts.Select(value => new CompositionFutureDefinition(value.ContractId, value.Symbol,
                    value.Dataset!, value.Exchange, value.Currency, value.LastTradingUtc!.Value,
                    value.MultiplierValue!.Value, value.TickSize!.Value, value.DefinitionDigest!)).ToImmutableArray();
                if (previous?.Futures is { } old && old.Futures.SequenceEqual(futures)) continue;
                var native = contracts.Select(value => new DatasetSubscriptionContract { DomainContractId = value.ContractId,
                    ProviderContractName = value.LocalSymbol, RootSymbol = value.Symbol, Dataset = "GLBX.MDP3",
                    AssetTypeId = TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures }).ToImmutableArray();
                var route = new CompositionRoutePlan(1, "", "GLBX.MDP3", default, [], futures, native, null, null, null).Seal();
                route.Validate();
                await routes.SaveAsync(route, token).ConfigureAwait(false);
                routeId = route.PlanId;
                plan = new() { Dataset = "GLBX.MDP3", Root = "ES", ScopeComplete = true, Futures = futures };
            }
            if (construction.MarketData is { } data)
            {
                plan = data.Deserialize<CompositionMarketDataPlan>() ?? throw new InvalidDataException("OC.CONFIG.MARKET_PLAN");
                if (plan.IncludeOptions) throw new InvalidDataException("OC.CONFIG.OPTION_CACHE_REQUIRED");
                var backend = await market.GetBackendReadSnapshotAsync(token).ConfigureAwait(false);
                var definitions = backend.Contracts.ToDictionary(value => value.ContractId, StringComparer.Ordinal);
                var native = plan.Futures.Select(value =>
                {
                    if (!definitions.TryGetValue(value.ContractId, out var contract)) throw new CompositionMarketSourceException("UnderlyingRouteUnavailable");
                    return new DatasetSubscriptionContract { DomainContractId = contract.ContractId, ProviderContractName = contract.LocalSymbol,
                        RootSymbol = contract.Symbol, Dataset = plan.Dataset,
                        AssetTypeId = TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.AssetTypeId.Futures };
                }).OrderBy(value => value.DomainContractId, StringComparer.Ordinal).ToImmutableArray();
                var route = new CompositionRoutePlan(1, "", plan.Dataset, default, [],
                    plan.Futures.OrderBy(value => value.ContractId, StringComparer.Ordinal).ToImmutableArray(), native, null, null, null).Seal();
                route.Validate();
                await routes.SaveAsync(route, token).ConfigureAwait(false);
                routeId = route.PlanId;
            }
            templates[policy.PayloadSha256] = new(construction, plan, routeId, exactSignalContract);
        }
    }

    internal PreparedCompositionConfiguration? TryGetConfiguration(string candidateHash) =>
        configurations.TryGetValue(candidateHash, out var compiled) && compiled.Until > clock.GetUtcNow().UtcDateTime ? compiled.Value : null;

    public Task<CompositionPreparationResult> CaptureAsync(IntrinsicTimeStrategyWorkflowView view, DateOnly valueDate)
    {
        var key = CompositionPreparationAcceptance.Key(view);
        if (captures.Count >= 256)
            foreach (var old in captures.Where(value => value.Value.Until <= clock.GetUtcNow().UtcDateTime).Select(value => value.Key)) captures.TryRemove(old, out _);
        if (captures.Count >= 512 && !captures.ContainsKey(key)) return Task.FromResult(Failed("OC.INPUT.CACHE_CAPACITY"));
        return captures.GetOrAdd(key, _ => new(view.ExpiresAtUtc,
            new(() => CaptureOnceAsync(view, key, valueDate), LazyThreadSafetyMode.ExecutionAndPublication))).Result.Value;
    }

    async Task<CompositionPreparationResult> CaptureOnceAsync(IntrinsicTimeStrategyWorkflowView view, CompositionPreparationKey key, DateOnly valueDate)
    {
        var selected = TradeSelectionContracts.ReadResult(view.TradeSelection.Result!).SelectedCandidate!;
        var reference = selected.CompositionPolicyReference;
        if (!templates.TryGetValue(reference.PayloadSha256, out var template)) return Failed("OC.CONFIG.CACHE_NOT_READY");
        var deadline = new DateTimeOffset(WorkflowCalculationRuntime.EffectiveDeadline(view));
        if (clock.GetUtcNow() >= deadline) return Failed("OC.TIME.EXPIRED");
        if (template.Construction.CachePolicy(selected.StructureKey) is { } cached)
        {
            var bias = selected.Bias == "Balanced" ? "Neutral" : selected.Bias;
            var horizon = view.TriggerEvent.EntityId.TimePeriod.ToString();
            var scope = Application.MarketData.OptionChainCache.StrategyOptionChainScope.Key(cached.ParameterSetId, cached.Version, valueDate, bias, horizon);
            return CompositionPreparationService.CaptureCached(key, market.OptionChainCache,
                new(scope, Guid.Empty, valueDate, horizon, deadline, cached.MaximumQuoteAgeMilliseconds, cached.MaximumQuoteSkewMilliseconds)
                { ConfigurationDigest = cached.ConfigurationDigest, StrategyDefinitionId = selected.StructureKey.Id,
                    StrategyDefinitionVersion = selected.StructureKey.Version, InstrumentRoot = selected.Product.Symbol,
                    WorkflowId = view.WorkflowId.Value, WorkflowRevision = view.WorkflowRevision }, clock);
        }
        var plan = template.Futures;
        if (plan is null || template.RouteId is null || plan.Root != selected.Product.Symbol) return Failed("CompositionUniverseUnqualified");
        if (template.ExactSignalContract)
        {
            var exact = plan.Futures.Where(value => value.ContractId == view.TriggerEvent.EntityId.ContractId).ToImmutableArray();
            if (exact.Length != 1) return Failed("OC.INPUT.EXACT_FUTURE_NOT_READY");
            plan = plan with { Futures = exact };
        }
        if (!admissions.TryGet(plan.Dataset, out var admission) || admission.ValueDate != valueDate) return Failed("Recovering");
        var instruments = ImmutableArray.CreateBuilder<CompositionMarketInstrument>(plan.Futures.Length);
        foreach (var definition in plan.Futures)
        {
            var health = market.GetFuturesMarketHealth(definition.ContractId);
            if (!health.Running || !health.Healthy || health.ValueDate != valueDate
                || health.Generation != $"{admission.Dataset}|{admission.WorkerInstanceId:N}|{admission.GenerationId:N}|{admission.ManifestRevision}")
                return Failed("Recovering");
            if (!market.TryGetLastTickPrice(definition.ContractId, out var tick) || tick.ValueDate != valueDate
                || tick.Quote is not { BidPrice: { } bid, AskPrice: { } ask } quote) return Failed("QuoteUnavailable");
            instruments.Add(new(definition.ContractId, new OptionPricingQuote(definition.ContractId, bid, ask,
                quote.BidSize, quote.AskSize, quote.EventTimestamp, quote.ReceiveTimestamp, quote.SourceSequence, admission.GenerationId),
                null, null, null, null, definition));
        }
        var request = new CompositionSnapshotRequest(Guid.NewGuid(), template.RouteId, view.TriggerEvent.EntityId.TimePeriod.ToString(),
            admission.GenerationId, clock.GetUtcNow(), deadline, false, Futures: plan.Futures);
        var page = new CompositionMarketPage(template.RouteId, admission.GenerationId, instruments.Count, instruments.MoveToImmutable(), null);
        var result = await new MarketCompositionSnapshotProvider(new FrozenSource(page), clock).CaptureAsync(request, default).ConfigureAwait(false);
        if (!admissions.TryGet(plan.Dataset, out var current) || current != admission) return Failed("Recovering");
        if (result.Snapshot is not { } snapshot) return new(null, result.Failure);
        var prepared = new CompositionPreparation(2, key, plan.Dataset, request, snapshot, clock.GetUtcNow(), "");
        prepared = prepared with { Digest = PricingSemanticHash.Compute(prepared) };
        CompositionPreparationService.Validate(prepared);
        return new(prepared, null);
    }

    sealed class FrozenSource(CompositionMarketPage page) : ICompositionMarketSource
    {
        public Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request, string? continuation, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(page); }
    }
    static CompositionPreparationResult Failed(string code) => new(null, new(code, "PreparedComposition", "", "Required resident input is unavailable."));
}
