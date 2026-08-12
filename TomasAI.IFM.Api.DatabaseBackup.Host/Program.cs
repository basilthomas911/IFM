using Microsoft.Extensions.Hosting;

namespace TomasAI.IFM.Api.DatabaseBackup.Host;

internal static class Program
{
    static async Task Main(string[] args)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        builder.Services.AddDatabaseBackupHost(builder.Configuration);
        using var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}
