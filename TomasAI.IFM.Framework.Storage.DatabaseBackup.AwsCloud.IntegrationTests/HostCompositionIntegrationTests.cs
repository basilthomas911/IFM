using Amazon.S3;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Api.DatabaseBackup.Host;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class HostCompositionIntegrationTests
{
    [Fact]
    public void Aws_disabled_startup_registers_no_aws_api_client_and_preserves_local_processor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDatabaseBackupHost(new ConfigurationBuilder().Build());

        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IAmazonS3));
        services.Where(descriptor => descriptor.ServiceType == typeof(IDatabaseRecoveryProcessor)).Should().ContainSingle();
    }

    [Fact]
    public void Local_and_aws_sources_can_be_registered_together_without_resolving_credentials()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(ValidAwsConfiguration()).Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDatabaseBackupHost(configuration);

        services.Where(descriptor => descriptor.ServiceType == typeof(IDatabaseRecoveryProcessor)).Should().HaveCount(2);
        services.Where(descriptor => descriptor.ServiceType == typeof(IAmazonS3)).Should().ContainSingle();
    }

    [Fact]
    public void Incomplete_enabled_profile_fails_once_during_composition()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseBackup:Sources:AwsCloud:Enabled"] = "true"
        }).Build();
        var services = new ServiceCollection().AddLogging();

        var action = () => services.AddDatabaseBackupHost(configuration);

        action.Should().Throw<InvalidOperationException>().WithMessage("The AwsCloud database-backup profile is incomplete or unsafe:*");
    }

    static Dictionary<string, string?> ValidAwsConfiguration() => new()
    {
        ["DatabaseBackup:Sources:AwsCloud:Enabled"] = "true",
        ["DatabaseBackup:Sources:AwsCloud:WorkloadAccountId"] = "107651266250",
        ["DatabaseBackup:Sources:AwsCloud:PrimaryVaultAccountId"] = "107651266250",
        ["DatabaseBackup:Sources:AwsCloud:RecoveryVaultAccountId"] = "107651266250",
        ["DatabaseBackup:Sources:AwsCloud:PrimaryRegion"] = "ca-central-1",
        ["DatabaseBackup:Sources:AwsCloud:RecoveryRegion"] = "ca-west-1",
        ["DatabaseBackup:Sources:AwsCloud:PrimaryBucketName"] = "ifm-primary-dev-107651266250",
        ["DatabaseBackup:Sources:AwsCloud:RecoveryBucketName"] = "ifm-recovery-dev-107651266250",
        ["DatabaseBackup:Sources:AwsCloud:JournalTableName"] = "ifm-database-backup-journal-dev",
        ["DatabaseBackup:Sources:AwsCloud:UploadRoleArn"] = "arn:aws:iam::107651266250:role/ifm-upload-dev",
        ["DatabaseBackup:Sources:AwsCloud:RecoveryReadRoleArn"] = "arn:aws:iam::107651266250:role/ifm-recovery-dev",
        ["DatabaseBackup:Sources:AwsCloud:PrimaryEncryptionKeyArn"] = "arn:aws:kms:ca-central-1:107651266250:key/11111111-1111-1111-1111-111111111111",
        ["DatabaseBackup:Sources:AwsCloud:RecoveryEncryptionKeyArn"] = "arn:aws:kms:ca-west-1:107651266250:key/22222222-2222-2222-2222-222222222222",
        ["DatabaseBackup:Sources:AwsCloud:SigningKeyArn"] = "arn:aws:kms:ca-central-1:107651266250:key/33333333-3333-3333-3333-333333333333"
    };
}
