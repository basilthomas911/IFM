using Amazon.S3;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;

/// <summary>Separates ordinary read/upload clients from the exact-version deletion role.</summary>
public sealed class AwsRetentionExecutionClients(IAmazonS3 primary, IAmazonS3 recovery) : IDisposable
{
    public IAmazonS3 For(DatabaseArtifactReplicaId replica) => replica.Value switch
    {
        "aws-primary" => primary, "aws-recovery" => recovery,
        _ => throw new InvalidDataException("Unknown retention execution replica.")
    };
    public void Dispose() { primary.Dispose(); recovery.Dispose(); }
}
public sealed class AwsRetentionReadClient(IAmazonS3 client) : IDisposable
{
    public IAmazonS3 Client { get; } = client;
    public void Dispose() => Client.Dispose();
}