using System;
using System.Threading.Tasks;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

/// <summary>Owns a newly generated PostgreSQL database; application databases are never targets.</summary>
internal sealed class PostgresInstrumentTestDatabase : IAsyncDisposable
{
    readonly string name = "ifm_instrument_test_" + Guid.NewGuid().ToString("N");
    const string Admin = "Host=localhost;Port=5432;Database=postgres;Pooling=false";
    public string Connection => "Host=localhost;Port=5432;Database=" + name;
    public static async Task<PostgresInstrumentTestDatabase> CreateAsync()
    {
        var database = new PostgresInstrumentTestDatabase();
        await database.ExecuteAdminAsync("CREATE DATABASE");
        return database;
    }
    async Task ExecuteAdminAsync(string operation)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^ifm_instrument_test_[a-f0-9]{32}$"))
            throw new InvalidOperationException("Unowned database name.");
        await using var connection = new NpgsqlConnection(PostgresObjectDataRepositoryConnection.ResolveHostConnectionString(Admin));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(operation + " " + name + (operation == "DROP DATABASE" ? " WITH (FORCE)" : ""), connection);
        await command.ExecuteNonQueryAsync();
    }
    public async ValueTask DisposeAsync() => await ExecuteAdminAsync("DROP DATABASE");
}
