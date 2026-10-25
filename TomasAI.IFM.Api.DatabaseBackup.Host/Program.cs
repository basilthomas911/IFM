using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;

namespace TomasAI.IFM.Api.DatabaseBackup.Host;

internal static class Program
{
    static async Task Main(string[] args)
    {
        var enroll = args.Contains("--enroll-development-vault", StringComparer.Ordinal);
        var builder = WebApplication.CreateBuilder(args.Where(value => value != "--enroll-development-vault").ToArray());
        builder.Services.AddDatabaseBackupHost(builder.Configuration);
        await using var host = builder.Build();
        if (enroll)
        {
            var options = builder.Configuration.GetSection(DatabaseBackupPublicationOptions.SectionName)
                .Get<DatabaseBackupPublicationOptions>() ?? throw new InvalidOperationException("Publication configuration is required.");
            if (!string.Equals(options.EnvironmentId, "development", StringComparison.Ordinal))
                throw new InvalidOperationException("Explicit vault enrollment is restricted to Development.");
            var root = options.OnlineVault.ResolveRoot();
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
                throw new InvalidOperationException("The configured vault must be empty; existing enrollment or artifacts cannot be replaced.");
            var identity = await host.Services.GetRequiredService<ILocalBackupVault>().EnrollAsync(
                new DatabaseMediaEnrollmentRequest(options.OnlineVault.MediaId,
                    new DatabaseArtifactReplicaId(options.OnlineVault.ReplicaId), options.EnvironmentId,
                    "online", DateTimeOffset.UtcNow), CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine($"Development vault enrolled and signature verified: {identity.MediaId}, {identity.ReplicaId}, {identity.SigningKeyId}");
            return;
        }
        host.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains("live")
        });
        host.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains("ready")
        });
        await host.RunAsync().ConfigureAwait(false);
    }
}
