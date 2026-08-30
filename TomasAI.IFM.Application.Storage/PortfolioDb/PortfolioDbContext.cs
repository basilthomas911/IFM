using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

public sealed class PortfolioDbContext(IDbConnectionSettings settings, IDbContextFactory factory, ILogger<DbProvider> logger)
    : ObjectDataRepository<PortfolioDbContext>(settings[PortfolioDbConnection], logger), IPortfolioDbContext
{
    public const string PortfolioDbConnection = "PortfolioDbConnection";
    public override PortfolioDbContext Database => this;

    public Task<PortfolioReadModel?> GetPortfolioAsync(int id, CancellationToken ct = default) => One<PortfolioReadModel>(PortfolioDbCql.GetPortfolio, V(id), ct);
    public Task<PortfolioProjectionRevision?> GetPortfolioRevisionAsync(int id, CancellationToken ct = default) =>
        OneValue<PortfolioProjectionRevision>(PortfolioDbCql.GetPortfolioRevision, V(Pos(id)), row => new(id, null, row.GetLong(0), row.GetLong(1)), ct);
    public Task<IReadOnlyList<PortfolioReadModel>> GetPortfoliosByStateAsync(PortfolioOperatingState s, int b, int a, int n, CancellationToken ct = default) => Many<PortfolioReadModel>(PortfolioDbCql.GetPortfoliosByState, V(s.ToString(), b, a, Page(n)), ct);
    public Task<IReadOnlyList<FundMandateReadModel>> GetFundsByPortfolioAsync(int p, int a, int n, CancellationToken ct = default) => Many<FundMandateReadModel>(PortfolioDbCql.GetFundsByPortfolio, V(Pos(p), a, Page(n)), ct);
    public Task<FundMandateReadModel?> GetFundAsync(int id, CancellationToken ct = default) => One<FundMandateReadModel>(PortfolioDbCql.GetFund, V(Pos(id)), ct);
    public Task<PortfolioProjectionRevision?> GetFundRevisionAsync(int id, CancellationToken ct = default) =>
        OneValue<PortfolioProjectionRevision>(PortfolioDbCql.GetFundRevision, V(Pos(id)), row => new(row.GetInt(0), id, row.GetLong(1), row.GetLong(2)), ct);
    public Task<IReadOnlyList<FundMandateReadModel>> GetActiveFundsAsync(int p, int y, string h, DateTime at, int n, CancellationToken ct = default) { Utc(at); ArgumentException.ThrowIfNullOrWhiteSpace(h); return Many<FundMandateReadModel>(PortfolioDbCql.GetActiveFunds, V(Pos(p), y, h, at, Page(n)), ct); }
    public Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetAssignmentsAsync(int p, int f, long v, int n, CancellationToken ct = default) => Many<FundTradeTemplateAssignmentReadModel>(PortfolioDbCql.GetAssignments, V(Pos(p), Pos(f), Positive(v), Page(n)), ct);
    public Task<FundAllocationReadModel?> GetCurrentAllocationAsync(int p, int f, CancellationToken ct = default) => One<FundAllocationReadModel>(PortfolioDbCql.GetAllocation, V(Pos(p), Pos(f)), ct);
    public Task<FundRiskEnvelopeReadModel?> GetCurrentRiskEnvelopeAsync(int p, int f, CancellationToken ct = default) => One<FundRiskEnvelopeReadModel>(PortfolioDbCql.GetEnvelope, V(Pos(p), Pos(f)), ct);
    public Task<IReadOnlyList<FundOrderProjectionReadModel>> GetOrdersAsync(int p, int f, DateOnly m, DateTime before, int n, CancellationToken ct = default) { Utc(before); return Many<FundOrderProjectionReadModel>(PortfolioDbCql.GetOrders, V(Pos(p), Pos(f), m, before, Page(n)), ct); }
    public Task<FundOrderProjectionReadModel?> GetOrderAsync(int id, CancellationToken ct = default) => One<FundOrderProjectionReadModel>(PortfolioDbCql.GetOrder, V(Pos(id)), ct);
    public Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetOrderTradesAsync(int id, int n, CancellationToken ct = default) => Many<FundOrderTradeProjectionReadModel>(PortfolioDbCql.GetOrderTrades, V(Pos(id), Page(n)), ct);
    public Task<FundOrderTradeProjectionReadModel?> GetTradeAsync(int id, CancellationToken ct = default) => One<FundOrderTradeProjectionReadModel>(PortfolioDbCql.GetTrade, V(Pos(id)), ct);
    public Task<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>> GetCompositionsAsync(Guid id, int n, CancellationToken ct = default) { if (id == Guid.Empty) throw new ArgumentException("WorkflowId is required."); return Many<FundCompositionWorkflowProjectionReadModel>(PortfolioDbCql.GetCompositions, V(id, Page(n)), ct); }

    public async Task UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel> row, int bucket, CancellationToken ct = default)
    {
        Check(row); var x = row.Value; var c = Common(row);
        await Put(PortfolioDbCql.InsertPortfolio, V(x.PortfolioId, x.PortfolioVersion, x.OperatingState.ToString(), c), ct);
        await Put(PortfolioDbCql.InsertPortfolioState, V(x.OperatingState.ToString(), bucket, x.PortfolioId, x.PortfolioVersion, c), ct);
    }
    public async Task UpsertFundAsync(PortfolioProjection<FundMandateReadModel> row, CancellationToken ct = default)
    {
        Check(row); var x = row.Value; var c = Common(row);
        await Put(PortfolioDbCql.InsertFundPortfolio, V(x.PortfolioId, x.FundId, x.FundMandateVersion, x.OperatingState.ToString(), c), ct);
        await Put(PortfolioDbCql.InsertFundId, V(x.FundId, x.FundMandateVersion, x.PortfolioId, x.OperatingState.ToString(), c), ct);
        if (x.OperatingState == FundOperatingState.Active)
            await Put(PortfolioDbCql.InsertActiveFund, V(x.PortfolioId, x.TradingYear, x.DecisionHorizon, x.EffectiveFromUtc, x.FundId, x.FundMandateVersion, c), ct);
    }
    public Task UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel> r, CancellationToken ct = default) { Check(r); var x=r.Value; return Put(PortfolioDbCql.InsertAssignment,V(x.PortfolioId,x.FundId,x.FundMandateVersion,x.TradeTemplateId,x.TradeTemplateVersion,Common(r)),ct); }
    public Task UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel> r, CancellationToken ct = default) { Check(r); var x=r.Value; return Put(PortfolioDbCql.InsertAllocation,V(x.PortfolioId,x.FundId,x.AllocationVersion,Common(r)),ct); }
    public Task UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel> r, CancellationToken ct = default) { Check(r); var x=r.Value; return Put(PortfolioDbCql.InsertEnvelope,V(x.PortfolioId,x.FundId,x.EnvelopeVersion,Common(r)),ct); }
    public async Task UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel> r, DateOnly month, CancellationToken ct = default) { Check(r); var x=r.Value; var c=Common(r); await Put(PortfolioDbCql.InsertOrderTimeline,V(x.PortfolioId,x.FundId,month,x.CreatedOnUtc,x.OrderId,x.Status,c),ct); await Put(PortfolioDbCql.InsertOrderId,V(x.OrderId,x.PortfolioId,x.FundId,x.Status,c),ct); }
    public async Task UpsertTradeAsync(PortfolioProjection<FundOrderTradeProjectionReadModel> r, CancellationToken ct = default) { Check(r); var x=r.Value; var c=Common(r); await Put(PortfolioDbCql.InsertTradeOrder,V(x.OrderId,x.TradeId,x.PortfolioId,x.FundId,c),ct); await Put(PortfolioDbCql.InsertTradeId,V(x.TradeId,x.OrderId,x.PortfolioId,x.FundId,c),ct); }
    public Task UpsertCompositionAsync(PortfolioProjection<FundCompositionWorkflowProjectionReadModel> r, CancellationToken ct = default) { Check(r); var x=r.Value; return Put(PortfolioDbCql.InsertComposition,V(x.WorkflowId,x.OrderId,x.PortfolioId,x.FundId,x.Status,Common(r)),ct); }

    async Task<T?> One<T>(string cql, PortfolioValues values, CancellationToken ct) where T : class => await factory.PortfolioDb.Use($"PortfolioDb.{nameof(One)}",cql).SetParameters(values).ExecuteSingleAsync(Map<T>,ct).ConfigureAwait(false);
    async Task<T?> OneValue<T>(string cql, PortfolioValues values, Func<IObjectDataRecord, T> map, CancellationToken ct) where T : class =>
        await factory.PortfolioDb.Use($"PortfolioDb.{nameof(OneValue)}", cql).SetParameters(values).ExecuteSingleAsync(map, ct).ConfigureAwait(false);
    async Task<IReadOnlyList<T>> Many<T>(string cql, PortfolioValues values, CancellationToken ct) where T : class => [.. await factory.PortfolioDb.Use($"PortfolioDb.{nameof(Many)}",cql).SetParameters(values).ExecuteQueryAsync(Map<T>,ct).ConfigureAwait(false)];
    async Task Put(string cql, PortfolioValues values, CancellationToken ct)
    {
        // EventSourceDb event IDs are globally increasing. Using them as the Scylla write timestamp
        // makes replay idempotent and prevents a delayed older projection from replacing a newer row.
        var monotonicCql = $"{cql.TrimEnd().TrimEnd(';')} USING TIMESTAMP :projectionWriteTimestamp;";
        var monotonicValues = new PortfolioValues([.. values.Values, values.Values[^4]]);
        await factory.PortfolioDb.Use("PortfolioDb.Upsert",monotonicCql).SetParameters(monotonicValues).ExecuteCommandAsync(ct).ConfigureAwait(false);
    }
    static T Map<T>(IObjectDataRecord row) where T : class => JsonSerializer.Deserialize<T>(row.GetString(0)) ?? throw new InvalidOperationException($"Stored {typeof(T).Name} payload is invalid.");
    static object?[] Common<T>(PortfolioProjection<T> r) => [r.SchemaVersion,r.AggregateVersion,r.SourceEventId,r.UpdatedOnUtc,JsonSerializer.Serialize(r.Value),r.PayloadHash];
    static PortfolioValues V(params object?[] values) => new(Flatten(values));
    static object?[] Flatten(object?[] values) => [.. values.SelectMany(x => x is object?[] a ? a : [x])];
    static void Check<T>(PortfolioProjection<T> r) { ArgumentNullException.ThrowIfNull(r); if (r.SchemaVersion<=0||r.AggregateVersion<=0||r.SourceEventId<=0||r.PayloadHash.Length!=64) throw new ArgumentException("Projection metadata is invalid."); Utc(r.UpdatedOnUtc); }
    static int Pos(int x) => x > 0 ? x : throw new ArgumentOutOfRangeException(nameof(x));
    static long Positive(long x) => x > 0 ? x : throw new ArgumentOutOfRangeException(nameof(x));
    static int Page(int n) => n is >= 1 and <= 200 ? n : throw new ArgumentOutOfRangeException(nameof(n),"Page size must be 1..200.");
    static void Utc(DateTime x) { if (x.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC."); }
}

internal readonly record struct PortfolioValues(object?[] Values) : IBindValue { public object Bind() => Values; }
