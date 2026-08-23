using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsExactVersionDeletionExecutorTests
{
    static readonly DateTimeOffset Now = new(2026, 8, 23, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Gate14")]
    public async Task Executor_deletes_only_the_signature_verified_plan_object_version()
    {
        var s3 = Substitute.For<IAmazonS3>();
        s3.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DeleteObjectResponse());
        var executor = new AwsExactVersionDeletionExecutor(s3);
        var approved = Approved([PlanObject("one")]);

        var result = await executor.ExecuteAsync(approved, CancellationToken.None);

        result.DeletedObjectVersionCount.Should().Be(1);
        result.DeletedBytes.Should().Be(16);
        await s3.Received(1).DeleteObjectAsync(
            Arg.Is<DeleteObjectRequest>(request =>
                request.BucketName == "ifm-primary-development"
                && request.Key == "v1/one"
                && request.VersionId == "version-one"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Gate14")]
    public async Task Partial_failure_stops_and_reports_only_completed_exact_versions()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var calls = 0;
        s3.DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1
                ? Task.FromResult(new DeleteObjectResponse())
                : Task.FromException<DeleteObjectResponse>(new InvalidOperationException("injected")));
        var executor = new AwsExactVersionDeletionExecutor(s3);
        var approved = Approved([PlanObject("one"), PlanObject("two"), PlanObject("three")]);

        var action = () => executor.ExecuteAsync(approved, CancellationToken.None).AsTask();

        var exception = (await action.Should().ThrowAsync<AwsPartialRetentionExecutionException>()).Which;
        exception.Completed.Select(static value => value.VersionId).Should().Equal("version-one");
        await s3.Received(2).DeleteObjectAsync(Arg.Any<DeleteObjectRequest>(), Arg.Any<CancellationToken>());
    }

    static AwsApprovedRetentionExecution Approved(AwsRetentionPlanObject[] objects)
    {
        var options = new AwsCloudDatabaseBackupOptions
        {
            PrimaryBucketName = "ifm-primary-development",
            RecoveryBucketName = "ifm-recovery-development",
            PrimaryRegion = "ca-central-1",
            RecoveryRegion = "ca-west-1"
        };
        var planId = new DatabaseRetentionPlanId(Guid.NewGuid());
        var plan = new AwsRetentionPlanDocument
        {
            PlanId = planId,
            Revision = 1,
            PolicyRevision = 2,
            ReplicaId = new DatabaseArtifactReplicaId("aws-primary"),
            EvaluationBoundaryUtc = Now.AddDays(-1),
            CreatedUtc = Now.AddHours(-1),
            RestorePoints = [new AwsRetentionPlanRestorePoint(
                new DatabaseRestorePointId("old"), DatabaseEngine.PostgreSql, objects)],
            DependencyProtectedRestorePoints = [],
            RetainedRestorePointCount = 1,
            ExpectedReclaimedBytes = objects.Sum(static value => value.Length)
        };
        var observations = objects.Select(static value => new AwsRetentionObjectObservation(
            value.BucketName,
            value.Region,
            value.ObjectKey,
            value.VersionId,
            value.Length,
            value.Sha256,
            value.ObservedRetainUntilUtc,
            LegalHold: false,
            RequiredReplicaComplete: true)).ToArray();
        return AwsRetentionPlanPolicy.ValidateExecution(
            plan,
            new AwsRetentionExecutionApproval(planId, 1, 2, "CAB-1"),
            observations,
            options,
            Now);
    }

    static AwsRetentionPlanObject PlanObject(string id) => new(
        "ifm-primary-development",
        "ca-central-1",
        $"v1/{id}",
        $"version-{id}",
        16,
        new string('A', 64),
        Now.AddMinutes(-1));
}
