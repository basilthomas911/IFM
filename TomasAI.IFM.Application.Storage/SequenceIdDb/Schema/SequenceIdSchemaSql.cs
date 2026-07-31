namespace TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;

internal static class SequenceIdSchemaSql
{
    public const string CreateGetCurrentSequenceIdFunction = """
        CREATE OR REPLACE FUNCTION public.fn_get_current_sequence_id(sequenceIdName TEXT)
        RETURNS BIGINT AS $$
        DECLARE
            seq_name TEXT := sequenceIdName;
            curr_val BIGINT;
        BEGIN
            EXECUTE format('SELECT currval(%L)', seq_name) INTO curr_val;
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
}
