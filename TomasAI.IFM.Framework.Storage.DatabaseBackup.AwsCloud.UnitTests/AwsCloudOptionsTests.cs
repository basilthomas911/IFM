using Amazon.DynamoDBv2;
using Amazon.KeyManagementService;
using Amazon.S3;
using Amazon.SecurityToken;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsCloudOptionsTests
{
    [Fact]
    public void Disabled_profile_is_inert_and_accepts_no_identifiers()
    {
        var options = new AwsCloudDatabaseBackupOptions();
        options.Invoking(static value => value.Validate()).Should().NotThrow();
        var services = new ServiceCollection().AddLogging().AddAwsCloudDatabaseBackup(options);
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IAmazonS3));
    }

    [Fact]
    public void Complete_development_profile_registers_one_singleton_client_per_service()
    {
        var services = new ServiceCollection().AddLogging().AddAwsCloudDatabaseBackup(Valid());
        foreach (var service in new[] { typeof(IAmazonS3), typeof(IAmazonDynamoDB), typeof(IAmazonKeyManagementService), typeof(IAmazonSecurityTokenService) })
            services.Where(descriptor => descriptor.ServiceType == service).Should().ContainSingle()
                .Which.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("region")]
    [InlineData("bucket")]
    [InlineData("retention")]
    public void Unsafe_profiles_fail_with_one_bounded_configuration_error(string defect)
    {
        var options = Valid();
        if (defect == "account") options.WorkloadAccountId = "123";
        if (defect == "region") options.PrimaryRegion = "not-a-region";
        if (defect == "bucket") options.RecoveryBucketName = options.PrimaryBucketName;
        if (defect == "retention") options.DefaultRetentionDays = 0;
        options.Invoking(static value => value.Validate()).Should().Throw<InvalidOperationException>()
            .WithMessage("The AwsCloud database-backup profile is incomplete or unsafe:*");
    }

    [Fact]
    public void Production_requires_independent_accounts_regions_and_compliance_lock()
    {
        var options = Valid();
        options.Environment = AwsBackupEnvironment.Production;
        options.ObjectLockMode = "Governance";
        options.Invoking(static value => value.Validate()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Options_contract_has_no_credential_or_secret_fields()
    {
        var names = typeof(AwsCloudDatabaseBackupOptions).GetProperties().Select(static property => property.Name).ToArray();
        names.Should().NotContain(name => name.Contains("AccessKey", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("SessionToken", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    internal static AwsCloudDatabaseBackupOptions Valid() => new()
    {
        Enabled = true,
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
}
