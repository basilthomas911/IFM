using System.Globalization;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Journal;

/// <summary>
/// Environment-scoped DynamoDB implementation of the durable DatabaseBackup inbox,
/// operation, checkpoint, replica, outbox, statistics, and reconciliation journal.
/// Authoritative records are never assigned TTLs.
/// </summary>
public sealed class DynamoDbDatabaseBackupExecutionJournal(
    IAmazonDynamoDB dynamoDb,
    AwsCloud.Configuration.AwsCloudDatabaseBackupOptions options,
    DatabaseBackupHostOptions hostOptions,
    TimeProvider timeProvider) : IDatabaseBackupExecutionJournal
{
    const int SchemaVersion = 1;
    const int MaximumItemBytes = 300 * 1024;
    readonly string _tableName = RequireTableName(options);
    readonly string _environment = options.Environment.ToString().ToLowerInvariant();
    readonly DatabaseBackupHostId _hostId = new(ValidateHost(hostOptions).HostId);
    readonly ConcurrentDictionary<Guid, Dictionary<string, AttributeValue>> _outboxKeys = new();

    public async ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        var response = await dynamoDb.DescribeTableAsync(
            new DescribeTableRequest { TableName = _tableName }, cancellationToken).ConfigureAwait(false);
        var table = response.Table ?? throw new InvalidOperationException("The AWS journal table description was empty.");
        if (table.TableStatus != TableStatus.ACTIVE)
            throw new InvalidOperationException($"The AWS journal table is not active (status {table.TableStatus}).");
        var keys = table.KeySchema ?? [];
        if (!keys.Any(static key => key.AttributeName == "PK" && key.KeyType == KeyType.HASH)
            || !keys.Any(static key => key.AttributeName == "SK" && key.KeyType == KeyType.RANGE))
            throw new InvalidOperationException("The AWS journal table does not have the required PK/SK key schema.");

        var backups = await dynamoDb.DescribeContinuousBackupsAsync(
            new DescribeContinuousBackupsRequest { TableName = _tableName }, cancellationToken).ConfigureAwait(false);
        if (backups.ContinuousBackupsDescription?.PointInTimeRecoveryDescription?.PointInTimeRecoveryStatus
            != PointInTimeRecoveryStatus.ENABLED)
            throw new InvalidOperationException("The AWS journal table does not have point-in-time recovery enabled.");
    }

    public ValueTask VerifyIntegrityAsync(CancellationToken cancellationToken) => InitializeAsync(cancellationToken);

    public async ValueTask<JournalAdmissionResult> AdmitAsync(
        DatabaseExecutionIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        intent.Validate();
        if (intent.Source != BackupSource.AwsCloud)
            throw new UnsupportedDatabaseBackupSourceException(intent.Source);

        var serialized = DatabaseBackupContractSerializer.Serialize(intent.ExecutionEvent);
        EnsureBounded(serialized.Payload, "execution intent");
        var operationKey = OperationKey(intent.OperationId);
        var inboxKey = Key(operationKey, $"INBOX#{intent.ExecutionEvent.Id:D}");
        var existingInbox = await GetAsync(inboxKey, cancellationToken).ConfigureAwait(false);
        if (existingInbox.Count > 0)
            return ResolveInbox(existingInbox, serialized.Hash, intent.OperationId);

        var existingOperation = await GetAsync(Key(operationKey, "OP"), cancellationToken).ConfigureAwait(false);
        var definitionHash = DatabaseBackupContractSerializer.DefinitionHash(intent.ExecutionEvent);
        if (existingOperation.Count > 0)
        {
            EnsureDefinition(existingOperation, definitionHash);
            await PutInboxAsync(inboxKey, intent, serialized.Hash, cancellationToken).ConfigureAwait(false);
            return new JournalAdmissionResult(intent.OperationId, JournalAdmissionOutcome.Admitted);
        }

        var accepted = DatabaseBackupServiceEventFactory.Accepted(intent, _hostId);
        var acceptedSerialized = DatabaseBackupContractSerializer.Serialize(accepted);
        var now = timeProvider.GetUtcNow();
        var operation = Key(operationKey, "OP");
        Add(operation,
            ("schema_version", Number(SchemaVersion)), ("record_type", Text("operation")),
            ("operation_id", Text(intent.OperationId.Value.ToString("D"))),
            ("source", Number((short)intent.Source)),
            ("operation_kind", Number((short)intent.ExecutionEvent.Source.OperationKind)),
            ("protection_set_id", Text(intent.ExecutionEvent.Source.ProtectionSetId.Value)),
            ("definition_hash", Text(definitionHash)),
            ("intent_event_id", Text(intent.ExecutionEvent.Id.ToString("D"))),
            ("intent_type", Text(serialized.TypeName)), ("intent_payload", Text(serialized.Payload)),
            ("phase", Number((short)DatabaseRecoveryPhase.Admitted)), ("terminal", Flag(false)),
            ("fencing_token", Number(0)), ("last_service_sequence", Number(1)),
            ("state_version", Number(1)), ("admitted_utc", Text(Format(now))),
            ("updated_utc", Text(Format(now))),
            ("GSI1PK", Text(RecoverablePartition)),
            ("GSI1SK", Text($"{now.UtcTicks:D20}#{intent.OperationId.Value:N}")));
        var inbox = CreateInbox(inboxKey, intent, serialized.Hash, now);
        var outbox = CreateOutbox(operationKey, accepted, acceptedSerialized, now);

        try
        {
            await dynamoDb.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    Put(operation, "attribute_not_exists(PK) AND attribute_not_exists(SK)"),
                    Put(inbox, "attribute_not_exists(PK) AND attribute_not_exists(SK)"),
                    Put(outbox, "attribute_not_exists(PK) AND attribute_not_exists(SK)")
                ],
                ClientRequestToken = intent.ExecutionEvent.Id.ToString("N")
            }, cancellationToken).ConfigureAwait(false);
            return new JournalAdmissionResult(intent.OperationId, JournalAdmissionOutcome.Admitted);
        }
        catch (TransactionCanceledException)
        {
            return await ResolveAdmissionAfterFailureAsync(intent, serialized.Hash, definitionHash, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AmazonDynamoDBException exception) when (IsAmbiguous(exception))
        {
            return await ResolveAdmissionAfterFailureAsync(intent, serialized.Hash, definitionHash, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask<JournalLease?> TryAcquireLeaseAsync(
        DatabaseRecoveryOperationId operationId,
        DatabaseBackupHostId hostId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (leaseDuration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        var now = timeProvider.GetUtcNow();
        var expires = now.Add(leaseDuration);
        try
        {
            var response = await dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = Key(OperationKey(operationId), "OP"),
                ConditionExpression = "attribute_exists(PK) AND terminal = :false AND (attribute_not_exists(lease_expires_ms) OR lease_expires_ms < :now OR lease_host_id = :host)",
                UpdateExpression = "SET lease_host_id=:host, lease_expires_ms=:expires, fencing_token=if_not_exists(fencing_token,:zero)+:one, state_version=if_not_exists(state_version,:zero)+:one, updated_utc=:updated",
                ExpressionAttributeValues = new()
                {
                    [":false"] = Flag(false), [":now"] = Number(now.ToUnixTimeMilliseconds()),
                    [":host"] = Text(hostId.Value), [":expires"] = Number(expires.ToUnixTimeMilliseconds()),
                    [":zero"] = Number(0), [":one"] = Number(1), [":updated"] = Text(Format(now))
                },
                ReturnValues = ReturnValue.ALL_NEW
            }, cancellationToken).ConfigureAwait(false);
            return new JournalLease(operationId, hostId, ReadLong(response.Attributes, "fencing_token"), expires, leaseDuration);
        }
        catch (ConditionalCheckFailedException)
        {
            return null;
        }
    }

    public async ValueTask RenewLeaseAsync(JournalLease lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var now = timeProvider.GetUtcNow();
        try
        {
            await dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName,
                Key = Key(OperationKey(lease.OperationId), "OP"),
                ConditionExpression = "terminal=:false AND lease_host_id=:host AND fencing_token=:token",
                UpdateExpression = "SET lease_expires_ms=:expires, state_version=state_version+:one, updated_utc=:updated",
                ExpressionAttributeValues = new()
                {
                    [":false"] = Flag(false), [":host"] = Text(lease.HostId.Value), [":token"] = Number(lease.FencingToken),
                    [":expires"] = Number(now.Add(lease.LeaseDuration).ToUnixTimeMilliseconds()),
                    [":one"] = Number(1), [":updated"] = Text(Format(now))
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ConditionalCheckFailedException)
        {
            throw new DatabaseLeaseLostException(lease.OperationId);
        }
    }

    public async ValueTask RecordCheckpointAsync(JournalCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.SafeDiagnosticReference.Length > 2048)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "A journal diagnostic reference is too large.");
        var pk = OperationKey(checkpoint.OperationId);
        var values = new Dictionary<string, AttributeValue>
        {
            [":false"] = Flag(false), [":host"] = Text(checkpoint.HostId.Value),
            [":token"] = Number(checkpoint.FencingToken), [":phase"] = Number((short)checkpoint.Phase),
            [":terminal"] = Flag(checkpoint.Terminal), [":one"] = Number(1),
            [":updated"] = Text(Format(checkpoint.ObservedUtc)), [":recoverable"] = Text(RecoverablePartition),
            [":recoverable_sort"] = Text($"{checkpoint.ObservedUtc.UtcTicks:D20}#{checkpoint.OperationId.Value:N}")
        };
        var checkpointItem = Key(pk, $"CHECKPOINT#{checkpoint.FencingToken:D20}#{(short)checkpoint.Phase:D3}");
        Add(checkpointItem,
            ("schema_version", Number(SchemaVersion)), ("record_type", Text("checkpoint")),
            ("operation_id", Text(checkpoint.OperationId.Value.ToString("D"))),
            ("fencing_token", Number(checkpoint.FencingToken)), ("phase", Number((short)checkpoint.Phase)),
            ("terminal", Flag(checkpoint.Terminal)),
            ("safe_diagnostic_reference", Text(checkpoint.SafeDiagnosticReference)),
            ("observed_utc", Text(Format(checkpoint.ObservedUtc))));

        var update = checkpoint.Terminal
            ? "SET phase=:phase, terminal=:terminal, state_version=state_version+:one, updated_utc=:updated REMOVE lease_host_id, lease_expires_ms, GSI1PK, GSI1SK"
            : "SET phase=:phase, terminal=:terminal, state_version=state_version+:one, updated_utc=:updated, GSI1PK=:recoverable, GSI1SK=:recoverable_sort";
        if (checkpoint.Terminal)
        {
            values.Remove(":recoverable");
            values.Remove(":recoverable_sort");
        }
        try
        {
            await dynamoDb.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem { Update = new Update { TableName = _tableName, Key = Key(pk, "OP"),
                        ConditionExpression = "terminal=:false AND lease_host_id=:host AND fencing_token=:token",
                        UpdateExpression = update, ExpressionAttributeValues = values } },
                    Put(checkpointItem, "attribute_not_exists(PK) AND attribute_not_exists(SK)")
                ]
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (TransactionCanceledException)
        {
            var stored = await GetAsync(Key(pk, "OP"), cancellationToken).ConfigureAwait(false);
            if (stored.Count == 0 || !MatchesLease(stored, checkpoint))
                throw new DatabaseLeaseLostException(checkpoint.OperationId);
            var existing = await GetAsync(Key(pk, checkpointItem["SK"].S), cancellationToken).ConfigureAwait(false);
            if (existing.Count == 0) throw;
        }
    }

    public async ValueTask EnqueueServiceEventAsync(DatabaseServiceEventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        envelope.Event.Validate();
        var serialized = DatabaseBackupContractSerializer.Serialize(envelope.Event);
        EnsureBounded(serialized.Payload, "service event");
        var pk = OperationKey(envelope.Event.Source.OperationId);
        var key = Key(pk, OutboxSort(envelope.Event.Source.SourceRevisionOrSequence));
        var existing = await GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            EnsureHash(existing, serialized.Hash, "A service-event outbox identity was replayed with conflicting content.");
            return;
        }
        var operation = await GetAsync(Key(pk, "OP"), cancellationToken).ConfigureAwait(false);
        if (operation.Count == 0) throw new InvalidOperationException("The DatabaseBackup operation is not present in the AWS journal.");
        var item = CreateOutbox(pk, envelope.Event, serialized, timeProvider.GetUtcNow());
        var sequence = envelope.Event.Source.SourceRevisionOrSequence;
        var writes = new List<TransactWriteItem> { Put(item, "attribute_not_exists(PK) AND attribute_not_exists(SK)") };
        if (sequence > ReadLong(operation, "last_service_sequence"))
            writes.Add(new TransactWriteItem { Update = new Update
            {
                TableName = _tableName, Key = Key(pk, "OP"),
                ConditionExpression = "last_service_sequence < :sequence",
                UpdateExpression = "SET last_service_sequence=:sequence, state_version=state_version+:one, updated_utc=:updated",
                ExpressionAttributeValues = new() { [":sequence"] = Number(sequence), [":one"] = Number(1), [":updated"] = Text(Format(timeProvider.GetUtcNow())) }
            }});
        try
        {
            await dynamoDb.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = writes }, cancellationToken).ConfigureAwait(false);
        }
        catch (TransactionCanceledException)
        {
            existing = await GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (existing.Count == 0) throw;
            EnsureHash(existing, serialized.Hash, "A service-event outbox identity was replayed with conflicting content.");
        }
    }

    public async IAsyncEnumerable<PendingServiceEvent> ReadPendingServiceEventsAsync(
        int maximumCount,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (maximumCount <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCount));
        var items = await ReadWorkQueueAsync(PendingOutboxPartition, maximumCount, cancellationToken).ConfigureAwait(false);
        foreach (var item in items.OrderBy(static value => value["operation_id"].S, StringComparer.Ordinal)
                     .ThenBy(static value => ReadLong(value, "service_sequence")))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var @event = DatabaseBackupContractSerializer.Deserialize(item["event_type"].S, item["event_payload"].S);
            _outboxKeys[Guid.Parse(item["event_id"].S)] = Key(item["PK"].S, item["SK"].S);
            yield return new PendingServiceEvent(
                Guid.Parse(item["event_id"].S),
                new DatabaseRecoveryOperationId(Guid.Parse(item["operation_id"].S)),
                ReadLong(item, "service_sequence"),
                (DatabaseBackupServiceEventContract)@event,
                checked((int)ReadLong(item, "publish_attempts")));
        }
    }

    public async ValueTask MarkServiceEventPublishedAsync(Guid eventId, DateTimeOffset publishedUtc, CancellationToken cancellationToken)
    {
        if (!_outboxKeys.TryGetValue(eventId, out var key))
        {
            var pending = await ReadWorkQueueAsync(PendingOutboxPartition, int.MaxValue, cancellationToken).ConfigureAwait(false);
            foreach (var candidate in pending)
                if (candidate.TryGetValue("event_id", out var value))
                    _outboxKeys[Guid.Parse(value.S)] = Key(candidate["PK"].S, candidate["SK"].S);
            if (!_outboxKeys.TryGetValue(eventId, out key))
                throw new InvalidOperationException("The DatabaseBackup service event is not present in the AWS outbox.");
        }
        var item = await GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (item.Count == 0) throw new InvalidOperationException("The DatabaseBackup service event is not present in the AWS outbox.");
        if (ReadBool(item, "published")) return;
        try
        {
            await dynamoDb.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _tableName, Key = key,
                ConditionExpression = "published=:false",
                UpdateExpression = "SET published=:true, published_utc=:published, publish_attempts=publish_attempts+:one REMOVE GSI1PK, GSI1SK",
                ExpressionAttributeValues = new()
                {
                    [":false"] = Flag(false), [":true"] = Flag(true), [":published"] = Text(Format(publishedUtc)), [":one"] = Number(1)
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (ConditionalCheckFailedException)
        {
            var resolved = await GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (resolved.Count == 0 || !ReadBool(resolved, "published")) throw;
        }
        _outboxKeys.TryRemove(eventId, out _);
    }

    public async IAsyncEnumerable<RecoverableJournalOperation> ReadRecoverableOperationsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var items = await ReadWorkQueueAsync(RecoverablePartition, int.MaxValue, cancellationToken).ConfigureAwait(false);
        foreach (var item in items.OrderBy(static value => value["admitted_utc"].S, StringComparer.Ordinal)
                     .ThenBy(static value => value["operation_id"].S, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var @event = DatabaseBackupContractSerializer.Deserialize(item["intent_type"].S, item["intent_payload"].S);
            yield return new RecoverableJournalOperation(
                new DatabaseExecutionIntent { ExecutionEvent = @event },
                (DatabaseRecoveryPhase)ReadLong(item, "phase"),
                ReadLong(item, "last_service_sequence"), ReadLong(item, "fencing_token"));
        }
    }

    public async ValueTask MarkCoreAcknowledgedAsync(
        DatabaseRecoveryOperationId operationId,
        long domainRevision,
        CancellationToken cancellationToken)
    {
        if (domainRevision <= 0) throw new ArgumentOutOfRangeException(nameof(domainRevision));
        var key = Key(OperationKey(operationId), "RECON#CORE");
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var existing = await GetAsync(key, cancellationToken).ConfigureAwait(false);
            var previous = existing.Count == 0 ? 0 : ReadLong(existing, "core_domain_revision");
            if (previous >= domainRevision) return;
            var now = timeProvider.GetUtcNow();
            var item = new Dictionary<string, AttributeValue>(key);
            Add(item, ("schema_version", Number(SchemaVersion)), ("record_type", Text("reconciliation")),
                ("operation_id", Text(operationId.Value.ToString("D"))), ("core_domain_revision", Number(domainRevision)),
                ("state_version", Number(previous == 0 ? 1 : ReadLong(existing, "state_version") + 1)),
                ("acknowledged_utc", Text(Format(now))));
            try
            {
                await dynamoDb.PutItemAsync(new PutItemRequest
                {
                    TableName = _tableName, Item = item,
                    ConditionExpression = previous == 0 ? "attribute_not_exists(PK)" : "state_version=:version",
                    ExpressionAttributeValues = previous == 0 ? null : new() { [":version"] = Number(ReadLong(existing, "state_version")) }
                }, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (ConditionalCheckFailedException) when (attempt < 3) { }
        }
        throw new DatabaseExecutionConflictException("The AWS journal reconciliation record changed repeatedly.");
    }

    async ValueTask<JournalAdmissionResult> ResolveAdmissionAfterFailureAsync(
        DatabaseExecutionIntent intent, string contentHash, string definitionHash, CancellationToken cancellationToken)
    {
        var pk = OperationKey(intent.OperationId);
        var inbox = await GetAsync(Key(pk, $"INBOX#{intent.ExecutionEvent.Id:D}"), cancellationToken).ConfigureAwait(false);
        if (inbox.Count > 0) return ResolveInbox(inbox, contentHash, intent.OperationId);
        var operation = await GetAsync(Key(pk, "OP"), cancellationToken).ConfigureAwait(false);
        if (operation.Count > 0) EnsureDefinition(operation, definitionHash);
        throw new DatabaseExecutionConflictException("The AWS journal admission transaction did not commit and could not be resolved safely.");
    }

    async ValueTask PutInboxAsync(
        Dictionary<string, AttributeValue> key, DatabaseExecutionIntent intent, string hash, CancellationToken cancellationToken)
    {
        var item = CreateInbox(key, intent, hash, timeProvider.GetUtcNow());
        try
        {
            await dynamoDb.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item,
                ConditionExpression = "attribute_not_exists(PK) AND attribute_not_exists(SK)" }, cancellationToken).ConfigureAwait(false);
        }
        catch (ConditionalCheckFailedException)
        {
            var existing = await GetAsync(key, cancellationToken).ConfigureAwait(false);
            _ = ResolveInbox(existing, hash, intent.OperationId);
        }
    }

    async Task<Dictionary<string, AttributeValue>> GetAsync(Dictionary<string, AttributeValue> key, CancellationToken cancellationToken)
        => (await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName, Key = key, ConsistentRead = true
        }, cancellationToken).ConfigureAwait(false)).Item ?? [];

    async Task<List<Dictionary<string, AttributeValue>>> ReadWorkQueueAsync(
        string partition, int maximumCount, CancellationToken cancellationToken)
    {
        var keys = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var response = await dynamoDb.QueryAsync(new QueryRequest
            {
                TableName = _tableName, IndexName = "WorkQueueIndex",
                KeyConditionExpression = "GSI1PK=:partition",
                ExpressionAttributeValues = new() { [":partition"] = Text(partition) },
                ExclusiveStartKey = start, ScanIndexForward = true,
                Limit = maximumCount == int.MaxValue ? 100 : Math.Min(100, maximumCount - keys.Count)
            }, cancellationToken).ConfigureAwait(false);
            if (response.Items is not null)
                keys.AddRange(response.Items.Take(maximumCount - keys.Count).Select(item => Key(item["PK"].S, item["SK"].S)));
            start = response.LastEvaluatedKey;
        } while (keys.Count < maximumCount && start is { Count: > 0 });

        var result = new List<Dictionary<string, AttributeValue>>(keys.Count);
        foreach (var page in keys.Chunk(100))
        {
            var pending = page.ToList();
            for (var attempt = 0; pending.Count > 0 && attempt < 5; attempt++)
            {
                var response = await dynamoDb.BatchGetItemAsync(new BatchGetItemRequest
                {
                    RequestItems = new()
                    {
                        [_tableName] = new KeysAndAttributes { Keys = pending, ConsistentRead = true }
                    }
                }, cancellationToken).ConfigureAwait(false);
                if (response.Responses?.TryGetValue(_tableName, out var items) == true) result.AddRange(items);
                pending = response.UnprocessedKeys?.TryGetValue(_tableName, out var unprocessed) == true
                    ? unprocessed.Keys ?? [] : [];
                if (pending.Count > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * (1 << attempt)), cancellationToken).ConfigureAwait(false);
            }
            if (pending.Count > 0) throw new InvalidOperationException("DynamoDB did not resolve all work-queue keys consistently.");
        }
        return result;
    }

    static JournalAdmissionResult ResolveInbox(
        Dictionary<string, AttributeValue> item, string expectedHash, DatabaseRecoveryOperationId operationId)
    {
        EnsureHash(item, expectedHash, "An execution event ID was replayed with conflicting content.");
        return new JournalAdmissionResult(operationId, JournalAdmissionOutcome.ExactDuplicate);
    }

    static Dictionary<string, AttributeValue> CreateInbox(
        Dictionary<string, AttributeValue> key, DatabaseExecutionIntent intent, string hash, DateTimeOffset now)
    {
        var item = new Dictionary<string, AttributeValue>(key);
        Add(item, ("schema_version", Number(SchemaVersion)), ("record_type", Text("inbox")),
            ("operation_id", Text(intent.OperationId.Value.ToString("D"))), ("content_hash", Text(hash)),
            ("admitted_utc", Text(Format(now))));
        return item;
    }

    Dictionary<string, AttributeValue> CreateOutbox(
        string pk, DatabaseBackupServiceEventContract @event,
        (string TypeName, string Payload, string Hash) serialized, DateTimeOffset now)
    {
        var sequence = @event.Source.SourceRevisionOrSequence;
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(@event), "A positive service sequence is required.");
        var item = Key(pk, OutboxSort(sequence));
        Add(item, ("schema_version", Number(SchemaVersion)), ("record_type", Text("outbox")),
            ("operation_id", Text(@event.Source.OperationId.Value.ToString("D"))),
            ("event_id", Text(@event.Id.ToString("D"))), ("service_sequence", Number(sequence)),
            ("event_type", Text(serialized.TypeName)), ("event_payload", Text(serialized.Payload)),
            ("content_hash", Text(serialized.Hash)), ("published", Flag(false)),
            ("publish_attempts", Number(0)), ("created_utc", Text(Format(now))),
            ("GSI1PK", Text(PendingOutboxPartition)),
            ("GSI1SK", Text($"{now.UtcTicks:D20}#{@event.Source.OperationId.Value:N}#{sequence:D20}")));
        return item;
    }

    TransactWriteItem Put(Dictionary<string, AttributeValue> item, string condition)
        => new() { Put = new Put { TableName = _tableName, Item = item, ConditionExpression = condition } };

    string OperationKey(DatabaseRecoveryOperationId operationId)
        => $"ENV#{_environment}#OP#{operationId.Value:N}";

    string RecoverablePartition => $"ENV#{_environment}#RECOVERABLE";
    string PendingOutboxPartition => $"ENV#{_environment}#OUTBOX#PENDING";
    static string OutboxSort(long sequence) => $"OUTBOX#{sequence:D20}";
    static Dictionary<string, AttributeValue> Key(string pk, string sk) => new() { ["PK"] = Text(pk), ["SK"] = Text(sk) };
    static AttributeValue Text(string value) => new() { S = value };
    static AttributeValue Number(long value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };
    static AttributeValue Flag(bool value) => new() { BOOL = value };
    static string Format(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    static long ReadLong(IReadOnlyDictionary<string, AttributeValue> item, string name)
        => long.Parse(item[name].N, CultureInfo.InvariantCulture);
    static bool ReadBool(IReadOnlyDictionary<string, AttributeValue> item, string name) => item[name].BOOL == true;

    static void Add(Dictionary<string, AttributeValue> item, params (string Name, AttributeValue Value)[] values)
    {
        foreach (var value in values) item[value.Name] = value.Value;
    }

    static void EnsureDefinition(IReadOnlyDictionary<string, AttributeValue> item, string expected)
    {
        if (!StringComparer.Ordinal.Equals(item["definition_hash"].S, expected))
            throw new DatabaseExecutionConflictException("An operation ID was replayed with a conflicting immutable definition.");
    }

    static void EnsureHash(IReadOnlyDictionary<string, AttributeValue> item, string expected, string message)
    {
        if (!item.TryGetValue("content_hash", out var hash) || !StringComparer.Ordinal.Equals(hash.S, expected))
            throw new DatabaseExecutionConflictException(message);
    }

    static bool MatchesLease(IReadOnlyDictionary<string, AttributeValue> item, JournalCheckpoint checkpoint)
        => !ReadBool(item, "terminal")
            && item.TryGetValue("lease_host_id", out var host) && host.S == checkpoint.HostId.Value
            && ReadLong(item, "fencing_token") == checkpoint.FencingToken;

    static void EnsureBounded(string payload, string description)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > MaximumItemBytes)
            throw new InvalidOperationException($"The AWS journal {description} exceeds the bounded item limit.");
    }

    static bool IsAmbiguous(AmazonDynamoDBException exception)
        => (int)exception.StatusCode >= 500 || exception.InnerException is TimeoutException;

    static string RequireTableName(AwsCloud.Configuration.AwsCloudDatabaseBackupOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        return value.JournalTableName;
    }

    static DatabaseBackupHostOptions ValidateHost(DatabaseBackupHostOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        return value;
    }
}
