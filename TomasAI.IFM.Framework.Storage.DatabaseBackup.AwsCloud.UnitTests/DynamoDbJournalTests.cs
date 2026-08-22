using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Journal;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class DynamoDbJournalTests
{
    [Fact]
    public async Task New_admission_is_one_idempotent_transaction_containing_operation_inbox_and_outbox()
    {
        var dynamo = Substitute.For<IAmazonDynamoDB>();
        dynamo.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = [] });
        TransactWriteItemsRequest? captured = null;
        dynamo.TransactWriteItemsAsync(Arg.Do<TransactWriteItemsRequest>(request => captured = request), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());
        var journal = Create(dynamo);

        var result = await journal.AdmitAsync(Intent(), CancellationToken.None);

        result.Outcome.Should().Be(JournalAdmissionOutcome.Admitted);
        captured.Should().NotBeNull();
        captured!.TransactItems.Should().HaveCount(3);
        captured.TransactItems.Select(static item => item.Put.Item["record_type"].S)
            .Should().Equal("operation", "inbox", "outbox");
        captured.ClientRequestToken.Should().HaveLength(32);
        captured.TransactItems.Should().OnlyContain(static item => item.Put.Item["schema_version"].N == "1");
    }

    [Fact]
    public async Task Exact_duplicate_resolves_by_consistent_read_without_writing()
    {
        var dynamo = Substitute.For<IAmazonDynamoDB>();
        var intent = Intent();
        var hash = DatabaseBackupContractSerializer.Serialize(intent.ExecutionEvent).Hash;
        dynamo.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new() { ["content_hash"] = new AttributeValue { S = hash } } });
        var journal = Create(dynamo);

        var result = await journal.AdmitAsync(intent, CancellationToken.None);

        result.Outcome.Should().Be(JournalAdmissionOutcome.ExactDuplicate);
        await dynamo.DidNotReceive().TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>());
        await dynamo.Received(1).GetItemAsync(
            Arg.Is<GetItemRequest>(static request => request.ConsistentRead == true), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Duplicate_event_id_with_changed_content_fails_closed()
    {
        var dynamo = Substitute.For<IAmazonDynamoDB>();
        dynamo.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetItemResponse { Item = new() { ["content_hash"] = new AttributeValue { S = new string('0', 64) } } });
        var journal = Create(dynamo);

        var action = () => journal.AdmitAsync(Intent(), CancellationToken.None).AsTask();

        await action.Should().ThrowAsync<DatabaseExecutionConflictException>();
    }

    [Fact]
    public async Task Conditional_lease_conflict_returns_no_lease_and_does_not_throw()
    {
        var dynamo = Substitute.For<IAmazonDynamoDB>();
        dynamo.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<UpdateItemResponse>>(_ => throw new ConditionalCheckFailedException("owned"));
        var journal = Create(dynamo);

        var lease = await journal.TryAcquireLeaseAsync(
            new DatabaseRecoveryOperationId(Guid.NewGuid()), new DatabaseBackupHostId("second-host"),
            TimeSpan.FromMinutes(1), CancellationToken.None);

        lease.Should().BeNull();
    }

    [Fact]
    public async Task Stale_fencing_token_cannot_renew_a_lease()
    {
        var dynamo = Substitute.For<IAmazonDynamoDB>();
        dynamo.UpdateItemAsync(Arg.Any<UpdateItemRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<UpdateItemResponse>>(_ => throw new ConditionalCheckFailedException("stale"));
        var journal = Create(dynamo);
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var lease = new JournalLease(operationId, new DatabaseBackupHostId("old-host"), 3,
            DateTimeOffset.UtcNow.AddMinutes(1), TimeSpan.FromMinutes(1));

        var action = () => journal.RenewLeaseAsync(lease, CancellationToken.None).AsTask();

        await action.Should().ThrowAsync<DatabaseLeaseLostException>();
    }

    [Fact]
    public async Task Recoverable_queue_uses_the_gsi_then_resolves_authority_with_consistent_batch_get()
    {
        var dynamo = Substitute.For<IAmazonDynamoDB>();
        var intent = Intent();
        var serialized = DatabaseBackupContractSerializer.Serialize(intent.ExecutionEvent);
        var key = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = $"ENV#development#OP#{intent.OperationId.Value:N}" },
            ["SK"] = new() { S = "OP" }
        };
        dynamo.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResponse { Items = [key], LastEvaluatedKey = [] });
        dynamo.BatchGetItemAsync(Arg.Any<BatchGetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(new BatchGetItemResponse
            {
                Responses = new()
                {
                    ["ifm-database-backup-journal-development"] =
                    [
                        new Dictionary<string, AttributeValue>(key)
                        {
                            ["admitted_utc"] = new() { S = DateTimeOffset.UtcNow.UtcDateTime.ToString("O") },
                            ["operation_id"] = new() { S = intent.OperationId.Value.ToString("D") },
                            ["intent_type"] = new() { S = serialized.TypeName },
                            ["intent_payload"] = new() { S = serialized.Payload },
                            ["phase"] = new() { N = ((short)DatabaseRecoveryPhase.Admitted).ToString() },
                            ["last_service_sequence"] = new() { N = "1" },
                            ["fencing_token"] = new() { N = "0" }
                        }
                    ]
                },
                UnprocessedKeys = []
            });
        var journal = Create(dynamo);

        var recovered = new List<RecoverableJournalOperation>();
        await foreach (var item in journal.ReadRecoverableOperationsAsync(CancellationToken.None)) recovered.Add(item);

        recovered.Should().ContainSingle().Which.Intent.OperationId.Should().Be(intent.OperationId);
        await dynamo.Received(1).QueryAsync(Arg.Is<QueryRequest>(request =>
            request.IndexName == "WorkQueueIndex" && request.KeyConditionExpression == "GSI1PK=:partition"),
            Arg.Any<CancellationToken>());
        await dynamo.Received(1).BatchGetItemAsync(Arg.Is<BatchGetItemRequest>(request =>
            request.RequestItems.Values.Single().ConsistentRead == true), Arg.Any<CancellationToken>());
    }

    static DynamoDbDatabaseBackupExecutionJournal Create(IAmazonDynamoDB dynamo) => new(
        dynamo, Options(), new DatabaseBackupHostOptions { HostId = "gate5-host" }, TimeProvider.System);

    static AwsCloudDatabaseBackupOptions Options() => new()
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
        UploadRoleArn = "arn:aws:iam::107651266250:role/ifm-upload-development",
        RecoveryReadRoleArn = "arn:aws:iam::107651266250:role/ifm-recovery-development",
        PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/11111111-1111-1111-1111-111111111111",
        RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/22222222-2222-2222-2222-222222222222",
        SigningKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/33333333-3333-3333-3333-333333333333"
    };

    static DatabaseExecutionIntent Intent()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var eventId = Guid.NewGuid();
        return new DatabaseExecutionIntent
        {
            ExecutionEvent = new DatabaseBackupExecutionRequestedEvent
            {
                Id = eventId,
                EventId = 1,
                CommandId = Guid.NewGuid(),
                EntityId = operationId,
                AggregateId = operationId.Format(),
                EventSource = "DatabaseBackupCommandActor",
                ReceivedOn = DateTime.UtcNow,
                RequiredDestinations = [new DatabaseLogicalDestination("aws-primary", true)],
                Source = new DatabaseSourceEnvelope
                {
                    SourceEventId = eventId,
                    OperationId = operationId,
                    Source = BackupSource.AwsCloud,
                    ProtectionSetId = new DatabaseProtectionSetId("postgresql-core"),
                    PolicyRevision = 1,
                    OperationKind = DatabaseRecoveryOperationKind.Backup,
                    Phase = DatabaseRecoveryPhase.Requested,
                    CorrelationId = Guid.NewGuid(),
                    CausationId = Guid.NewGuid(),
                    ObservedUtc = DateTimeOffset.UtcNow
                }
            }
        };
    }
}
