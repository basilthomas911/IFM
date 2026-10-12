using System.Text.Json;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Native trade lifecycle reads. Position history uses bounded keyset pages in ascending order.</summary>
public sealed class PostgresTradeFlowReadStore
{
    sealed record PositionCursor(Guid PositionId,DateTime FromUtc,DateTime ToUtc,DateTime LastAtUtc,long LastSequence);

    /// <summary>Reads every dated position row through ordered pages without a fixed history row cap.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task<QueryPage<StrategyPositionSnapshot>> PositionHistoryAsync(IEnlistedPostgresTransaction db,
        Guid positionId,DateTime fromUtc,DateTime toUtc,int pageSize,byte[]? cursor=null,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        if(positionId==Guid.Empty || fromUtc.Kind!=DateTimeKind.Utc || toUtc.Kind!=DateTimeKind.Utc || fromUtc>toUtc || pageSize is <1 or >1000)
            throw new ArgumentException("A position, ordered UTC range and page size 1..1000 are required.",nameof(positionId));
        PositionCursor? last=null;
        if(cursor is not null)
        {
            try { last=JsonSerializer.Deserialize<PositionCursor>(cursor); }
            catch(JsonException error) { throw new ArgumentException("Invalid position history cursor.",nameof(cursor),error); }
            if(last is null || last.PositionId!=positionId || last.FromUtc!=fromUtc || last.ToUtc!=toUtc || last.LastAtUtc.Kind!=DateTimeKind.Utc || last.LastAtUtc<fromUtc || last.LastAtUtc>toUtc || last.LastSequence<0)
                throw new ArgumentException("Position history cursor does not match this query.",nameof(cursor));
        }
        var sql="""
            SELECT asOfUtc,positionSequence,payload FROM trade.strategy_position_history
            WHERE positionId=$1 AND asOfUtc>=$2 AND asOfUtc<=$3
            """;
        object?[] values;
        if(last is null) {sql+=" ORDER BY asOfUtc,positionSequence LIMIT $4;";values=[positionId,fromUtc,toUtc,pageSize+1];}
        else {sql+=" AND (asOfUtc,positionSequence)>($4,$5) ORDER BY asOfUtc,positionSequence LIMIT $6;";values=[positionId,fromUtc,toUtc,last.LastAtUtc,last.LastSequence,pageSize+1];}
        var rows=await (db.QueryAsync(sql,values,r=>(At:r.GetDateTime(0),Sequence:r.GetInt64(1),Position:MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>((byte[])r.GetValue(2))),token))
            .ConfigureAwait(false);
        var items=rows.Take(pageSize).Select(x=>x.Position).ToArray();
        var next=rows.Count>pageSize?JsonSerializer.SerializeToUtf8Bytes(new PositionCursor(positionId,fromUtc,toUtc,rows[pageSize-1].At,rows[pageSize-1].Sequence)):null;
        return new(items,next);
    }

    /// <summary>Reads the last committed position snapshot.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task<StrategyPositionSnapshot?> PositionAsync(IEnlistedPostgresTransaction db,StrategyPositionId id,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var trade=id.Trade;
        var payload=await (db.ScalarAsync("SELECT payload FROM trade.strategy_position_current WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4 AND positionId=$5;",[trade.PortfolioId,trade.FundId,trade.OrderId,trade.TradeId,id.PositionId],token))
            .ConfigureAwait(false);
        return payload is byte[] bytes?MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(bytes):null;
    }
    sealed record RouteCursor(Guid PositionId,Guid LegId);
    const string RouteColumns="contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation";
    static OpenPositionRouteReadModel Route(Npgsql.NpgsqlDataReader r) => new(r.GetString(0),
        new PortfolioFundTradeLeg(r.GetInt32(1),r.GetInt32(2),r.GetInt32(3),r.GetInt32(4),r.GetGuid(5),r.GetGuid(6),Enum.Parse<TradeStrategyKind>(r.GetString(7)),r.GetInt64(8)));

    /// <summary>Reads the current committed routes for one contract or the complete recovery snapshot.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public Task<IReadOnlyList<OpenPositionRouteReadModel>> RoutesAsync(IEnlistedPostgresTransaction db,string? contractId=null,CancellationToken token=default)
        
    {
        ArgumentNullException.ThrowIfNull(db);
        return contractId is null
            ? db.QueryAsync("SELECT "+RouteColumns+" FROM trade.open_position_route_recovery WHERE shard=0 ORDER BY positionId,tradeLegId;",[],Route,token)
            : db.QueryAsync("SELECT "+RouteColumns+" FROM trade.open_position_route WHERE contractId=$1 ORDER BY positionId,tradeLegId;",[contractId],Route,token);
    }

    /// <summary>Reads bounded route recovery pages without Scylla paging-state tokens.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task<QueryPage<OpenPositionRouteReadModel>> RoutePageAsync(IEnlistedPostgresTransaction db,int size,byte[]? cursor=null,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        if(size is <1 or >500) throw new ArgumentOutOfRangeException(nameof(size));
        RouteCursor? last=null;
        if(cursor is not null)
        {
            try {last=JsonSerializer.Deserialize<RouteCursor>(cursor);}
            catch(JsonException error) {throw new ArgumentException("Invalid route recovery cursor.",nameof(cursor),error);}
            if(last is null || last.PositionId==Guid.Empty || last.LegId==Guid.Empty) throw new ArgumentException("Invalid route recovery cursor.",nameof(cursor));
        }
        var sql="SELECT "+RouteColumns+" FROM trade.open_position_route_recovery WHERE shard=0";
        object?[] values;
        if(last is null) {sql+=" ORDER BY positionId,tradeLegId LIMIT $1;";values=[size+1];}
        else {sql+=" AND (positionId,tradeLegId)>($1,$2) ORDER BY positionId,tradeLegId LIMIT $3;";values=[last.PositionId,last.LegId,size+1];}
        var rows=await (db.QueryAsync(sql,values,Route,token))
            .ConfigureAwait(false);
        var next=rows.Count>size?JsonSerializer.SerializeToUtf8Bytes(new RouteCursor(rows[size-1].Route.StrategyPositionId,rows[size-1].Route.TradeLegId)):null;
        return new(rows.Take(size).ToArray(),next);
    }

    /// <summary>Reads the persisted execution facts without contacting the broker.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task<OrderExecutionDefinition?> ExecutionAsync(IEnlistedPostgresTransaction db,TradeOrderId id,Guid attempt,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var value=await (db.ScalarAsync("SELECT payload FROM trade.order_execution WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND executionAttemptId=$4;",[id.PortfolioId,id.FundId,id.OrderId,attempt],token))
            .ConfigureAwait(false);
        return value is byte[] bytes?MessagePackBinarySerializer.Shared.Deserialize<OrderExecutionDefinition>(bytes):null;
    }

    /// <summary>Reads the established trade's last accepted evidence revision.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task<EstablishedTradeDefinition?> TradeAsync(IEnlistedPostgresTransaction db,TradeEntityId id,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var value=await (db.ScalarAsync("SELECT payload FROM trade.established_trade WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4;",[id.PortfolioId,id.FundId,id.OrderId,id.TradeId],token))
            .ConfigureAwait(false);
        return value is byte[] bytes?MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(bytes):null;
    }

    /// <summary>Reads persisted whole-trade limits, preserving the existing optional same-type limit override.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public Task<IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel>> LimitsAsync(IEnlistedPostgresTransaction db,int? tradeId=null,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var sql="""
            SELECT t.tradeId,t.tradeType,t.riskMargin,t.maxProfit,t.maxLoss,t.maxReturn,
              COALESCE(s.maxLossLimit,t.maxLossLimit),COALESCE(s.minProfitLimit,t.minProfitLimit),COALESCE(s.maxProfitLimit,t.maxProfitLimit),
              t.minProfitTarget,t.dailyProfitTarget,t.createdOn,t.createdBy,t.updatedOn,t.updatedBy
            FROM trade.trade_limit t LEFT JOIN trade.trade_type_limit s ON t.tradeId=s.tradeId AND t.tradeType=s.tradeType
            """;
        if(tradeId.HasValue) sql+=" WHERE t.tradeId=$1";
        sql+=" ORDER BY t.tradeId,t.tradeType;";
        return db.QueryAsync(sql,tradeId.HasValue?[tradeId.Value]:[],r=>new TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel
        {
            TradeId=r.GetInt32(0),TradeType=Enum.Parse<TradeType>(r.GetString(1)),RiskMargin=r.GetDecimal(2),MaxProfit=r.GetDecimal(3),MaxLoss=r.GetDecimal(4),
            MaxReturn=r.GetDecimal(5),MaxLossLimit=r.GetDecimal(6),MinProfitLimit=r.GetDecimal(7),MaxProfitLimit=r.GetDecimal(8),MinProfitTarget=r.GetDecimal(9),
            DailyProfitTarget=r.GetDecimal(10),CreatedOn=r.GetDateTime(11),CreatedBy=r.GetString(12),UpdatedOn=r.GetDateTime(13),UpdatedBy=r.GetString(14)
        },token);
    }

    /// <summary>Reads the saved spread limit rows without requesting live market data.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public Task<IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel>> SpreadLimitsAsync(IEnlistedPostgresTransaction db,int? tradeId=null,TradeType? type=null,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var sql="SELECT tradeId,tradeType,maxLossLimit,minProfitLimit,maxProfitLimit FROM trade.trade_type_limit";
        object?[] values=[];
        if(tradeId.HasValue) {sql+=" WHERE tradeId=$1";values=[tradeId.Value];}
        if(type.HasValue)
        {
            if(!tradeId.HasValue) throw new ArgumentException("A trade identifier is required with a spread type.",nameof(type));
            sql+=" AND tradeType=$2";values=[tradeId.Value,type.Value.ToString()];
        }
        sql+=" ORDER BY tradeId,tradeType;";
        return db.QueryAsync(sql,values,r=>new TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel
        {TradeId=r.GetInt32(0),TradeType=Enum.Parse<TradeType>(r.GetString(1)),MaxLossLimit=r.GetDecimal(2),MinProfitLimit=r.GetDecimal(3),MaxProfitLimit=r.GetDecimal(4)},token);
    }

}
