using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>Stores immutable strategy configuration versions in PostgreSQL.</summary>
public sealed class ConfigurationDbContext(
    IDbConnectionSettings connectionSettings,
    IDbContextFactory dbFactory,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<ConfigurationDbContext>(connectionSettings[ConfigurationDbConnection], logger),
      IConfigurationDbContext
{
    /// <summary>Gets the configuration connection setting name.</summary>
    public const string ConfigurationDbConnection = "ConfigurationDbConnection";

    /// <inheritdoc />
    public override ConfigurationDbContext Database => this;

    /// <inheritdoc />
    public async Task InsertRegimeDiscoveryDraftAsync(
        RegimeDiscoveryParameterSet parameterSet,
        string description,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameterSet);
        var validation = new RegimeDiscoveryParameterSetValidationRules().Execute(parameterSet);
        if (validation.Length != 0)
            throw new ArgumentException(string.Join("; ", validation.Select(value => value.ErrorMessage)),
                nameof(parameterSet));
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        var payload = RegimeDiscoveryParameterPayload.Serialize(parameterSet);
        await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertDraft)}",
                ConfigurationDbSql.InsertDraft)
            .SetParameters(new InsertConfigurationDraft(
                parameterSet.ParameterSetId,
                parameterSet.Version,
                checked((short)parameterSet.SchemaVersion),
                (short)ConfigurationParameterSetStatus.Draft,
                payload,
                RegimeDiscoveryParameterPayload.ComputeSha256(payload),
                description ?? string.Empty,
                DateTime.UtcNow,
                createdBy))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task InsertMarketConditionDraftAsync(
        MarketConditionParameterSet parameterSet,
        string description,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameterSet);
        var validation = new MarketConditionParameterSetValidationRules().Execute(parameterSet);
        if (validation.Length != 0)
            throw new ArgumentException(string.Join("; ", validation.Select(value => value.ErrorMessage)), nameof(parameterSet));
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        var payload = MarketConditionParameterPayload.Serialize(parameterSet);
        await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertMarketConditionDraft)}",
                ConfigurationDbSql.InsertMarketConditionDraft)
            .SetParameters(new InsertConfigurationDraft(parameterSet.ParameterSetId, parameterSet.Version,
                checked((short)parameterSet.SchemaVersion), (short)ConfigurationParameterSetStatus.Draft,
                payload, MarketConditionParameterPayload.ComputeSha256(payload), description ?? string.Empty,
                DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        StrategyParameterSetKind kind,
        Guid parameterSetId,
        int version,
        DateTime effectiveFromUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateLifecycleArguments(parameterSetId, version, effectiveFromUtc, nameof(effectiveFromUtc));
        var sql = ConfigurationDbSql.PublishFor(kind);
        var affected = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.Publish.{kind}", sql)
            .SetParameters(new PublishConfiguration(
                (short)ConfigurationParameterSetStatus.Published, effectiveFromUtc,
                parameterSetId, version, (short)ConfigurationParameterSetStatus.Draft))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        EnsureSingleLifecycleTransition(affected, kind, parameterSetId, version, "publish", "Draft");
    }

    /// <inheritdoc />
    public async Task RetireAsync(
        StrategyParameterSetKind kind,
        Guid parameterSetId,
        int version,
        DateTime retiredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateLifecycleArguments(parameterSetId, version, retiredAtUtc, nameof(retiredAtUtc));
        var sql = ConfigurationDbSql.RetireFor(kind);
        var affected = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.Retire.{kind}", sql)
            .SetParameters(new RetireConfiguration(
                (short)ConfigurationParameterSetStatus.Retired, retiredAtUtc,
                parameterSetId, version, (short)ConfigurationParameterSetStatus.Published))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        EnsureSingleLifecycleTransition(affected, kind, parameterSetId, version, "retire", "Published");
    }

    /// <inheritdoc />
    public async Task<ResolvedRegimeDiscoveryParameterSet?> GetRegimeDiscoveryAsync(
        Guid parameterSetId,
        int version,
        CancellationToken cancellationToken = default)
    {
        var row = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetExact)}", ConfigurationDbSql.GetExact)
            .SetParameters(new GetConfiguration(parameterSetId, version))
            .ExecuteSingleAsync(Map, cancellationToken).ConfigureAwait(false);
        return row is null ? null : Resolve(row);
    }

    /// <inheritdoc />
    public async Task<ResolvedMarketConditionParameterSet?> GetMarketConditionAsync(
        Guid parameterSetId,
        int version,
        CancellationToken cancellationToken = default)
    {
        if (parameterSetId == Guid.Empty) throw new ArgumentException("Parameter-set identity is required.", nameof(parameterSetId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        var row = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetExactMarketCondition)}",
                ConfigurationDbSql.GetExactMarketCondition)
            .SetParameters(new GetConfiguration(parameterSetId, version))
            .ExecuteSingleAsync(MapMarketCondition, cancellationToken).ConfigureAwait(false);
        return row is null ? null : ResolveMarketCondition(row);
    }

    /// <inheritdoc />
    public async Task<ResolvedRegimeDiscoveryParameterSet?> ResolveEffectiveRegimeDiscoveryAsync(
        DateTime effectiveAtUtc,
        TimeFrameType targetHorizon,
        CancellationToken cancellationToken = default)
    {
        if (targetHorizon is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))
            throw new ArgumentOutOfRangeException(nameof(targetHorizon), targetHorizon,
                "Regime Discovery configuration is resolved only for Daily, Weekly, or Monthly horizons.");
        var rows = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.ResolveEffective)}",
                ConfigurationDbSql.ResolveEffective)
            .SetParameters(new ResolveConfiguration(
                (short)ConfigurationParameterSetStatus.Published, effectiveAtUtc, (short)targetHorizon))
            .ExecuteQueryAsync(Map, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 1 && rows.ElementAt(0).EffectiveFromUtc == rows.ElementAt(1).EffectiveFromUtc)
            throw new InvalidOperationException("Effective Regime Discovery parameter selection is ambiguous.");
        return rows.Count == 0 ? null : Resolve(rows.First());
    }

    /// <inheritdoc />
    public async Task<ResolvedMarketConditionParameterSet?> ResolveEffectiveMarketConditionAsync(
        DateTime effectiveAtUtc,
        int fundId,
        string instrumentRoot,
        TimeFrameType targetHorizon,
        CancellationToken cancellationToken = default)
    {
        if (effectiveAtUtc == default || effectiveAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Effective resolution timestamp must be a non-default UTC value.", nameof(effectiveAtUtc));
        if (fundId <= 0) throw new ArgumentOutOfRangeException(nameof(fundId));
        if (!string.Equals(instrumentRoot, "ES", StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(instrumentRoot));
        if (targetHorizon is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))
            throw new ArgumentOutOfRangeException(nameof(targetHorizon));
        var rows = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.ResolveEffectiveMarketCondition)}",
                ConfigurationDbSql.ResolveEffectiveMarketCondition)
            .SetParameters(new ResolveMarketConditionConfiguration((short)ConfigurationParameterSetStatus.Published,
                effectiveAtUtc, fundId, instrumentRoot, (short)targetHorizon))
            .ExecuteQueryAsync(MapMarketCondition, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 1)
            throw new InvalidOperationException("Effective Market Condition parameter selection is ambiguous.");
        return rows.Count == 0 ? null : ResolveMarketCondition(rows.First());
    }

    static ConfigurationParameterSet Map(IObjectDataRecord row) => new(
        StrategyParameterSetKind.RegimeDiscovery,
        row.GetGuid(0), row.GetInt(1), checked((short)row.GetInt(2)),
        (ConfigurationParameterSetStatus)row.GetInt(3),
        row.IsNull(4) ? null : row.GetDateTime(4),
        row.IsNull(5) ? null : row.GetDateTime(5),
        row.GetString(6), row.GetString(7), row.GetString(8), row.GetDateTime(9), row.GetString(10));

    static ConfigurationParameterSet MapMarketCondition(IObjectDataRecord row) => Map(row) with
        { Kind = StrategyParameterSetKind.MarketCondition };

    static ResolvedRegimeDiscoveryParameterSet Resolve(ConfigurationParameterSet row)
    {
        var typed = JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(row.PayloadJson)
            ?? throw new InvalidOperationException("Stored Regime Discovery configuration cannot be deserialized.");
        var canonicalPayload = RegimeDiscoveryParameterPayload.Serialize(typed);
        if (!string.Equals(row.PayloadSha256, RegimeDiscoveryParameterPayload.ComputeSha256(canonicalPayload),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stored Regime Discovery configuration hash is invalid.");
        var errors = new RegimeDiscoveryParameterSetValidationRules().Execute(typed);
        if (errors.Length != 0)
            throw new InvalidOperationException(string.Join("; ", errors.Select(value => value.ErrorMessage)));
        return new(typed, canonicalPayload, row.PayloadSha256,
            row.EffectiveFromUtc ?? DateTime.MinValue);
    }

    static ResolvedMarketConditionParameterSet ResolveMarketCondition(ConfigurationParameterSet row)
    {
        var typed = JsonSerializer.Deserialize<MarketConditionParameterSet>(row.PayloadJson)
            ?? throw new InvalidOperationException("Stored Market Condition configuration cannot be deserialized.");
        var canonicalPayload = MarketConditionParameterPayload.Serialize(typed);
        var canonicalHash = MarketConditionParameterPayload.ComputeSha256(canonicalPayload);
        if (!string.Equals(row.PayloadSha256, canonicalHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stored Market Condition configuration hash is invalid.");
        if (row.ParameterSetId != typed.ParameterSetId || row.Version != typed.Version ||
            row.SchemaVersion != typed.SchemaVersion)
            throw new InvalidOperationException("Stored Market Condition configuration identity or schema metadata is invalid.");
        var errors = new MarketConditionParameterSetValidationRules().Execute(typed);
        if (errors.Length != 0)
            throw new InvalidOperationException(string.Join("; ", errors.Select(value => value.ErrorMessage)));
        return new(typed, canonicalPayload, canonicalHash,
            row.EffectiveFromUtc ?? DateTime.MinValue);
    }

    static void ValidateLifecycleArguments(
        Guid parameterSetId,
        int version,
        DateTime timestampUtc,
        string timestampParameterName)
    {
        if (parameterSetId == Guid.Empty) throw new ArgumentException("Parameter-set identity is required.", nameof(parameterSetId));
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        if (timestampUtc == default || timestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Lifecycle timestamps must be non-default UTC values.", timestampParameterName);
    }

    static void EnsureSingleLifecycleTransition(
        IReadOnlyCollection<long> affectedRows,
        StrategyParameterSetKind kind,
        Guid parameterSetId,
        int version,
        string operation,
        string requiredState)
    {
        if (affectedRows.Count == 1 && affectedRows.Single() == 1)
            return;
        throw new InvalidOperationException(
            $"Cannot {operation} {kind} parameter set {parameterSetId:D} version {version}; " +
            $"exactly one {requiredState} version must exist.");
    }

}
