using System.Text;
using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb.Subscriptions;

/// <summary>Content-addressed route reconstruction. Ownership remains exclusively in durable business intent.</summary>
public sealed class PostgresCompositionRoutePlanStore(IDbConnectionSettings settings) : ICompositionRoutePlanStore
{
    public const string CreateTable = """
        CREATE TABLE IF NOT EXISTS market_data_service.composition_route_plan (
          plan_id text PRIMARY KEY CHECK(length(plan_id)=64),
          payload jsonb NOT NULL CHECK(octet_length(payload::text)<=1048576));
        """;
    const string Read = "SELECT payload::text FROM market_data_service.composition_route_plan WHERE plan_id=$1;";
    const string Insert = "INSERT INTO market_data_service.composition_route_plan(plan_id,payload) VALUES($1,$2::jsonb) ON CONFLICT(plan_id) DO NOTHING;";
    static readonly JsonSerializerOptions Json = new() { MaxDepth = 32 };
    readonly string connectionString = settings[MarketDataServiceDbContext.MarketDataServiceDbConnection].ConnectionString;

    public async Task SaveAsync(CompositionRoutePlan plan, CancellationToken cancellationToken)
    {
        plan.Validate();
        var payload = JsonSerializer.Serialize(plan, Json);
        if (Encoding.UTF8.GetByteCount(payload) > 1024 * 1024) throw new InvalidDataException("Route plan exceeds its bound.");
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(Insert, connection) { CommandTimeout = 10 };
        command.Parameters.Add(new() { Value = plan.PlanId }); command.Parameters.Add(new() { Value = payload });
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        var saved = await ReadAsync(plan.PlanId, cancellationToken).ConfigureAwait(false);
        if (saved is null || saved.PlanId != plan.PlanId) throw new InvalidDataException("Route plan commit could not be verified.");
    }

    public async Task<CompositionRoutePlan?> ReadAsync(string planId, CancellationToken cancellationToken)
    {
        if (planId is not { Length: 64 } || !planId.All(Uri.IsHexDigit)) throw new ArgumentException("Exact route plan identity required.");
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(Read, connection) { CommandTimeout = 10 };
        command.Parameters.Add(new() { Value = planId });
        var payload = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (payload is null) return null;
        if (Encoding.UTF8.GetByteCount(payload) > 1024 * 1024) throw new InvalidDataException("Persisted route plan exceeds its bound.");
        var result = JsonSerializer.Deserialize<CompositionRoutePlan>(payload, Json) ?? throw new InvalidDataException("Route plan is empty.");
        result.Validate();
        if (result.PlanId != planId) throw new InvalidDataException("Route plan identity differs from its key.");
        return result;
    }
}
