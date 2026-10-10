using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Framework.Storage.Extensions;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public partial class MarketDataDbContext
{
    /// <summary>Projects a committed configuration snapshot into immutable version and monotonic current tables.</summary>
    public async Task ProjectAsync(StrategyOptionChainParameterSet parameters, long revision, bool published, CancellationToken token)
    {
        parameters.Validate(); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        var payload = parameters.Serialize();
        await Database.Use("OptionChainParameter.Version", "INSERT INTO strategy_option_chain_parameter_version (set_id,version,payload) VALUES (:id,:version,:payload) IF NOT EXISTS;")
            .SetParameters(new CompositionPreparationParameters([parameters.ParameterSetId, parameters.Version, payload])).ExecuteCommandAsync(token).ConfigureAwait(false);
        var committed = await ReadVersionAsync(parameters.ParameterSetId, parameters.Version, token).ConfigureAwait(false);
        if (committed is null || committed.Hash() != parameters.Hash())
            throw new InvalidDataException("An immutable option-chain parameter version has conflicting content.");
        var identity = new CompositionPreparationParameters([parameters.Environment, parameters.ParameterSetId, parameters.Version, revision, published, payload]);
        await Database.Use("OptionChainParameter.Current.Insert", "INSERT INTO strategy_option_chain_parameter_current (environment,set_id,version,revision,published,payload) VALUES (:environment,:id,:version,:revision,:published,:payload) IF NOT EXISTS;")
            .SetParameters(identity).ExecuteCommandAsync(token).ConfigureAwait(false);
        await Database.Use("OptionChainParameter.Current.Advance", "UPDATE strategy_option_chain_parameter_current SET revision=:revision,published=:published,payload=:payload WHERE environment=:environment AND set_id=:id AND version=:version IF revision < :expected;")
            .SetParameters(new CompositionPreparationParameters([revision, published, payload, parameters.Environment, parameters.ParameterSetId, parameters.Version, revision]))
            .ExecuteCommandAsync(token).ConfigureAwait(false);
    }

    /// <summary>Queries persisted current published rows; parameter queries never load command streams.</summary>
    public async Task<ImmutableArray<StrategyOptionChainParameterSet>> ReadPublishedAsync(string environment, CancellationToken token)
    {
        if (environment is not ("Development" or "Paper" or "Production")) throw new ArgumentException("Invalid parameter environment.");
        var rows = await Database.Use("OptionChainParameter.Current.Read", "SELECT published,payload FROM strategy_option_chain_parameter_current WHERE environment=:environment LIMIT 129;")
            .SetParameters(new CompositionPreparationParameters([environment]))
            .ExecuteQueryAsync(row => (Published: row.GetBool(0), Parameters: StrategyOptionChainParameterSet.Read(row.GetString(1))), token).ConfigureAwait(false);
        if (rows.Count > 128) throw new InvalidDataException("Global strategy parameter capacity exceeded.");
        return rows.GroupBy(x => x.Parameters.ParameterSetId).Select(g => g.OrderByDescending(x => x.Parameters.Version).First())
            .Where(x => x.Published && x.Parameters.Enabled).Select(x => x.Parameters).OrderBy(x => x.ParameterSetId).ToImmutableArray();
    }

    /// <summary>Reads one persisted immutable parameter version.</summary>
    public async Task<StrategyOptionChainParameterSet?> ReadVersionAsync(Guid setId, int version, CancellationToken token)
    {
        if (setId == Guid.Empty || version < 1) throw new ArgumentException("Exact parameter identity is required.");
        return await Database.Use("OptionChainParameter.Version.Read", "SELECT payload FROM strategy_option_chain_parameter_version WHERE set_id=:id AND version=:version;")
            .SetParameters(new CompositionPreparationParameters([setId, version]))
            .ExecuteSingleAsync(row => StrategyOptionChainParameterSet.Read(row.GetString(0)), token).ConfigureAwait(false);
    }
}
