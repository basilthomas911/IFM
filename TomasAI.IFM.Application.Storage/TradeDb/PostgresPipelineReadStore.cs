using System.Text.Json;
using System.Globalization;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Reads accepted pipeline results and scoped PostgreSQL history pages.</summary>
public static class PostgresPipelineReadStore
{
    /// <summary>Reads and validates exact composer invocation evidence.</summary>
    public static async Task<OrderCompositionFunctionCompletedEvent?> CompositionAsync(IEnlistedPostgresTransaction transaction, StrategyWorkflowId workflow, Guid invocation, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var rows=await transaction.QueryAsync("SELECT payload,result_hash,input_hash FROM trade.order_composition_invocation WHERE workflow_id=$1 AND invocation_id=$2;",
            [workflow.Value,invocation],r=>(Payload:r.GetFieldValue<byte[]>(0),Result:r.GetString(1),Input:r.GetString(2)),token)
            .ConfigureAwait(false);
        if(rows.Count==0)return null;
        var row=rows[0];var source=MessagePackBinarySerializer.Shared.Deserialize<OrderCompositionFunctionCompletedEvent>(row.Payload);
        var result=OrderCompositionContracts.ReadResult(source.Result);
        if(result.WorkflowId!=workflow || result.InvocationId!=invocation || !string.Equals(source.Result.PayloadSha256,row.Result,StringComparison.Ordinal)
            || !string.Equals(result.InputSha256,row.Input,StringComparison.Ordinal))throw new InvalidDataException("Stored composition evidence mismatch.");
        return source;
    }

    /// <summary>Reads and validates exact selection invocation evidence.</summary>
    public static async Task<TradeSelectionFunctionCompletedEvent?> SelectionAsync(IEnlistedPostgresTransaction transaction, StrategyWorkflowId workflow, Guid invocation, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var rows=await transaction.QueryAsync("SELECT event_payload,result_sha256 FROM trade.trade_selection_invocation_event WHERE workflow_id=$1 AND invocation_id=$2 AND source_sequence=1;",
            [workflow.Value,invocation],r=>(Payload:r.GetFieldValue<byte[]>(0),Result:r.GetString(1)),token)
            .ConfigureAwait(false);
        if(rows.Count==0)return null;
        var row=rows[0];var source=MessagePackBinarySerializer.Shared.Deserialize<TradeSelectionFunctionCompletedEvent>(row.Payload);
        var result=TradeSelectionContracts.ReadResult(source.Result);
        if(result.WorkflowId!=workflow || result.InvocationId!=invocation || !string.Equals(source.Result.PayloadSha256,row.Result,StringComparison.Ordinal))
            throw new InvalidDataException("Stored selector evidence identity/hash mismatch.");
        return source;
    }

    /// <summary>Reads a keyset page scoped to one portfolio, fund, and value date.</summary>
    public static async Task<QueryPage<OrderCompositionHistoryRow>> CompositionHistoryAsync(IEnlistedPostgresTransaction transaction,int portfolio,int fund,DateOnly date,int pageSize,byte[]? pagingState,CancellationToken token=default)
    {
        var page=await ReadHistoryAsync(transaction,true,portfolio,fund,date,pageSize,pagingState,token)
            .ConfigureAwait(false);
        return new(page.Items.Select(r=>new OrderCompositionHistoryRow(portfolio,fund,date,r.AtUtc,r.Workflow,r.Invocation,r.Event,r.Horizon,r.Outcome,r.Reason,r.Result,r.Hash)).ToArray(),page.PagingState);
    }

    /// <summary>Reads a keyset page scoped to one portfolio, fund, and value date.</summary>
    public static async Task<QueryPage<TradeSelectionHistoryRow>> SelectionHistoryAsync(IEnlistedPostgresTransaction transaction,int portfolio,int fund,DateOnly date,int pageSize,byte[]? pagingState,CancellationToken token=default)
    {
        var page=await ReadHistoryAsync(transaction,false,portfolio,fund,date,pageSize,pagingState,token)
            .ConfigureAwait(false);
        return new(page.Items.Select(r=>new TradeSelectionHistoryRow(portfolio,fund,date,r.AtUtc,r.Workflow,r.Invocation,r.Event,r.Horizon,r.Outcome,r.Reason,r.Result,r.Hash)).ToArray(),page.PagingState);
    }

    static async Task<QueryPage<HistoryRow>> ReadHistoryAsync(IEnlistedPostgresTransaction transaction,bool composition,int portfolio,int fund,DateOnly date,int pageSize,byte[]? pagingState,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(portfolio);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fund);
        if(date==default)throw new ArgumentException("A history value date is required.",nameof(date));
        if(pageSize<1 || pageSize>(composition?100:200))throw new ArgumentOutOfRangeException(nameof(pageSize),"A bounded history page size is required.");
        var scope=string.Create(CultureInfo.InvariantCulture,$"{composition}:{portfolio}:{fund}:{date.DayNumber}");
        Cursor? cursor=null;
        if(pagingState is not null)
        {
            if(pagingState.Length>2048)throw new ArgumentException("Invalid history cursor.",nameof(pagingState));
            try{cursor=JsonSerializer.Deserialize<Cursor>(pagingState);}catch(JsonException error){throw new ArgumentException("Invalid history cursor.",nameof(pagingState),error);}
            if(cursor is null || !string.Equals(cursor.Scope,scope,StringComparison.Ordinal) || cursor.AtUtc.Kind!=DateTimeKind.Utc || cursor.Workflow==Guid.Empty || cursor.Invocation==Guid.Empty)
                throw new ArgumentException("History cursor does not belong to this scope.",nameof(pagingState));
        }
        // Table and column names below are fixed application literals; callers supply only parameter values.
        var table=composition?"trade.order_composition_history":"trade.trade_selection_history_by_fund_date";
        var at=composition?"evaluated_at_utc":"occurred_at_utc";
        var hash=composition?"result_hash":"result_sha256";
        var continuation=cursor is null?string.Empty:$" AND ({at},workflow_id,invocation_id)<($4,$5,$6)";
        var limit=cursor is null?"$4":"$7";
        var sql=$"SELECT {at},workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,{hash} FROM {table} WHERE portfolio_id=$1 AND fund_id=$2 AND value_date=$3{continuation} ORDER BY {at} DESC,workflow_id DESC,invocation_id DESC LIMIT {limit};";
        object?[] values=cursor is null?[portfolio,fund,date,pageSize+1]:[portfolio,fund,date,cursor.AtUtc,cursor.Workflow,cursor.Invocation,pageSize+1];
        var rows=await transaction.QueryAsync(sql,values,r=>new HistoryRow(r.GetDateTime(0),r.GetGuid(1),r.GetGuid(2),r.GetGuid(3),r.GetInt16(4),checked((byte)r.GetInt16(5)),r.GetString(6),r.GetGuid(7),r.GetString(8)),token)
            .ConfigureAwait(false);
        var items=rows.Take(pageSize).ToArray();
        byte[]? next=null;
        if(rows.Count>pageSize){var last=items[^1];next=JsonSerializer.SerializeToUtf8Bytes(new Cursor(scope,last.AtUtc,last.Workflow,last.Invocation));}
        return new(items,next);
    }
    sealed record Cursor(string Scope,DateTime AtUtc,Guid Workflow,Guid Invocation);
    sealed record HistoryRow(DateTime AtUtc,Guid Workflow,Guid Invocation,Guid Event,short Horizon,byte Outcome,string Reason,Guid Result,string Hash);
}
