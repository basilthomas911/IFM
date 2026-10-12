using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Journal;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsEmptyRetentionExecutionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Empty_plan_verifies_signature_and_identity_without_catalog_download_or_deletion(
        bool invalidSignature, bool wrongRevision)
    {
        var options = new AwsCloudDatabaseBackupOptions { JournalTableName = "test-journal", PrimaryBucketName = "test-vault" };
        var now = DateTimeOffset.UtcNow;
        var planId = new DatabaseRetentionPlanId(Guid.NewGuid());
        var replica = new DatabaseArtifactReplicaId("aws-primary");
        var plan = new AwsRetentionPlanDocument { PlanId = planId, Revision = 1, PolicyRevision = 1,
            ReplicaId = replica, EvaluationBoundaryUtc = now, CreatedUtc = now, RestorePoints = [],
            DependencyProtectedRestorePoints = [], RetainedRestorePointCount = 1, ExpectedReclaimedBytes = 0 };
        var signature = new AwsSignatureEnvelope { KeyArn = "test", Algorithm = "test", DigestAlgorithm = "test",
            DigestBase64 = "", SignatureBase64 = "", SignedUtc = now };
        var request = new DatabaseRetentionEvaluationRequest(planId, 1, now, replica, [], [], []);
        var content = DatabaseBackupCanonicalJson.Serialize(new AwsStoredRetentionPlan(request, plan, signature));
        var signatureContent = DatabaseBackupCanonicalJson.Serialize(signature);
        var s3 = Substitute.For<IAmazonS3>();
        s3.ListVersionsAsync(Arg.Any<ListVersionsRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var key = call.Arg<ListVersionsRequest>().Prefix;
            Assert.Contains("retention-plan-", key);
            return Task.FromResult(new ListVersionsResponse { Versions = [new S3ObjectVersion { Key = key, VersionId = "one" }] });
        });
        s3.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var bytes = call.Arg<GetObjectRequest>().Key.EndsWith(".signature.json") ? signatureContent : content;
            return Task.FromResult(new GetObjectResponse { ContentLength = bytes.Length, ResponseStream = new MemoryStream(bytes) });
        });
        var signatures = Substitute.For<IAwsDocumentSignatureService>();
        if (invalidSignature) signatures.VerifyAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<AwsSignatureEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException(new InvalidDataException("invalid signature")));
        var dynamo = Substitute.For<IAmazonDynamoDB>();
        dynamo.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>()).Returns(new QueryResponse { Items = [] });
        var clock = TimeProvider.System;
        var journal = new DynamoDbDatabaseBackupExecutionJournal(dynamo, options, new DatabaseBackupHostOptions { HostId = "test-host" }, clock);
        var store = new S3ImmutableObjectStore(s3, options, clock);
        var evidence = new AwsRecoveryEvidenceStore(s3, store, signatures, options, clock);
        var deletion = Substitute.For<IAmazonS3>();
        var capability = new AwsCloudRetentionCapability(new AwsRetentionReadClient(s3), new AwsRecoveryVaultClient(s3),
            new AwsRetentionExecutionClients(deletion, deletion), signatures, evidence,
            new AwsRetentionPlanAuthorizationService(signatures, options, clock), journal, options, clock);
        var execute = new DatabaseRetentionExecutionRequest(planId, wrongRevision ? 2 : 1, replica, "test-approval");
        if (invalidSignature || wrongRevision)
            await Assert.ThrowsAsync<InvalidDataException>(() => capability.ExecuteAsync(execute, default).AsTask());
        else
        {
            var result = await capability.ExecuteAsync(execute, default);
            Assert.Equal(0, result.DeletedFileCount);
            Assert.Equal(0, result.DeletedBytes);
        }
        await signatures.Received(1).VerifyAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<AwsSignatureEnvelope>(), Arg.Any<CancellationToken>());
        await s3.DidNotReceiveWithAnyArgs().GetObjectMetadataAsync(default(GetObjectMetadataRequest)!, default);
        Assert.Empty(deletion.ReceivedCalls());
    }
}