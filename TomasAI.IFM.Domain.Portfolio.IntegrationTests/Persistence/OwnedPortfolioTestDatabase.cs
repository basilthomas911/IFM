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
            && Regex.IsMatch(connection.Database ?? "", "^ifm_eventlog_bench_[a-f0-9]{12}_synthetic_host$");
    }
}
