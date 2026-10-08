using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Pricing;

/// <summary>Reads qualified valuation evidence from already-owned exact option scopes; it never opens a subscription.</summary>
public sealed class IndividualOptionRiskReader(ICompositionMarketDataApi market, DatasetWorkerAdmissionRegistry admissions, ILogger<IndividualOptionRiskReader>? logger = null)
{
    readonly ConcurrentDictionary<Guid, WorkerOptionChainRequest> scopes = new();
    readonly ConcurrentDictionary<string, DateTimeOffset> unavailableReportedAt = new(StringComparer.Ordinal);

    /// <summary>Retains a provider-acknowledged scope for read-only risk capture.</summary>
    /// <param name="lease">The acknowledged exact-option lease.</param>
    public void Register(WorkerOptionChainRequest lease) => scopes[lease.LeaseId] = lease;

    /// <summary>Removes only the released owner scope.</summary>
    /// <param name="leaseId">The owner lease identity.</param>
    public void Remove(Guid leaseId) => scopes.TryRemove(leaseId, out _);

    /// <summary>Checks ownership of all exact leg scopes without opening subscriptions or requesting valuations.</summary>
    /// <param name="contractIds">The four monitored contracts.</param><param name="valueDate">The active exchange session.</param>
    /// <returns>True only when all contracts have retained scopes in the current admitted dataset generation.</returns>
    public bool HasScopes(IEnumerable<string> contractIds, DateOnly valueDate)
    {
        if (!admissions.TryGet("GLBX.MDP3", out var admitted) || admitted.ValueDate != valueDate) return false;
        return contractIds.All(contractId => scopes.Values.Any(value => value.ValueDate == valueDate
            && value.GenerationId == admitted.GenerationId && value.Options.Length == 1
            && value.Options[0].Pricing.Contract.ContractId == contractId));
    }

    /// <summary>Captures one qualified option valuation from the current admitted worker generation.</summary>
    /// <param name="contractId">The exact persisted contract identity.</param>
    /// <param name="valueDate">The required exchange session.</param>
    /// <param name="token">The bounded read deadline.</param>
    /// <returns>The immutable pricing evidence, or null when scope, generation or quote qualification is unavailable.</returns>
    public async Task<MarketCompositionSnapshot?> CaptureAsync(string contractId, DateOnly valueDate, CancellationToken token)
    {
        if (!admissions.TryGet("GLBX.MDP3", out var admitted) || admitted.ValueDate != valueDate) return null;
        var lease = scopes.Values.FirstOrDefault(value => value.ValueDate == valueDate && value.GenerationId == admitted.GenerationId
            && value.Options.Length == 1 && value.Options[0].Pricing.Contract.ContractId == contractId);
        if (lease is null) return null;
        var now = DateTimeOffset.UtcNow;
        // The worker freezes observations before selecting its valuation instant. A host timestamp
        // chosen before the pipe round trip incorrectly rejects newly received live quotes.
        var result = await market.CaptureAsync("GLBX.MDP3", new CompositionSnapshotRequest(Guid.NewGuid(), lease.ScopeId,
            "Daily", admitted.GenerationId, default, now.AddSeconds(3), true, MaximumContracts: 1,
            MaximumQuoteAgeMilliseconds: 5000, MaximumQuoteSkewMilliseconds: 1000), token).ConfigureAwait(false);
        if (result.Failure is null && result.Snapshot is { } snapshot && snapshot.GenerationId == admitted.GenerationId
            && snapshot.ValidUntilUtc > DateTimeOffset.UtcNow && snapshot.Instruments.Length == 1
            && snapshot.Instruments[0].Instrument.ContractId == contractId && snapshot.Instruments[0].Valuation is not null)
        {
            return snapshot;
        }
        // At most one warning per contract per minute; no per-quote log or payload serialization.
        if (logger?.IsEnabled(LogLevel.Warning) == true
            && (!unavailableReportedAt.TryGetValue(contractId, out var last) || now - last >= TimeSpan.FromMinutes(1)))
        {
            unavailableReportedAt[contractId] = now;
            logger.LogWarning("Individual option risk unavailable; MethodName={MethodName} ContractId={ContractId} ValueDate={ValueDate} GenerationId={GenerationId} ScopeId={ScopeId} FailureCode={FailureCode} Input={Input} Detail={Detail}",
                nameof(CaptureAsync), contractId, valueDate, admitted.GenerationId, lease.ScopeId,
                result.Failure?.Code ?? "UnqualifiedSnapshot", result.Failure?.Input ?? "Valuation", result.Failure?.Detail ?? "The returned scope did not contain current qualified risk evidence.");
        }
        return null;
    }
}
