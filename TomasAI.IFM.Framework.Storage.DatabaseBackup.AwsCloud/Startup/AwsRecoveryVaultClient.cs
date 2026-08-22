using Amazon.S3;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;

public sealed class AwsRecoveryVaultClient(IAmazonS3 client) : IDisposable
{
    public IAmazonS3 Client { get; } = client;
    public void Dispose() => Client.Dispose();
}
