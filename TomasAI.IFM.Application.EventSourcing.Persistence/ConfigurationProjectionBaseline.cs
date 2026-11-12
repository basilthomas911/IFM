using TomasAI.IFM.Shared.EventSourcing;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Application.EventSourcing.Persistence;

/// <summary>Captures an approved offline configuration baseline and applies its accepted event.</summary>
public sealed class ConfigurationProjectionBaseline(IPostgresEventTransaction transactions,
    IConfigurationDbContext configuration, TimeProvider? clock = null)
{
    const string Stream = "ConfigurationProjectionBaseline";

    /// <summary>Appends an exact locked source image after the operator has drained source writers.</summary>
    public Task<ConfigurationProjectionBaselineEvent> CaptureAsync(bool sourceWritesQuiesced, CancellationToken cancellationToken = default)
    {
        if (!sourceWritesQuiesced)
            throw new ArgumentException("Stop source writers before configuration baseline capture.", nameof(sourceWritesQuiesced));
        return transactions.ExecuteOnceAsync(async (db, token) =>
        {
            var image = await configuration.DbReader.CaptureConfigurationBaselineAsync(db, token).ConfigureAwait(false);
            var source = new ConfigurationProjectionBaselineEvent
            {
                SchemaVersion = 2, Id = Guid.NewGuid(), CommandId = Guid.NewGuid(),
                ReceivedOn = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime,
                SourceCut = image.SourceCut, Tables = image.Tables.ToArray(),
                LookupSequenceLastValue = image.SequenceLast, LookupSequenceIsCalled = image.SequenceCalled
            };
            var revision = await db.ReadCommittedStreamRevisionAsync(Stream, token).ConfigureAwait(false);
            await db.AppendAsync(new EnlistedEventAppend(Stream, source.CommandId, source, revision), token).ConfigureAwait(false);
            await configuration.DbWriter.ClaimConfigurationBaselineAsync(db, source.Id, source.EventId, source.SourceCut, token)
                .ConfigureAwait(false);
            return source;
        }, cancellationToken);
    }

    /// <summary>Restores the latest committed baseline event from its persisted source bytes.</summary>
    public Task<ConfigurationProjectionBaselineEvent?> ReadLatestAsync(CancellationToken cancellationToken = default)
        => transactions.ExecuteOnceAsync(async (db, token) =>
            (await db.ReadLatestCommittedEventAsync(Stream, token).ConfigureAwait(false))
                ?.ToDomainEvent() as ConfigurationProjectionBaselineEvent, cancellationToken);

    /// <summary>Validates accepted baseline evidence before applying the exact rows transactionally.</summary>
    public Task MaterializeAsync(ConfigurationProjectionBaselineEvent source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SchemaVersion is not (1 or 2) || source.LookupSequenceLastValue < 1 || source.Id == Guid.Empty
            || source.EventId <= source.SourceCut
            || !configuration.DbReader.MatchesConfigurationBaselineLayout(source.SchemaVersion, source.Tables.Select(table => table.Name))
            || source.Tables.Any(table => !string.Equals(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(table.RowsJson))), table.Sha256, StringComparison.Ordinal)))
            throw new InvalidDataException("Committed complete configuration baseline and matching hashes required.");
        return transactions.ExecuteOnceAsync(async (db, token) =>
        {
            if (source.SchemaVersion == 1)
                await MarketDataSchemaDb.CreateLegacyOptionChainBaselineTablesAsync(db, token).ConfigureAwait(false);
            return await configuration.DbWriter.MaterializeConfigurationBaselineAsync(db, source, token).ConfigureAwait(false);
        }, cancellationToken);
    }
}
