using Amazon;
using Amazon.DynamoDBv2;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using Amazon.SecurityToken;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Identity;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Processing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;

public static class AwsCloudServiceCollectionExtensions
{
    public static IServiceCollection AddAwsCloudDatabaseBackup(
        this IServiceCollection services,
        AwsCloudDatabaseBackupOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        services.AddSingleton(options);
        if (!options.Enabled) return services;

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AWSCredentials>(_ => CreateCredentials(options));
        services.AddSingleton<IAmazonSecurityTokenService>(provider => new AmazonSecurityTokenServiceClient(
            provider.GetRequiredService<AWSCredentials>(), ServiceConfig<AmazonSecurityTokenServiceConfig>(options.PrimaryRegion, options)));
        services.AddSingleton<IAmazonS3>(provider => new AmazonS3Client(
            provider.GetRequiredService<AWSCredentials>(), ServiceConfig<AmazonS3Config>(options.PrimaryRegion, options)));
        services.AddSingleton<IAmazonDynamoDB>(provider => new AmazonDynamoDBClient(
            provider.GetRequiredService<AWSCredentials>(), ServiceConfig<AmazonDynamoDBConfig>(options.PrimaryRegion, options)));
        services.AddSingleton<IAmazonKeyManagementService>(provider => new AmazonKeyManagementServiceClient(
            provider.GetRequiredService<AWSCredentials>(), ServiceConfig<AmazonKeyManagementServiceConfig>(options.PrimaryRegion, options)));
        services.AddSingleton<IAwsIdentityPreflight, AwsIdentityPreflight>();
        services.AddSingleton<AwsCredentialSessionInspector>();
        services.AddSingleton<AwsCloudDatabaseRecoveryProcessor>();
        services.AddSingleton<IDatabaseRecoveryProcessor>(provider => provider.GetRequiredService<AwsCloudDatabaseRecoveryProcessor>());
        return services;
    }

    static AWSCredentials CreateCredentials(AwsCloudDatabaseBackupOptions options)
    {
        var source = DefaultAWSCredentialsIdentityResolver.GetCredentials(
            ServiceConfig<AmazonSecurityTokenServiceConfig>(options.PrimaryRegion, options));
        return string.IsNullOrWhiteSpace(options.IdentityRoleArn)
            ? source
            : new AssumeRoleAWSCredentials(source, options.IdentityRoleArn, $"ifm-backup-{Environment.ProcessId}");
    }

    static T ServiceConfig<T>(string region, AwsCloudDatabaseBackupOptions options) where T : ClientConfig, new()
        => new()
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
            Timeout = options.ApiTimeout,
            MaxErrorRetry = options.MaximumSdkRetries
        };
}
