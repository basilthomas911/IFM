namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

/// <summary>SQL statements and query templates for SequenceId storage.</summary>
public static class SequenceIdDbSql
{
    /// <summary>
    /// SQL to get the current sequence ID
    /// </summary>
    internal const string GetCurrentSequenceId =
        """
        SELECT public.fn_get_current_sequence_id($1) AS "Value";
        """;

    /// <summary>
    /// SQL to get the next sequence ID
    /// </summary>
    internal const string GetNextSequenceId =
        """
        SELECT public.fn_get_next_sequence_id($1) AS "Value";
        """;

    /// <summary>
    /// SQL to read the configured PostgreSQL sequence increment.
    /// </summary>
    internal const string GetSequenceAllocationSize =
        """
        SELECT increment_by AS "Value"
        FROM pg_sequences
        WHERE schemaname = 'public'
        AND sequencename = lower($1);
        """;

    /// <summary>Reads a sequence selected by a checked-in identifier.</summary>
    internal const string ReadSequenceValue =
        """
        SELECT last_value FROM public.{0};
        """;

    /// <summary>Checks that the sequence functions exist without allocating identifiers.</summary>
    internal const string ValidateFunctions =
        """
        SELECT to_regprocedure('public.fn_get_current_sequence_id(text)') IS NOT NULL
        AND to_regprocedure('public.fn_get_next_sequence_id(text)') IS NOT NULL;
        """;
}
