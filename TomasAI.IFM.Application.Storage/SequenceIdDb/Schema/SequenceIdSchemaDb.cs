using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;

public sealed class SequenceIdSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<SequenceIdSchemaDb>(connectionSettings[SequenceIdDbContext.SequenceIdDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects = BuildDefinitions();

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;

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
                INCREMENT BY 100
                NO MINVALUE
                NO MAXVALUE
                CACHE 1;
                """,
                $"DROP SEQUENCE IF EXISTS public.{sequenceName};")));
        return definitions.ToArray();
    }
}
