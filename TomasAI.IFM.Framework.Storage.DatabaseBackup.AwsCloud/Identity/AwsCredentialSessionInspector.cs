using Amazon.Runtime;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Identity;

public enum AwsCredentialSessionKind { StaticDevelopment, TemporarySession }

public sealed class AwsCredentialSessionInspector(
    AWSCredentials credentials,
    AwsCloudDatabaseBackupOptions options)
{
    public AwsCredentialSessionKind Inspect()
    {
        ImmutableCredentials value;
        try { value = credentials.GetCredentials(); }
        catch (Exception exception) when (exception is AmazonClientException or InvalidOperationException)
        {
            throw new AwsIdentityRejectedException("The AWS default credential chain did not resolve usable credentials.");
        }
        if (string.IsNullOrWhiteSpace(value.AccessKey) || string.IsNullOrWhiteSpace(value.SecretKey))
            throw new AwsIdentityRejectedException("The AWS default credential chain returned incomplete credentials.");
        var temporary = !string.IsNullOrWhiteSpace(value.Token);
        if (options.Environment != AwsBackupEnvironment.Development && !temporary)
            throw new AwsIdentityRejectedException("Staging and production require temporary session credentials.");
        return temporary ? AwsCredentialSessionKind.TemporarySession : AwsCredentialSessionKind.StaticDevelopment;
    }
}
