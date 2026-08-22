using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Identity;

public sealed record AwsIdentityObservation(
    string AccountId,
    string PrincipalArn,
    string Partition,
    string Region,
    string? RequestId,
    DateTimeOffset ObservedUtc);

public interface IAwsIdentityPreflight
{
    ValueTask<AwsIdentityObservation> VerifyAsync(CancellationToken cancellationToken);
}

public sealed class AwsIdentityPreflight(
    IAmazonSecurityTokenService sts,
    AwsCloudDatabaseBackupOptions options,
    AwsCredentialSessionInspector credentials,
    TimeProvider timeProvider,
    ILogger<AwsIdentityPreflight> logger) : IAwsIdentityPreflight
{
    public async ValueTask<AwsIdentityObservation> VerifyAsync(CancellationToken cancellationToken)
    {
        options.Validate();
        _ = credentials.Inspect();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.ApiTimeout);
        var response = await sts.GetCallerIdentityAsync(new GetCallerIdentityRequest(), timeout.Token).ConfigureAwait(false);
        var arn = response.Arn ?? throw new AwsIdentityRejectedException("STS did not return a principal ARN.");
        var partition = arn.Split(':', 3) is ["arn", var value, _] ? value : string.Empty;
        if (!partition.Equals("aws", StringComparison.Ordinal))
            throw new AwsIdentityRejectedException("The AWS partition is not allowlisted.");
        if (!string.Equals(response.Account, options.WorkloadAccountId, StringComparison.Ordinal))
            throw new AwsIdentityRejectedException("The caller account is not allowlisted for this profile.");
        if (!options.PrimaryRegion.Equals(sts.Config.RegionEndpoint?.SystemName, StringComparison.OrdinalIgnoreCase))
            throw new AwsIdentityRejectedException("The STS client Region does not match the configured primary Region.");
        var observation = new AwsIdentityObservation(
            response.Account, arn, partition, options.PrimaryRegion,
            response.ResponseMetadata?.RequestId, timeProvider.GetUtcNow());
        logger.LogInformation(
            "AWS backup identity preflight accepted account {AccountId}, principal {PrincipalArn}, partition {Partition}, Region {Region}, request {AwsRequestId}.",
            observation.AccountId, observation.PrincipalArn, observation.Partition, observation.Region, observation.RequestId);
        return observation;
    }
}

public sealed class AwsIdentityRejectedException(string message) : InvalidOperationException(message);
