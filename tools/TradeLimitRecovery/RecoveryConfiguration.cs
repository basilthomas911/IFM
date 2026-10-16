using Microsoft.Extensions.Configuration;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

/// <summary>Uses the application settings and the same host credential resolver as the API.</summary>
internal sealed record RecoveryConfiguration(string Postgres, string NatsUrl)
{
    internal static RecoveryConfiguration Load()
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var root = FindRepositoryRoot();
        var api = Read(Path.Combine(root,"TomasAI.IFM.Application.Api.Server"),environment);
        var ui = Read(Path.Combine(root,"TomasAI.IFM.UI.Net"),environment);
        var overridden = Environment.GetEnvironmentVariable("IFM_TRADE_RECOVERY_POSTGRES");
        var connection = string.IsNullOrWhiteSpace(overridden)
            ? api.GetConnectionString("TradeDbConnection") : overridden;
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException($"ConnectionStrings:TradeDbConnection is missing from the API {environment} configuration.");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        // Normal application configuration has no inline secrets. Reuse its existing
        // POSTGRES_<environment>_KEY through the API's credential resolver.
        if (string.IsNullOrEmpty(parsed.Username) && string.IsNullOrEmpty(parsed.Password))
            connection = PostgresObjectDataRepositoryConnection.ResolveHostConnectionString(connection);
        var nats = Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")
            ?? api["IFM_TEST_NATS_URL"] ?? ui["AppSettings:NatsServerUri"] ?? new NatsProducerOptions().Url;
        return new(connection,nats);
    }

    static IConfigurationRoot Read(string directory, string environment) => new ConfigurationBuilder()
        .SetBasePath(directory).AddJsonFile("appsettings.json",optional:false)
        .AddJsonFile($"appsettings.{environment}.json",optional:true)
        .AddEnvironmentVariables().Build();

    static string FindRepositoryRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(),AppContext.BaseDirectory })
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName,"TomasAI.IFM.Application.Api.Server","appsettings.json")))
                    return directory.FullName;
        throw new InvalidOperationException("Cannot find the API application settings. Run the tool from the IFM repository.");
    }
}
