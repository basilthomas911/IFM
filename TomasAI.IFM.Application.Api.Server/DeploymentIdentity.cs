using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace TomasAI.IFM.Application.Api.Server;

public sealed record DeploymentIdentityOptions
{
    public const string SectionName = "DeploymentIdentity";
    public const string DefaultManifestFileName = "ifm-deployment-manifest.json";

    public string ManifestPath { get; init; } = string.Empty;
    public string[] RequiredArtifacts { get; init; } =
    [
        "TomasAI.IFM.Application.Api.Server.dll",
        "TomasAI.IFM.Domain.Trade.dll"
    ];
}

public sealed record DeploymentIdentityValidation(
    bool Enforced,
    bool Valid,
    string BuildId,
    string ManifestPath,
    DateTime CapturedAtUtc,
    IReadOnlyDictionary<string, string> ProcessStartHashes,
    IReadOnlyList<string> Errors);

public sealed class DeploymentIdentityMonitor
{
    readonly DeploymentIdentityOptions _options;
    readonly string _baseDirectory;
    readonly string _manifestPath;
    readonly bool _enforced;
    readonly DateTime _capturedAtUtc;
    readonly IReadOnlyDictionary<string, string> _processStartHashes;

    public DeploymentIdentityMonitor(DeploymentIdentityOptions options)
        : this(options, AppContext.BaseDirectory,
            string.Equals(Assembly.GetEntryAssembly()?.GetName().Name,
                typeof(DeploymentIdentityMonitor).Assembly.GetName().Name,
                StringComparison.OrdinalIgnoreCase))
    {
    }

    internal DeploymentIdentityMonitor(
        DeploymentIdentityOptions options,
        string baseDirectory,
        bool standaloneApiProcess)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.RequiredArtifacts is null || options.RequiredArtifacts.Length == 0)
            throw new InvalidOperationException("Deployment identity requires at least one artifact.");

        _options = options;
        _baseDirectory = Path.GetFullPath(baseDirectory);
        _manifestPath = Path.GetFullPath(string.IsNullOrWhiteSpace(options.ManifestPath)
            ? Path.Combine(_baseDirectory, DeploymentIdentityOptions.DefaultManifestFileName)
            : options.ManifestPath);
        _enforced = standaloneApiProcess;
        _capturedAtUtc = DateTime.UtcNow;
        _processStartHashes = CaptureArtifactHashes();
    }

    public DeploymentIdentityValidation Validate()
    {
        if (!_enforced)
            return new(false, true, "in-process-test-host", _manifestPath, _capturedAtUtc,
                _processStartHashes, []);

        var errors = new List<string>();
        var manifest = ReadManifest(errors);
        var currentHashes = CaptureArtifactHashes(errors);
        if (manifest is not null)
        {
            foreach (var artifact in _options.RequiredArtifacts.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(artifact);
                if (!manifest.Artifacts.TryGetValue(fileName, out var expected))
                {
                    errors.Add($"Manifest does not contain required artifact '{fileName}'.");
                    continue;
                }

                if (!_processStartHashes.TryGetValue(fileName, out var loaded))
                    errors.Add($"Process-start hash for '{fileName}' is unavailable.");
                else if (!string.Equals(loaded, expected, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Process loaded a stale '{fileName}' artifact.");

                if (!currentHashes.TryGetValue(fileName, out var current))
                    errors.Add($"Current deployed hash for '{fileName}' is unavailable.");
                else if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Current '{fileName}' artifact does not match the deployment manifest.");
            }
        }

        return new(true, errors.Count == 0, manifest?.BuildId ?? string.Empty, _manifestPath,
            _capturedAtUtc, _processStartHashes, errors);
    }

    public DeploymentIdentityValidation EnsureStartupValid()
    {
        var validation = Validate();
        if (!validation.Valid)
            throw new InvalidOperationException(
                "Deployment identity validation failed: " + string.Join(" ", validation.Errors));
        return validation;
    }

    DeploymentIdentityManifest? ReadManifest(List<string> errors)
    {
        if (!File.Exists(_manifestPath))
        {
            errors.Add($"Deployment manifest '{_manifestPath}' was not found.");
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<DeploymentIdentityManifest>(
                File.ReadAllText(_manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest is null || manifest.SchemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.BuildId)
                || manifest.Artifacts is null)
            {
                errors.Add("Deployment manifest is empty or has an unsupported schema.");
                return null;
            }

            manifest.Artifacts = new Dictionary<string, string>(manifest.Artifacts,
                StringComparer.OrdinalIgnoreCase);
            return manifest;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            errors.Add($"Deployment manifest could not be read: {exception.GetType().Name}.");
            return null;
        }
    }

    IReadOnlyDictionary<string, string> CaptureArtifactHashes(List<string>? errors = null)
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in _options.RequiredArtifacts.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(artifact);
            var path = Path.GetFullPath(Path.Combine(_baseDirectory, artifact));
            try
            {
                if (!File.Exists(path))
                {
                    errors?.Add($"Required deployment artifact '{fileName}' was not found.");
                    continue;
                }

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                hashes[fileName] = Convert.ToHexString(SHA256.HashData(stream));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors?.Add($"Required deployment artifact '{fileName}' could not be hashed: {exception.GetType().Name}.");
            }
        }

        return hashes;
    }

    sealed class DeploymentIdentityManifest
    {
        public int SchemaVersion { get; init; }
        public string BuildId { get; init; } = string.Empty;
        public DateTime GeneratedAtUtc { get; init; }
        public Dictionary<string, string> Artifacts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
