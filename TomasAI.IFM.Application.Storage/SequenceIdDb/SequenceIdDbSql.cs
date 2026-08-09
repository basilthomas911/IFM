using System;

namespace TomasAI.IFM.Application.Storage.SequenceIdDb;

/// <summary>
/// Contains SQL query strings for SequenceIdDb operations
/// </summary>
public static class SequenceIdDbSql
{
    /// <summary>
    /// SQL to read the configured PostgreSQL sequence increment.
    /// </summary>
    public const string GetSequenceAllocationSize = """
select increment_by as "Value"
from pg_sequences
where schemaname = 'public'
and sequencename = lower($1)
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
