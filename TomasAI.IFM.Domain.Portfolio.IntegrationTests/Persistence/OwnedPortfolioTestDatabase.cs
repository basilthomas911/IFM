using Npgsql;
using System.Text.RegularExpressions;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

/// <summary>Restricts destructive fixture operations to the current owned loopback database.</summary>
internal static class OwnedPortfolioTestDatabase
{
    internal static bool IsOwned(NpgsqlConnectionStringBuilder connection)
    {
        var configured = Environment.GetEnvironmentVariable("IFM_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(configured)) return false;
        var expected = new NpgsqlConnectionStringBuilder(configured);
        return connection.Host is "127.0.0.1" or "localhost"
            && connection.Host == expected.Host && connection.Port == expected.Port
            && connection.Database == expected.Database
            && (Regex.IsMatch(connection.Database ?? "", "^ifm_eventlog_bench_[a-f0-9]{12}_synthetic_host$") || IsHistoricalFixture(connection));
    }
    static bool IsHistoricalFixture(NpgsqlConnectionStringBuilder connection)
    {
        if(Environment.GetEnvironmentVariable("IFM_RUN_HISTORICAL_OWNER_ACCEPTANCE")!="1")return false;
        var path=Environment.GetEnvironmentVariable("IFM_REPLAY_OWNER_ACCEPTANCE_RESTORE_CERTIFICATE");var sha=Environment.GetEnvironmentVariable("IFM_REPLAY_OWNER_ACCEPTANCE_RESTORE_SHA256");
        if(path is null||sha is null||!File.Exists(path)||new FileInfo(path).Length>4*1024*1024)return false;
        using var input=File.OpenRead(path);if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input)).ToLowerInvariant()!=sha)return false;
        input.Position=0;using var document=System.Text.Json.JsonDocument.Parse(input);var proof=document.RootElement;
        var run=proof.GetProperty("runId").GetString();if(!Regex.IsMatch(run??"","^[a-f0-9]{12}$")||connection.Database!="ifm_session_"+run+"_historical_host"||proof.GetProperty("state").GetString()!="RestoredNotReplayed"||proof.GetProperty("qualified").GetBoolean()||!proof.GetProperty("fixtureComponentOnly").GetBoolean())return false;
        var endpoint=proof.GetProperty("endpoints").GetProperty("postgres");
        return endpoint.GetProperty("container").GetString()=="ifm_session_"+run+"_postgres"&&endpoint.GetProperty("host").GetString()==connection.Host&&endpoint.GetProperty("port").GetInt32()==connection.Port&&endpoint.GetProperty("database").GetString()==connection.Database;
    }
}
