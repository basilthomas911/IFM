using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.SequenceId;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;

internal static class SequenceIdSchemaSql
{
    public static readonly string CreateGetCurrentSequenceIdFunction = $"""
        CREATE OR REPLACE FUNCTION public.fn_get_current_sequence_id(sequenceIdName TEXT)
        RETURNS BIGINT AS $$
        DECLARE
            seq_name TEXT := sequenceIdName;
            curr_val BIGINT;
        BEGIN
            EXECUTE format(
                'SELECT CASE WHEN is_called THEN last_value + {SequenceIdSettings.AllocationSize - 1} ELSE 0 END FROM public.%I',
                lower(seq_name))
            INTO curr_val;
            RETURN curr_val;
        END;
        $$ LANGUAGE plpgsql;
        """;

    public const string CreateGetNextSequenceIdFunction = """
        CREATE OR REPLACE FUNCTION public.fn_get_next_sequence_id(sequenceIdName TEXT)
        RETURNS BIGINT AS $$
        DECLARE
            seq_name TEXT := sequenceIdName;
            next_val BIGINT;
        BEGIN
            EXECUTE format('SELECT nextval(%L)', seq_name) INTO next_val;
            RETURN next_val;
        END;
        $$ LANGUAGE plpgsql;
        """;
    /// <summary>Verifies required sequence functions without allocating identifiers.</summary>
    internal const string ValidateFunctions = """
        SELECT to_regprocedure('public.fn_get_current_sequence_id(text)') IS NOT NULL
        AND to_regprocedure('public.fn_get_next_sequence_id(text)') IS NOT NULL;
        """;

    /// <summary>Reads a sequence selected exclusively from the checked-in enum identities.</summary>
    internal static string ReadSequenceValue(string persistedName) => $"SELECT last_value FROM public.{persistedName};";

    internal static SchemaObjectDefinition[] BuildDefinitions()
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
