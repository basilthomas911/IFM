namespace TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;

/// <summary>Contains PostgreSQL schema statements for SequenceIdDb.</summary>
public static class SequenceIdSchemaSql
{
    /// <summary>Creates the get current sequence id function.</summary>
    internal const string CreateGetCurrentSequenceIdFunction =
        """
        CREATE OR REPLACE FUNCTION public.fn_get_current_sequence_id(sequenceIdName TEXT)
        RETURNS BIGINT AS $$
        DECLARE
            seq_name TEXT := sequenceIdName;
            curr_val BIGINT;
        BEGIN
            EXECUTE format(
                'SELECT CASE WHEN is_called THEN last_value + {0} ELSE 0 END FROM public.%I',
                lower(seq_name))
            INTO curr_val;
            RETURN curr_val;
        END;
        $$ LANGUAGE plpgsql;
        """;

    /// <summary>Creates the get next sequence id function.</summary>
    internal const string CreateGetNextSequenceIdFunction =
        """
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

    /// <summary>Creates a sequence for a known identifier and allocation size.</summary>
    internal const string CreateSequence =
        """
        CREATE SEQUENCE IF NOT EXISTS public.{0}
        START WITH 1
        INCREMENT BY {1}
        NO MINVALUE
        NO MAXVALUE
        CACHE 1;
        """;

    /// <summary>Drops the get current sequence id function.</summary>
    internal const string DropGetCurrentSequenceIdFunction =
        """
        DROP FUNCTION IF EXISTS public.fn_get_current_sequence_id(TEXT);
        """;

    /// <summary>Drops the get next sequence id function.</summary>
    internal const string DropGetNextSequenceIdFunction =
        """
        DROP FUNCTION IF EXISTS public.fn_get_next_sequence_id(TEXT);
        """;

    /// <summary>Drops a sequence for a known identifier.</summary>
    internal const string DropSequence =
        """
        DROP SEQUENCE IF EXISTS public.{0};
        """;
}
