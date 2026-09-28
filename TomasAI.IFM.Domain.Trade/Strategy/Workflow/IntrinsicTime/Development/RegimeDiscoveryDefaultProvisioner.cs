using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;

/// <summary>Creates missing baseline Regime Discovery profiles required by Development workflows.</summary>
public sealed class RegimeDiscoveryDefaultProvisioner(IConfigurationDbContext configurationDb)
{
    static readonly TimeFrameType[] Horizons =
        [TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly];

    /// <summary>
    /// Ensures an effective Regime Discovery profile for every supported strategy horizon while preserving
    /// effective authored profiles.
    /// </summary>
    public async Task<RegimeDiscoveryDefaultProvisioningResult> EnsureAsync(
        DateTime effectiveAtUtc,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        if (effectiveAtUtc == default || effectiveAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The default Regime Discovery effective timestamp must be UTC.", nameof(effectiveAtUtc));

        var existing = 0;
        var published = 0;
        foreach (var horizon in Horizons)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await configurationDb.GetEffectiveRegimeDiscoveryAsync(effectiveAtUtc, horizon, cancellationToken)
                    .ConfigureAwait(false) is not null)
            {
                existing++;
                continue;
            }

            var parameterSet = RegimeDiscoveryParameterSet.CreateDefault(
                DefaultId("parameter-set", horizon),
                DefaultId("strategy", horizon),
                horizon);
            var exact = await configurationDb.GetRegimeDiscoveryAsync(
                    parameterSet.ParameterSetId, parameterSet.Version, cancellationToken)
                .ConfigureAwait(false);
            var expectedHash = RegimeDiscoveryParameterPayload.ComputeSha256(parameterSet);
            if (exact is null)
            {
                await configurationDb.InsertRegimeDiscoveryDraftAsync(
                        parameterSet,
                        "Development default Regime Discovery parameters.",
                        createdBy,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (!string.Equals(exact.PayloadSha256, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Development Regime Discovery default identity collision for {horizon}.");
            }

            await configurationDb.PublishAsync(
                    StrategyParameterSetKind.RegimeDiscovery,
                    parameterSet.ParameterSetId,
                    parameterSet.Version,
                    effectiveAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            published++;
        }

        return new(existing, published);
    }

    static Guid DefaultId(string purpose, TimeFrameType horizon)
    {
        var key = $"IFM.Development.RegimeDiscovery.v1|{purpose}|{horizon}";
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
    }
}

/// <summary>Reports the actions performed while ensuring Development Regime Discovery defaults.</summary>
public sealed record RegimeDiscoveryDefaultProvisioningResult(
    int ExistingProfiles,
    int PublishedProfiles);
