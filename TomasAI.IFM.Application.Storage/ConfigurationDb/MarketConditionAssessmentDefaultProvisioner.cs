using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>
/// Creates the baseline Market Condition Assessment profiles required by the realtime strategy workflow.
/// Existing authored profiles are preserved, while defaults created by this provisioner may follow a newer
/// effective Regime Discovery version.
/// </summary>
public sealed class MarketConditionAssessmentDefaultProvisioner(IConfigurationDbContext configurationDb)
{
    static readonly TimeFrameType[] Horizons =
        [TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly];

    /// <summary>Ensures one effective assessment profile for every supported strategy horizon.</summary>
    public async Task<MarketConditionAssessmentDefaultProvisioningResult> EnsureAsync(
        string marketProfileId,
        DateTime effectiveAtUtc,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketProfileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        if (effectiveAtUtc == default || effectiveAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The default assessment effective timestamp must be UTC.", nameof(effectiveAtUtc));

        var existing = 0;
        var published = 0;
        var replaced = 0;

        foreach (var horizon in Horizons)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var regime = await configurationDb.ResolveEffectiveRegimeDiscoveryAsync(
                    effectiveAtUtc, horizon, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Cannot provision Market Condition Assessment default '{marketProfileId}' for {horizon}: " +
                    "no effective Regime Discovery parameters exist for that horizon.");

            var current = await configurationDb.ResolveEffectiveMarketConditionAssessmentAsync(
                    effectiveAtUtc, marketProfileId, "ES", horizon, cancellationToken)
                .ConfigureAwait(false);

            if (current is not null && HasBinding(current.ParameterSet, regime))
            {
                existing++;
                continue;
            }

            if (current is not null && !IsProvisionedDefault(current.ParameterSet))
                throw new InvalidOperationException(
                    $"Published Market Condition Assessment profile '{marketProfileId}' for {horizon} is bound to " +
                    $"Regime Discovery {current.ParameterSet.HorizonProfile.RegimeProfileId}/v" +
                    $"{current.ParameterSet.HorizonProfile.RegimeProfileVersion}, while the effective version is " +
                    $"{regime.ParameterSet.ParameterSetId}/v{regime.ParameterSet.Version}. " +
                    "The incompatible profile was authored outside Development default provisioning and was not overwritten.");

            var parameterSet = MarketConditionAssessmentParameterSet.CreateDefault(
                marketProfileId,
                horizon,
                DefaultId(marketProfileId, horizon, regime.ParameterSet.ParameterSetId, regime.ParameterSet.Version),
                regime.ParameterSet.ParameterSetId,
                regime.ParameterSet.Version);
            var exact = await configurationDb.GetMarketConditionAssessmentAsync(
                    parameterSet.ParameterSetId, parameterSet.Version, cancellationToken)
                .ConfigureAwait(false);

            if (exact is null)
            {
                await configurationDb.InsertMarketConditionAssessmentDraftAsync(
                        parameterSet,
                        "Development default bound to the effective Regime Discovery parameters.",
                        createdBy,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                if (!string.Equals(exact.PayloadSha256, MarketConditionAssessmentHash.Parameters(parameterSet),
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Development assessment default identity collision for '{marketProfileId}' and {horizon}.");
                if (exact.Status != ConfigurationParameterSetStatus.Draft)
                    throw new InvalidOperationException(
                        $"Development assessment default '{marketProfileId}' for {horizon} already has lifecycle state " +
                        $"{exact.Status}, but it is not the effective profile.");
            }

            if (current is not null)
            {
                await configurationDb.RetireAsync(
                        StrategyParameterSetKind.MarketConditionAssessment,
                        current.ParameterSet.ParameterSetId,
                        current.ParameterSet.Version,
                        effectiveAtUtc,
                        cancellationToken)
                    .ConfigureAwait(false);
                replaced++;
            }

            await configurationDb.PublishAsync(
                    StrategyParameterSetKind.MarketConditionAssessment,
                    parameterSet.ParameterSetId,
                    parameterSet.Version,
                    effectiveAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
            published++;
        }

        return new(existing, published, replaced);
    }

    static bool HasBinding(
        MarketConditionAssessmentParameterSet assessment,
        ResolvedRegimeDiscoveryParameterSet regime) =>
        assessment.HorizonProfile.RegimeProfileId == regime.ParameterSet.ParameterSetId &&
        assessment.HorizonProfile.RegimeProfileVersion == regime.ParameterSet.Version;

    static bool IsProvisionedDefault(MarketConditionAssessmentParameterSet assessment) =>
        assessment.ParameterSetId == DefaultId(
            assessment.MarketProfileId,
            assessment.TargetHorizon,
            assessment.HorizonProfile.RegimeProfileId,
            assessment.HorizonProfile.RegimeProfileVersion);

    static Guid DefaultId(string marketProfileId, TimeFrameType horizon, Guid regimeId, int regimeVersion)
    {
        var key = $"IFM.Development.MarketConditionAssessment.v1|{marketProfileId}|ES|{horizon}|{regimeId:D}|{regimeVersion}";
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 16));
    }
}

/// <summary>Reports the actions performed while ensuring Development assessment defaults.</summary>
public sealed record MarketConditionAssessmentDefaultProvisioningResult(
    int ExistingProfiles,
    int PublishedProfiles,
    int ReplacedProfiles);
