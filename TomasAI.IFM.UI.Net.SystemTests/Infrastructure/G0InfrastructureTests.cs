using System.Text.Json;
using FluentAssertions;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

[Trait("Category", "G0Infrastructure")]
public sealed class G0InfrastructureTests
{
    [Fact]
    public void Secret_redactor_removes_exact_credentials_and_named_assignments()
    {
        var redactor = new SecretRedactor(["live-fmp-secret"]);

        var redacted = redactor.Redact(
            "FMP_API_KEY=live-fmp-secret Authorization: Bearer another-token password=hunter2 safe=value");

        redacted.Should().NotContain("live-fmp-secret");
        redacted.Should().NotContain("another-token");
        redacted.Should().NotContain("hunter2");
        redacted.Should().Contain("safe=value");
        redacted.Should().Contain("[REDACTED]");
    }

    [Fact]
    public async Task Audit_recorder_continues_after_failed_and_blocked_steps()
    {
        var run = NewRun();
        var recorder = new G0AuditRecorder(run);

        await recorder.RunAsync(
            "G0-001", "failure", "pass", _ => throw new InvalidOperationException("broken"), CancellationToken.None);
        await recorder.RunAsync(
            "G0-002", "blocked", "dependency", _ => throw new G0DependencyException("NATS absent"), CancellationToken.None);
        await recorder.RunAsync(
            "G0-003", "pass", "pass", _ => Task.FromResult(new G0StepObservation("pass", "done")), CancellationToken.None);

        run.Steps.Select(step => step.Status).Should().Equal(
            G0StepStatus.Failed,
            G0StepStatus.BlockedDependency,
            G0StepStatus.Passed);
        run.Steps[0].Error.Should().Contain("InvalidOperationException");
        run.Steps[1].Error.Should().BeNull();
    }

    [Fact]
    public void Readiness_document_exposes_registered_actor_type_count()
    {
        const string json = """
        {
          "status": "Healthy",
          "entries": {
            "actor_runtime": {
              "status": "Healthy",
              "description": "ready",
              "durationMilliseconds": 1.2,
              "data": { "registeredActorTypes": 81 }
            }
          }
        }
        """;

        var document = JsonSerializer.Deserialize<ApiReadinessDocument>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        document.Should().NotBeNull();
        document!.Status.Should().Be("Healthy");
        document.RegisteredActorTypes.Should().Be(81);
    }

    [Theory]
    [InlineData("127.0.0.1:4222", 4222)]
    [InlineData("[::1]:22543", 22543)]
    [InlineData("invalid", -1)]
    public void Network_endpoint_parser_handles_IPv4_and_IPv6(string endpoint, int expectedPort)
        => InfrastructureProbe.GetPort(endpoint).Should().Be(expectedPort);

    [Fact]
    public async Task Evidence_writer_emits_machine_and_human_readable_redacted_results()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ifm-g0-{Guid.NewGuid():N}");
        try
        {
            var configuration = NewConfiguration(root);
            var writer = new G0EvidenceWriter(configuration, new SecretRedactor(["do-not-write-me"]));
            var run = NewRun();
            run.Steps.Add(new G0StepResult(
                "G0-001", "configuration", G0StepStatus.Failed,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                "safe", "api-key=do-not-write-me", "token=do-not-write-me", []));
            run.CompletedUtc = DateTimeOffset.UtcNow;

            await writer.WriteResultAsync(run);

            var json = await File.ReadAllTextAsync(Path.Combine(writer.RunDirectory, "result.json"));
            var markdown = await File.ReadAllTextAsync(Path.Combine(writer.RunDirectory, "summary.md"));
            json.Should().NotContain("do-not-write-me");
            markdown.Should().NotContain("do-not-write-me");
            json.Should().Contain("G0-001");
            markdown.Should().Contain("Failed");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    static G0RunResult NewRun()
        => new()
        {
            RunId = "unit-test",
            Environment = "Development",
            StartedUtc = DateTimeOffset.UtcNow
        };

    static G0Configuration NewConfiguration(string resultsRoot)
        => new()
        {
            RunId = "unit-test",
            EnvironmentName = "Development",
            RepositoryRoot = resultsRoot,
            ApiExecutable = Path.Combine(resultsRoot, "api.exe"),
            DesktopExecutable = Path.Combine(resultsRoot, "ui.exe"),
            ResultsRoot = resultsRoot,
            ApiReadyUri = new Uri("http://localhost:22543/health/ready"),
            NatsUri = new Uri("nats://localhost:4222"),
            PostgreSql = new G0Endpoint("PostgreSQL", "localhost", 5432),
            ScyllaDb = new G0Endpoint("ScyllaDB", "localhost", 9042),
            Redis = new G0Endpoint("Redis", "localhost", 6379),
            FmpAdapter = "Production",
            FmpCredentialPresent = true,
            DeterministicAdapterApproved = false,
            ReadinessTimeout = TimeSpan.FromSeconds(1),
            StartupTimeout = TimeSpan.FromSeconds(1),
            ShutdownTimeout = TimeSpan.FromSeconds(1)
        };
}
