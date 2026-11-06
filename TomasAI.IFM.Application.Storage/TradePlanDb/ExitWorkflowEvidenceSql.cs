namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>Looks up committed source facts by their owning stream and exact command identity.</summary>
public static class ExitWorkflowEvidenceSql
{
    public const string ReadCommitted = """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,
               e.commandid,e.eventtimestamp::text,e.streamversion
        FROM event_stream_id s
        JOIN event_log e ON e.eventstreamid=s.eventstreamid
        JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE s.eventstream=$1 AND e.commandid=$2
        ORDER BY e.streamversion DESC
        LIMIT 2;
        """;

    public const string ReadLatestOwnerSource = """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,
               e.commandid,e.eventtimestamp::text,e.streamversion
        FROM event_stream_id s
        JOIN event_log e ON e.eventstreamid=s.eventstreamid AND e.streamversion=s.currentversion
        JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE s.eventstream=$1;
        """;
}
