using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

BenchmarkRunner.Run<CanonicalManifestBenchmarks>();

[MemoryDiagnoser]
public class CanonicalManifestBenchmarks
{
    readonly DatabaseBackupManifest _manifest;
    readonly byte[] _serialized;

    public CanonicalManifestBenchmarks()
    {
        var operation = new DatabaseRecoveryOperationId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var restorePoint = new DatabaseRestorePointId(operation.Format());
        _manifest = new()
        {
            ManifestId = "benchmark-manifest",
            OperationId = operation,
            RestorePointId = restorePoint,
            Source = BackupSource.AwsCloud,
            Engine = DatabaseEngine.PostgreSql,
            ProtectionSetId = new("core-postgresql"),
            SafeBoundaryReference = "benchmark-boundary",
            CreatedUtc = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero),
            Artifacts = Enumerable.Range(0, 1_000).Select(index => new DatabaseArtifactDigest(
                $"artifacts/{index:D5}.bin", 1_048_576, new string((char)('a' + index % 6), 64))).ToArray(),
            Replicas = [new("primary-vault"), new("recovery-vault")],
            BackupLineage = new()
            {
                RequestedMode = DatabaseBackupMode.Full,
                ResolvedMode = DatabaseBackupMode.Full,
                NativeKind = DatabaseNativeBackupKind.PostgreSqlBase,
                BaseRestorePointId = restorePoint,
                ChainDepth = 0
            }
        };
        _serialized = DatabaseBackupCanonicalJson.Serialize(_manifest);
    }

    [Benchmark(Baseline = true)] public byte[] Serialize() => DatabaseBackupCanonicalJson.Serialize(_manifest);
    [Benchmark] public DatabaseBackupManifest Deserialize() => DatabaseBackupCanonicalJson.Deserialize<DatabaseBackupManifest>(_serialized);
    [Benchmark] public void Validate() => DatabaseBackupManifestPolicy.Validate(_manifest);
}
