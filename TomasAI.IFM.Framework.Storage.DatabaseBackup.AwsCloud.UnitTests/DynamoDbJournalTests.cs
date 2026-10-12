using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using NSubstitute;
using System.Net;
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
    public async Task Cutover_reactivates_only_validated_restore_with_compare_and_swap_and_no_new_sequence_one()
    {
        var dynamo = Substitute.For<IAmazonDynamoDB>();
        var original = Intent().ExecutionEvent;
        var restore = new DatabaseRestoreExecutionRequestedEvent
        {
            Id = original.Id, EntityId = original.EntityId, CommandId = original.CommandId,
            Source = original.Source with { OperationKind = DatabaseRecoveryOperationKind.Restore },
            RestorePointId = new("verified-backup"), FreshTarget = new("fresh-validation", "disposable")
        };
        var stored = DatabaseBackupContractSerializer.Serialize(restore);
        var ready = DatabaseBackupContractSerializer.Serialize(DatabaseBackupServiceEventFactory.ReadyForCutover(
            new() { ExecutionEvent = restore }, new("test-host"), 5, "validated-target", 12));
        var operation = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new() { S = "ENV#development#OP#" + original.EntityId.Format() }, ["SK"] = new() { S = "OP" },
            ["intent_type"] = new() { S = stored.TypeName }, ["intent_payload"] = new() { S = stored.Payload },
            ["phase"] = new() { N = "11" }, ["terminal"] = new() { BOOL = true },
            ["last_service_sequence"] = new() { N = "5" }, ["fencing_token"] = new() { N = "2" },
            ["state_version"] = new() { N = "8" }
        };
        dynamo.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var key = call.Arg<GetItemRequest>().Key["SK"].S;
            return new GetItemResponse { Item = key == "OP" ? operation : key.StartsWith("OUTBOX#")
                ? new() { ["event_type"] = new() { S = ready.TypeName }, ["event_payload"] = new() { S = ready.Payload } } : [] };
        });
        TransactWriteItemsRequest? captured = null;
        dynamo.TransactWriteItemsAsync(Arg.Do<TransactWriteItemsRequest>(request => captured = request), Arg.Any<CancellationToken>())
            .Returns(new TransactWriteItemsResponse());
        var cutover = new DatabaseCutoverExecutionRequestedEvent
        {
            Id = Guid.NewGuid(), EntityId = restore.EntityId, CommandId = Guid.NewGuid(),
            Source = restore.Source with { OperationKind = DatabaseRecoveryOperationKind.Cutover },
            RestorePointId = restore.RestorePointId, FreshTarget = restore.FreshTarget, ValidationRevision = 12
        };
        var intent = new DatabaseExecutionIntent { ExecutionEvent = cutover };
        var journal = Create(dynamo);
        await Assert.ThrowsAsync<DatabaseExecutionConflictException>(() => journal.AdmitAsync(intent with
            { ExecutionEvent = cutover with { ValidationRevision = 11 } }, default).AsTask());
        (await journal.AdmitAsync(intent, default)).Outcome.Should().Be(JournalAdmissionOutcome.Admitted);
        captured!.TransactItems.Should().HaveCount(2);
        var replacement = captured.TransactItems[0].Put;
        replacement.Item["last_service_sequence"].N.Should().Be("5");
        replacement.Item["fencing_token"].N.Should().Be("3");
        replacement.Item["terminal"].BOOL.Should().BeFalse();
        replacement.Item["phase"].N.Should().Be("12");
        replacement.ExpressionAttributeValues[":version"].N.Should().Be("8");
        replacement.ConditionExpression.Should().Contain("state_version=:version");
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
    public async Task Ambiguous_admission_response_is_resolved_by_a_consistent_inbox_read()
    {
        var dynamo = Substitute.For<IAmazonDynamoDB>();
        var intent = Intent();
        var hash = DatabaseBackupContractSerializer.Serialize(intent.ExecutionEvent).Hash;
        var inboxReads = 0;
        dynamo.GetItemAsync(Arg.Any<GetItemRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<GetItemRequest>();
                if (!request.Key["SK"].S.StartsWith("INBOX#", StringComparison.Ordinal)
                    || Interlocked.Increment(ref inboxReads) == 1)
                    return Task.FromResult(new GetItemResponse { Item = [] });

                return Task.FromResult(new GetItemResponse
                {
                    Item = new() { ["content_hash"] = new AttributeValue { S = hash } }
                });
            });
        dynamo.TransactWriteItemsAsync(Arg.Any<TransactWriteItemsRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<TransactWriteItemsResponse>>(_ => throw new AmazonDynamoDBException("response lost")
            {
                StatusCode = HttpStatusCode.InternalServerError
            });
        var journal = Create(dynamo);

        var result = await journal.AdmitAsync(intent, CancellationToken.None);

        result.Outcome.Should().Be(JournalAdmissionOutcome.ExactDuplicate);
        await dynamo.Received(3).GetItemAsync(
            Arg.Is<GetItemRequest>(static request => request.ConsistentRead == true),
            Arg.Any<CancellationToken>());
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
