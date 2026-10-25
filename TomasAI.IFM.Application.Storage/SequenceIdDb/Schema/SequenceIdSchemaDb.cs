using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
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
        var functionsExist = await Use("Recovery.Validate.SequenceFunctions", """
            SELECT to_regprocedure('public.fn_get_current_sequence_id(text)') IS NOT NULL
            AND to_regprocedure('public.fn_get_next_sequence_id(text)') IS NOT NULL;
            """).ExecuteScalarAsync(static row => row.GetBool(0), cancellationToken).ConfigureAwait(false);
        if (!functionsExist) throw new InvalidOperationException("Sequence allocation functions are missing.");
        var sequences = Enum.GetNames<SequenceName>();
        foreach (var name in sequences)
        {
            // Enum names are checked-in identifiers; this reads sequence state and never calls nextval.
            _ = await Use("Recovery.Validate.Sequence." + name, $"SELECT last_value FROM public.{name};")
                .ExecuteScalarAsync(static row => row.GetLong(0), cancellationToken).ConfigureAwait(false);
            var increment = await Use("Recovery.Validate.SequenceIncrement", SequenceIdDbSql.GetSequenceAllocationSize)
                .SetParameters(new GetNextSequenceId(name))
                .ExecuteScalarAsync(static row => row.GetLong(0), cancellationToken).ConfigureAwait(false);
            if (increment != SequenceIdSettings.AllocationSize)
                throw new InvalidOperationException("Sequence allocation increment differs: " + name);
        }
        return sequences.Length + 2;
    }

    static SchemaObjectDefinition[] BuildDefinitions()
    {
        var definitions = new List<SchemaObjectDefinition>
        {
            new(
                "fn_get_current_sequence_id",
                SequenceIdSchemaSql.CreateGetCurrentSequenceIdFunction,
                "DROP FUNCTION IF EXISTS public.fn_get_current_sequence_id(TEXT);"),
            new(
                "fn_get_next_sequence_id",
                SequenceIdSchemaSql.CreateGetNextSequenceIdFunction,
                "DROP FUNCTION IF EXISTS public.fn_get_next_sequence_id(TEXT);")
        };
        definitions.AddRange(Enum.GetNames<SequenceName>().Select(sequenceName =>
            new SchemaObjectDefinition(
                sequenceName,
                $"""
                CREATE SEQUENCE IF NOT EXISTS public.{sequenceName}
                START WITH 1
                INCREMENT BY {SequenceIdSettings.AllocationSize}
                NO MINVALUE
                NO MAXVALUE
                CACHE 1;
                """,
                $"DROP SEQUENCE IF EXISTS public.{sequenceName};")));
        return definitions.ToArray();
    }
}
