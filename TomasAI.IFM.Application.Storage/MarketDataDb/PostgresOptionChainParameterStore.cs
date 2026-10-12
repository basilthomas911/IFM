using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Materializes accepted global option-chain policies on the owning source-event transaction.</summary>
public static class PostgresOptionChainParameterStore
{
    /// <summary>Preserves immutable version content and advances the current revision without invoking providers.</summary>
    public static async Task ProjectAsync(IEnlistedPostgresTransaction transaction, StrategyOptionChainParameterSet parameters,
        long revision, bool published, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        parameters.Validate();
        var payload = parameters.Serialize();
        await transaction.ExecuteAsync("INSERT INTO market_data.strategy_option_chain_parameter_version(set_id,version,payload) VALUES($1,$2,$3) ON CONFLICT(set_id,version) DO NOTHING;",
            Values(Uuid(parameters.ParameterSetId), Integer(parameters.Version), Text(payload)), cancellationToken)
            .ConfigureAwait(false);
        var stored = await transaction.ScalarAsync("SELECT payload FROM market_data.strategy_option_chain_parameter_version WHERE set_id=$1 AND version=$2;",
            Values(Uuid(parameters.ParameterSetId), Integer(parameters.Version)), cancellationToken)
            .ConfigureAwait(false);
        if (stored is not string content || !string.Equals(StrategyOptionChainParameterSet.Read(content).Hash(), parameters.Hash(), StringComparison.Ordinal))
            throw new InvalidDataException("An immutable option-chain parameter version has conflicting content.");
        await transaction.ExecuteAsync("INSERT INTO market_data.strategy_option_chain_parameter_current(environment,set_id,version,revision,published,payload) VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT(environment,set_id,version) DO UPDATE SET revision=EXCLUDED.revision,published=EXCLUDED.published,payload=EXCLUDED.payload WHERE strategy_option_chain_parameter_current.revision < EXCLUDED.revision;",
            Values(Text(parameters.Environment), Uuid(parameters.ParameterSetId), Integer(parameters.Version), Bigint(revision), Boolean(published), Text(payload)), cancellationToken)
            .ConfigureAwait(false);
    }
}
