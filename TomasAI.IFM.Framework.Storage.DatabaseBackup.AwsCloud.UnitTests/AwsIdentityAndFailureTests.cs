using System.Net;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Identity;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsIdentityAndFailureTests
{
    [Fact]
    public async Task Preflight_returns_only_safe_identity_metadata()
    {
        var options = AwsCloudOptionsTests.Valid();
        var sts = Substitute.For<IAmazonSecurityTokenService>();
        sts.Config.RegionEndpoint.Returns(Amazon.RegionEndpoint.CACentral1);
        sts.GetCallerIdentityAsync(Arg.Any<GetCallerIdentityRequest>(), Arg.Any<CancellationToken>()).Returns(new GetCallerIdentityResponse
        {
            Account = options.WorkloadAccountId,
            Arn = $"arn:aws:iam::{options.WorkloadAccountId}:role/test",
            ResponseMetadata = new ResponseMetadata { RequestId = "request-1" }
        });
        var service = new AwsIdentityPreflight(sts, options,
            new AwsCredentialSessionInspector(new BasicAWSCredentials("test-id", "test-secret"), options),
            TimeProvider.System, NullLogger<AwsIdentityPreflight>.Instance);

        var result = await service.VerifyAsync(CancellationToken.None);

        result.AccountId.Should().Be(options.WorkloadAccountId);
        result.Partition.Should().Be("aws");
        result.ToString().ToLowerInvariant().Should().NotContain("secret");
    }

    [Fact]
    public async Task Wrong_account_is_rejected_before_any_mutating_client_is_used()
    {
        var options = AwsCloudOptionsTests.Valid();
        var sts = Substitute.For<IAmazonSecurityTokenService>();
        sts.Config.RegionEndpoint.Returns(Amazon.RegionEndpoint.CACentral1);
        sts.GetCallerIdentityAsync(Arg.Any<GetCallerIdentityRequest>(), Arg.Any<CancellationToken>()).Returns(new GetCallerIdentityResponse
        { Account = "000000000000", Arn = "arn:aws:iam::000000000000:role/wrong" });
        var service = new AwsIdentityPreflight(sts, options,
            new AwsCredentialSessionInspector(new BasicAWSCredentials("test-id", "test-secret"), options),
            TimeProvider.System, NullLogger<AwsIdentityPreflight>.Instance);

        var action = async () => await service.VerifyAsync(CancellationToken.None);
        await action.Should().ThrowAsync<AwsIdentityRejectedException>().WithMessage("*not allowlisted*");
    }

    [Theory]
    [InlineData("ThrottlingException", HttpStatusCode.BadRequest, AwsFailureKind.Throttled, true)]
    [InlineData("AccessDeniedException", HttpStatusCode.Forbidden, AwsFailureKind.AccessDenied, false)]
    [InlineData("ExpiredTokenException", HttpStatusCode.Forbidden, AwsFailureKind.ExpiredCredentials, true)]
    [InlineData("InternalFailure", HttpStatusCode.InternalServerError, AwsFailureKind.Transient, true)]
    public void Aws_failures_are_classified_without_rethrow_loops(string code, HttpStatusCode status, AwsFailureKind kind, bool retryable)
    {
        var exception = new AmazonServiceException("safe") { ErrorCode = code, StatusCode = status, RequestId = "request-2" };
        AwsFailureClassifier.Classify(exception).Should().Be(new AwsFailureObservation(kind, retryable, code, "request-2"));
    }

    [Fact]
    public void Absent_default_credentials_are_a_non_retrying_configuration_observation()
    {
        AwsFailureClassifier.Classify(new AmazonClientException("credentials unavailable"))
            .Should().Be(new AwsFailureObservation(AwsFailureKind.Configuration, false, "credential-chain", null));
    }

    [Fact]
    public void Static_development_and_temporary_sessions_are_distinguished_without_exposing_values()
    {
        var development = AwsCloudOptionsTests.Valid();
        new AwsCredentialSessionInspector(new BasicAWSCredentials("test-id", "test-secret"), development)
            .Inspect().Should().Be(AwsCredentialSessionKind.StaticDevelopment);
        var staging = AwsCloudOptionsTests.Valid();
        staging.Environment = AwsBackupEnvironment.Staging;
        new AwsCredentialSessionInspector(new SessionAWSCredentials("test-id", "test-secret", "test-token"), staging)
            .Inspect().Should().Be(AwsCredentialSessionKind.TemporarySession);
    }

    [Fact]
    public void Static_staging_credentials_are_rejected()
    {
        var options = AwsCloudOptionsTests.Valid();
        options.Environment = AwsBackupEnvironment.Staging;
        var inspector = new AwsCredentialSessionInspector(new BasicAWSCredentials("test-id", "test-secret"), options);
        inspector.Invoking(static value => value.Inspect()).Should().Throw<AwsIdentityRejectedException>()
            .WithMessage("*temporary session*");
    }

    [Fact]
    public async Task Wrong_region_is_rejected()
    {
        var options = AwsCloudOptionsTests.Valid();
        var sts = Substitute.For<IAmazonSecurityTokenService>();
        sts.Config.RegionEndpoint.Returns(Amazon.RegionEndpoint.USWest2);
        sts.GetCallerIdentityAsync(Arg.Any<GetCallerIdentityRequest>(), Arg.Any<CancellationToken>()).Returns(new GetCallerIdentityResponse
        { Account = options.WorkloadAccountId, Arn = $"arn:aws:iam::{options.WorkloadAccountId}:role/test" });
        var service = new AwsIdentityPreflight(sts, options,
            new AwsCredentialSessionInspector(new BasicAWSCredentials("test-id", "test-secret"), options),
            TimeProvider.System, NullLogger<AwsIdentityPreflight>.Instance);
        var action = async () => await service.VerifyAsync(CancellationToken.None);
        await action.Should().ThrowAsync<AwsIdentityRejectedException>().WithMessage("*Region*");
    }
}
