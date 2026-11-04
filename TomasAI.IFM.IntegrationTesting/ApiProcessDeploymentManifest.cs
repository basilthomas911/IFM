using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace TomasAI.IFM.IntegrationTesting;

/// <summary>Identifies the actual copied API artifacts used by an owned standalone test process.</summary>
public sealed class ApiProcessDeploymentManifest : IDisposable
{
    readonly string directory = Path.Combine(Path.GetTempPath(), "ifm-api-process-" + Guid.NewGuid().ToString("N"));
    readonly string manifestPath;

    public ApiProcessDeploymentManifest(string artifactDirectory)
    {
        var artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "TomasAI.IFM.Application.Api.Server.dll", "TomasAI.IFM.Application.Api.Server.Core.dll", "TomasAI.IFM.Domain.Trade.dll" })
        {
            using var stream = File.OpenRead(Path.Combine(artifactDirectory, name));
            artifacts.Add(name, Convert.ToHexString(SHA256.HashData(stream)));
        }
        Directory.CreateDirectory(directory);
        manifestPath = Path.Combine(directory, "ifm-deployment-manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
        {
            SchemaVersion = 1, BuildId = string.Join('.', artifacts.Values), GeneratedAtUtc = DateTime.UtcNow, Artifacts = artifacts
        }));
    }

    public void Configure(ProcessStartInfo process)
    {
        ArgumentNullException.ThrowIfNull(process);
        process.Environment["DeploymentIdentity__ManifestPath"] = manifestPath;
    }

    public void Dispose()
    {
        File.Delete(manifestPath);
        Directory.Delete(directory);
    }
}
