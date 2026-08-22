using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Identity;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class LiveAwsIdentityIntegrationTests
{
    [Fact]
    [Trait("Category", "LiveAwsReadOnly")]
    public async Task Default_sdk_credential_chain_resolves_allowlisted_development_identity()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;
        var options = new AwsCloudDatabaseBackupOptions
        {
            Enabled = true,
            LiveAwsTestsEnabled = true,
            WorkloadAccountId = "107651266250",
            PrimaryVaultAccountId = "107651266250",
            RecoveryVaultAccountId = "107651266250",
            PrimaryRegion = "ca-central-1",
            RecoveryRegion = "ca-west-1",
            PrimaryBucketName = "ifm-backup-primary-dev-107651266250",
            RecoveryBucketName = "ifm-backup-recovery-dev-107651266250",
            JournalTableName = "ifm-database-backup-journal-dev",
            UploadRoleArn = "arn:aws:iam::107651266250:role/ifm-backup-upload-dev",
            RecoveryReadRoleArn = "arn:aws:iam::107651266250:role/ifm-backup-recovery-read-dev",
            PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/11111111-1111-1111-1111-111111111111",
            RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/22222222-2222-2222-2222-222222222222",
            SigningKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/33333333-3333-3333-3333-333333333333"
        };
        await using var provider = new ServiceCollection().AddLogging().AddAwsCloudDatabaseBackup(options).BuildServiceProvider();

        var observation = await provider.GetRequiredService<IAwsIdentityPreflight>().VerifyAsync(CancellationToken.None);

        observation.AccountId.Should().Be(options.WorkloadAccountId);
        observation.Partition.Should().Be("aws");
        observation.Region.Should().Be(options.PrimaryRegion);
    }
}
