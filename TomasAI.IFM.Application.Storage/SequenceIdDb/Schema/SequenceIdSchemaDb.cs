using System.Globalization;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;

public sealed class SequenceIdSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<SequenceIdSchemaDb>(connectionSettings[SequenceIdDbContext.SequenceIdDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects = BuildDefinitions();

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;

    /// <summary>Checks sequence readability, allocation sizes and function signatures without allocating any IDs.</summary>
    public override async Task<int> ValidateTablesAsync(CancellationToken cancellationToken = default)
    {
        var functionsExist = await Use("Recovery.Validate.SequenceFunctions", SequenceIdDbSql.ValidateFunctions)
            .ExecuteScalarAsync(MapToSequenceExists, cancellationToken)
            .ConfigureAwait(false);
        if (!functionsExist) throw new InvalidOperationException("Sequence allocation functions are missing.");
        var sequences = Enum.GetNames<SequenceName>();
        foreach (var name in sequences)
        {
            // Enum names are checked-in identifiers; this reads sequence state and never calls nextval.
            _ = await Use("Recovery.Validate.Sequence." + name, string.Format(CultureInfo.InvariantCulture, SequenceIdDbSql.ReadSequenceValue, name))
            .ExecuteScalarAsync(MapToSequenceValue, cancellationToken)
            .ConfigureAwait(false);
        var preparedParameters1 = new GetNextSequenceId(name);
            var increment = await Use("Recovery.Validate.SequenceIncrement", SequenceIdDbSql.GetSequenceAllocationSize)
            .SetParameters(preparedParameters1)
            .ExecuteScalarAsync(MapToSequenceValue, cancellationToken)
            .ConfigureAwait(false);
            if (increment != SequenceIdSettings.AllocationSize)
                throw new InvalidOperationException("Sequence allocation increment differs: " + name);
        }
        return sequences.Length + 2;
    }


    /// <summary>Maps the schema-existence scalar without altering the sequence.</summary>
    static bool MapToSequenceExists(IObjectDataRecord row) => row.GetBool(0);

    /// <summary>Maps the persisted sequence scalar at ordinal zero.</summary>
    static long MapToSequenceValue(IObjectDataRecord row) => row.GetLong(0);

    static SchemaObjectDefinition[] BuildDefinitions() =>
    [
        new("fn_get_current_sequence_id",
            string.Format(CultureInfo.InvariantCulture, SequenceIdSchemaSql.CreateGetCurrentSequenceIdFunction, SequenceIdSettings.AllocationSize - 1),
            SequenceIdSchemaSql.DropGetCurrentSequenceIdFunction),
        new("fn_get_next_sequence_id", SequenceIdSchemaSql.CreateGetNextSequenceIdFunction, SequenceIdSchemaSql.DropGetNextSequenceIdFunction),
        .. Enum.GetNames<SequenceName>().Select(name => new SchemaObjectDefinition(
            name,
            string.Format(CultureInfo.InvariantCulture, SequenceIdSchemaSql.CreateSequence, name, SequenceIdSettings.AllocationSize),
            string.Format(CultureInfo.InvariantCulture, SequenceIdSchemaSql.DropSequence, name))),
    ];
}
