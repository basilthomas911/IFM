using System.Text.Json;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Api.DatabaseBackup.Host.Services;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Processing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed partial class LiveAwsPublicationAndSigningIntegrationTests
{
    [Fact]
    [Trait("Category", "LiveAwsAutomaticRetentionHost")]
    public async Task Automatic_retention_command_is_persisted_and_admitted_by_running_host()
    {
        if (Environment.GetEnvironmentVariable("IFM_AWS_AUTOMATIC_RETENTION_HOST_TEST") != "1") return;
        var root = Environment.GetEnvironmentVariable("IFM_GATE14_OWNER_EVIDENCE")
            ?? throw new InvalidOperationException("Persistent evidence directory required.");
        var options = LiveOptions();
        options.AcceptBackupRequests = true;
        options.AutomaticRetentionEnabled = true;
        options.AutomaticRetentionApprovalReference = "DevelopmentOwnerAutomaticRetention-20261010";
        var postgresQualification = Environment.GetEnvironmentVariable("IFM_AWS_AUTOMATIC_RETENTION_POSTGRES_TEST") == "1";
        options.PostgreSqlProtectionSets = postgresQualification ? ["core-postgresql"] : [];
        options.ScyllaProtectionSets = postgresQualification ? [] : ["read-model-scylla"];
        // Use a fresh qualification slot rather than replay an interrupted daily execution.
        // Production host scheduling stays at its configured daily interval.
        if (postgresQualification) options.AutomaticRetentionInterval = TimeSpan.FromHours(1);
        var intents = AwsAutomaticRetentionSchedule.Create(options, DateTimeOffset.UtcNow);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using var connection = new NatsConnectionManager();
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = "nats://localhost:4222" }, NullLogger.Instance, connection);
        var sender = new AwsAutomaticRetentionCommandSender(producer);
        using var dynamo = new AmazonDynamoDBClient(RegionEndpoint.CACentral1);
        var observations = new List<object>();
        foreach (var intent in intents)
        {
            await sender.SendAsync(intent, timeout.Token);
            // Repeat exactly the same command to verify restart-safe admission through the real actor.
            await sender.SendAsync(intent, timeout.Token);
            GetItemResponse response;
            do
            {
                response = await dynamo.GetItemAsync(new GetItemRequest
                {
                    TableName = options.JournalTableName, ConsistentRead = true,
                    Key = new() { ["PK"] = new AttributeValue("ENV#development#OP#" + intent.OperationId.Format()),
                        ["SK"] = new AttributeValue("OP") }
                }, timeout.Token);
                if (response.Item?.Count > 0) break;
                await Task.Delay(TimeSpan.FromSeconds(3), timeout.Token);
            } while (true);
            Assert.Equal("7", response.Item["operation_kind"].N);
            Assert.True(long.Parse(response.Item["last_service_sequence"].N) >= 1);
            observations.Add(new { operationId = intent.OperationId.Format(), replica = intent.ExecutionEvent.RequiredDestinations[0].Name,
                terminal = response.Item["terminal"].BOOL, phase = response.Item["phase"].N,
                lastServiceSequence = response.Item["last_service_sequence"].N });
        }
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, postgresQualification ? "automatic-retention-postgres-host-live.json" : "automatic-retention-host-live.json"),
            JsonSerializer.Serialize(new { observedUtc = DateTimeOffset.UtcNow, observations }, new JsonSerializerOptions { WriteIndented = true }), timeout.Token);
    }
}