using System;

namespace TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;

/// <summary>
/// Contains SQL query strings for SequenceIdDb operations
/// </summary>
public static class SequenceIdDbSql
{
    /// <summary>
    /// SQL to create a function for getting the current sequence ID
    /// </summary>
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

    /// <summary>
    /// SQL to create a function for getting the next sequence ID
    /// </summary>
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

    /// <summary>
    /// SQL to create the FuturesItiSignal_SequenceId sequence
    /// </summary>
    public const string CreateSequenceFuturesItiSignal_SequenceId = """
CREATE SEQUENCE FuturesItiSignal_SequenceId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    /// <summary>
    /// SQL to create the FuturesItiTrendClassData_SequenceId sequence
    /// </summary>
    public const string CreateSequenceFuturesItiTrendClassData_SequenceId = """
CREATE SEQUENCE FuturesItiTrendClassData_SequenceId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    /// <summary>
    /// SQL to create the FuturesItiTrendDeltaData_SequenceId sequence
    /// </summary>
    public const string CreateSequenceFuturesItiTrendDeltaData_SequenceId = """
CREATE SEQUENCE FuturesItiTrendDeltaData_SequenceId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    /// <summary>
    /// SQL to create the FuturesOptionTickData_TickId sequence
    /// </summary>
    public const string CreateSequenceFuturesOptionTickData_TickId = """
CREATE SEQUENCE FuturesOptionTickData_TickId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    /// <summary>
    /// SQL to create the FuturesTickData_TickId sequence
    /// </summary>
    public const string CreateSequenceFuturesTickData_TickId = """
CREATE SEQUENCE FuturesTickData_TickId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    /// <summary>
    /// SQL to create the OptionQuote_QuoteId sequence
    /// </summary>
    public const string CreateSequenceOptionQuote_QuoteId = """
CREATE SEQUENCE OptionQuote_QuoteId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    /// <summary>
    /// SQL to create the OptionQuoteData_SequenceId sequence
    /// </summary>
    public const string CreateSequenceOptionQuoteData_SequenceId = """
CREATE SEQUENCE OptionQuoteData_SequenceId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    /// <summary>
    /// SQL to create the OptionTradeSpreadData_SequenceId sequence
    /// </summary>
    public const string CreateSequenceOptionTradeSpreadData_SequenceId = """
CREATE SEQUENCE OptionTradeSpreadData_SequenceId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    /// <summary>
    /// SQL to create the StreamingRequest_RequestId sequence
    /// </summary>
    public const string CreateSequenceStreamingRequest_RequestId = """
CREATE SEQUENCE StreamingRequest_RequestId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    /// <summary>
    /// SQL to create the TelemetryLog_SequenceId sequence
    /// </summary>
    public const string CreateSequenceTelemetryLog_SequenceId = """
CREATE SEQUENCE TelemetryLog_SequenceId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    /// <summary>
    /// SQL to create the TradePlan_SequenceId sequence
    /// </summary>
    public const string CreateSequenceTradePlan_SequenceId = """
CREATE SEQUENCE TradePlan_SequenceId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    public const string CreateSequenceTradePlacementSignal_SequenceId = """
CREATE SEQUENCE TradePlacementSignal_SequenceId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";

    public const string CreateSequenceFuturesIntraDay_SequenceId = """
        CREATE SEQUENCE FuturesIntraDay_SequenceId
        START WITH 1
        INCREMENT BY 100
        NO MINVALUE
        NO MAXVALUE
        CACHE 1;
        """;

    public const string CreateSequenceFundTransaction_TransactionId = """
CREATE SEQUENCE FundTransaction_TransactionId
START WITH 1
INCREMENT BY 100
NO MINVALUE
NO MAXVALUE
CACHE 1;
""";
    /// <summary>
    /// SQL to get current futures EOD data by date range
    /// </summary>
    public const string GetCurrentFuturesEodDataByDateRangeIndex = """
select 
valueDate as "ValueDate",
contractId as "ContractId"
from futures_eod_data_index
where token(valueDate) >= token(:startDate)
and token(valueDate) <= token(:endDate)
""";

    /// <summary>
    /// SQL to get the current sequence ID
    /// </summary>
    public const string GetCurrentSequenceId = """
select public.fn_get_current_sequence_id($1) as "Value"
""";

    /// <summary>
    /// SQL to get the next sequence ID
    /// </summary>
    public const string GetNextSequenceId = """
select public.fn_get_next_sequence_id($1) as "Value"
""";
}