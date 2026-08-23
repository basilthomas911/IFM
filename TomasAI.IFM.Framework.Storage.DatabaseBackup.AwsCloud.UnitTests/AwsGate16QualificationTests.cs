using System.Net;
using Amazon.S3;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Identity;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Observability;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsGate16QualificationTests
{
    [Theory]
    [InlineData("ThrottlingException", HttpStatusCode.TooManyRequests, AwsFailureKind.Throttled, true)]
    [InlineData("AccessDenied", HttpStatusCode.Forbidden, AwsFailureKind.AccessDenied, false)]
    [InlineData("ExpiredToken", HttpStatusCode.Forbidden, AwsFailureKind.ExpiredCredentials, true)]
    [InlineData("InternalError", HttpStatusCode.InternalServerError, AwsFailureKind.Transient, true)]
    [Trait("Category", "Gate16")]
    public void Aws_faults_have_bounded_retry_or_fail_closed_semantics(
        string code,
        HttpStatusCode status,
        AwsFailureKind expected,
        bool retryable)
    {
        var exception = new AmazonS3Exception("injected")
        {
            ErrorCode = code,
            StatusCode = status,
            RequestId = "request-id"
        };

        var result = AwsFailureClassifier.Classify(exception);

        result.Kind.Should().Be(expected);
        result.Retryable.Should().Be(retryable);
        result.Code.Should().Be(code);
    }

    [Fact]
    [Trait("Category", "Gate16")]
    public void Transport_partition_is_retryable_but_corrupt_evidence_is_not()
    {
        AwsFailureClassifier.Classify(new HttpRequestException("dns"))
            .Should().Match<AwsFailureObservation>(static value =>
                value.Kind == AwsFailureKind.Transient && value.Retryable);
        AwsFailureClassifier.Classify(new InvalidDataException("corrupt"))
            .Should().Match<AwsFailureObservation>(static value =>
                value.Kind == AwsFailureKind.Permanent && !value.Retryable);
    }

    [Theory]
    [InlineData(0, 4L * 1024 * 1024 * 1024, 512L * 1024 * 1024 * 1024, 100)]
    [InlineData(5, 4L * 1024 * 1024 * 1024, 512L * 1024 * 1024 * 1024, 100)]
    [InlineData(1, 1, 512L * 1024 * 1024 * 1024, 100)]
    [InlineData(1, 4L * 1024 * 1024 * 1024, 1024, 100)]
    [InlineData(1, 4L * 1024 * 1024 * 1024, 512L * 1024 * 1024 * 1024, 0)]
    [Trait("Category", "Gate16")]
    public void Unsafe_concurrency_capacity_or_cost_bound_is_rejected(
        int concurrency,
        long reserve,
        long maximumScylla,
        decimal budget)
    {
        var options = ValidOptions();
        options.MaximumConcurrentOperations = concurrency;
        options.MinimumStagingFreeBytes = reserve;
        options.MaximumScyllaProtectionSetBytes = maximumScylla;
        options.MonthlyCostBudgetUsd = budget;

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>().WithMessage("*capacity*");
    }

    [Fact]
    [Trait("Category", "Gate16")]
    public void Cost_model_covers_storage_replication_requests_kms_dynamodb_audit_retrieval_egress_and_drills()
    {
        const long gb = 1024L * 1024 * 1024;
        var usage = new AwsBackupMonthlyUsage(
            10 * gb, 10 * gb, 10 * gb, gb, gb, 10_000, 5_000, 1_000_000, 100_000, 4);
        var rates = new AwsBackupUnitRates(
            0.02m, 0.02m, 0.01m, 0.03m, 0.05m, 0.005m, 0.003m, 0.25m, 0.10m, 0.50m);

        var cost = AwsBackupCostModel.Estimate(usage, rates);

        cost.Storage.Should().Be(0.40m);
        cost.Replication.Should().Be(0.10m);
        cost.Retrieval.Should().Be(0.03m);
        cost.Egress.Should().Be(0.05m);
        cost.Requests.Should().Be(0.05m);
        cost.Kms.Should().Be(0.015m);
        cost.DynamoDb.Should().Be(0.25m);
        cost.Audit.Should().Be(0.10m);
        cost.RestoreDrills.Should().Be(2m);
        AwsBackupCostModel.EnsureWithinBudget(cost, 100m);
        var overBudget = () => AwsBackupCostModel.EnsureWithinBudget(cost, 1m);
        overBudget.Should().Throw<InvalidOperationException>().WithMessage("*exceeds*");
    }

    static AwsCloudDatabaseBackupOptions ValidOptions() => new()
    {
        Enabled = true,
        Environment = AwsBackupEnvironment.Development,
        WorkloadAccountId = "107651266250",
        PrimaryVaultAccountId = "107651266250",
        RecoveryVaultAccountId = "107651266250",
        PrimaryRegion = "ca-central-1",
        RecoveryRegion = "ca-west-1",
        PrimaryBucketName = "ifm-primary-development",
        RecoveryBucketName = "ifm-recovery-development",
        JournalTableName = "ifm-database-backup-journal-development",
        UploadRoleArn = "arn:aws:iam::107651266250:role/upload",
        RecoveryReadRoleArn = "arn:aws:iam::107651266250:role/recovery",
        PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/primary",
        RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/recovery",
        SigningKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/signing",
        WalSpoolPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ifm-wal"))
    };
}
