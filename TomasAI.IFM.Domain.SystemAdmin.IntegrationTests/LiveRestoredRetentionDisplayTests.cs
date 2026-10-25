using TomasAI.IFM.Application.DatabaseBackup.Adapters.AwsCloud.Journal;
using TomasAI.IFM.Application.DatabaseBackup.Adapters.AwsCloud.Processing;
using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Translation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Journal;

namespace TomasAI.IFM.Domain.SystemAdmin.IntegrationTests;

public sealed class LiveRestoredRetentionDisplayTests
{
    [Fact]
    public async Task Missing_completed_retention_display_recovers_from_latest_independent_observation_without_execution()
    {
        if (Environment.GetEnvironmentVariable("IFM_AWS_RETENTION_DISPLAY_TEST") != "1") return;
        var operation = new DatabaseRecoveryOperationId(Guid.Parse(Environment.GetEnvironmentVariable("IFM_AWS_RETENTION_DISPLAY_OPERATION")
            ?? throw new InvalidOperationException("Exact completed operation required.")));
        var evidenceRoot = Environment.GetEnvironmentVariable("IFM_AWS_RETENTION_DISPLAY_EVIDENCE")
            ?? throw new InvalidOperationException("Persistent evidence directory required.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var dynamo = new AmazonDynamoDBClient(RegionEndpoint.CACentral1);
        var options = new AwsCloudDatabaseBackupOptions { Environment = AwsBackupEnvironment.Development,
            JournalTableName = "ifm-database-backup-journal-development" };
        var journal = new DynamoDbDatabaseBackupExecutionJournal(dynamo, options,
            new DatabaseBackupHostOptions { HostId = "qualification-reader" }, TimeProvider.System);
        var latest = await journal.ReadLatestServiceEventAsync(operation, timeout.Token)
            ?? throw new InvalidOperationException("Independent terminal observation missing.");
        Assert.Equal(DatabaseRecoveryOperationKind.Retention, latest.Source.OperationKind);
        Assert.Equal(DatabaseRecoveryPhase.Completed, latest.Source.Phase);
        var command = DatabaseBackupEventTranslator.ReconcileTerminalRetention(latest);
        var key = new Dictionary<string, AttributeValue> { ["PK"] = new("ENV#development#OP#" + operation.Format()), ["SK"] = new("OP") };
        async Task<GetItemResponse> Read() => await dynamo.GetItemAsync(new GetItemRequest
            { TableName = options.JournalTableName, Key = key, ConsistentRead = true }, timeout.Token);
        var before = await Read();
        await using var connection = new NatsConnectionManager();
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = "nats://localhost:4222" }, NullLogger.Instance, connection);
        await producer.StartAsync(new TomasAI.IFM.Shared.EventModelActor.ActorMailboxId(
            TomasAI.IFM.Shared.EventModelActor.ActorType.Command, DatabaseBackupInternalCommand.Actor), timeout.Token);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var result = await producer.RequestAsync<ReconcileDatabaseBackupServiceStateCommand, DatabaseRecoveryOperationId,
                TomasAI.IFM.Shared.EventSourcing.GuidResult>(command.Subject, command, command.EntityId, timeout.Token);
            Assert.True(result.Success, result.ErrorMessage);
        }
        var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(evidenceRoot, "backup-console", "TomasAI.IFM.Application.DatabaseBackup.Console.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "show-operation", "--operation-id", operation.Format(), "--source", "aws",
            "--environment", "Development", "--caller", "basil", "--authorization", "DevelopmentOwnerQualification" })
            start.ArgumentList.Add(argument);
        using var query = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Query client could not start.");
        var output = query.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = query.StandardError.ReadToEndAsync(timeout.Token);
        await query.WaitForExitAsync(timeout.Token);
        var projectionJson = await output;
        _ = await error;
        Assert.Equal(0, query.ExitCode);
        using var projection = JsonDocument.Parse(projectionJson);
        Assert.True(projection.RootElement.TryGetProperty("phase", out var phase), projectionJson);
        Assert.Equal(14, phase.GetInt32());
        Assert.Equal(operation.Value, projection.RootElement.GetProperty("operationId").GetProperty("value").GetGuid());
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "restored-retention-projection-" + operation.Format() + ".json"), projectionJson, timeout.Token);
        var after = await Read();
        Assert.Equal(before.Item["state_version"].N, after.Item["state_version"].N);
        Assert.Equal(before.Item["last_service_sequence"].N, after.Item["last_service_sequence"].N);
        Assert.Equal("14", after.Item["phase"].N);
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "restored-retention-display-" + operation.Format() + ".json"),
            JsonSerializer.Serialize(new { operationId = operation.Format(), observedUtc = DateTimeOffset.UtcNow,
                recoveredFromSingleLatestObservation = true, duplicateRequestAccepted = true, executionJournalUnchanged = true,
                terminalPhase = 14, serviceSequence = after.Item["last_service_sequence"].N }, new JsonSerializerOptions { WriteIndented = true }), timeout.Token);
    }
}