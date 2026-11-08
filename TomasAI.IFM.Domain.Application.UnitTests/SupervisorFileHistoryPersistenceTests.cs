using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Application.Api.Server.Core.Observability.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class SupervisorFileHistoryPersistenceTests
{
    static SupervisorHealthHistoryPoint Point(int revision) => new(revision, DateTime.UnixEpoch.AddMinutes(revision),
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, default, 0, 0, 0, 0, 0, 0, 0, TimeSpan.Zero, 0, 0);

    [Fact]
    public async Task Concurrent_reads_and_appends_match_restart_and_retention()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-history-" + Guid.NewGuid().ToString("N"));
        try
        {
            var environment = new Environment(root);
            var store = new SupervisorFileHistoryPersistence(environment);
            var writers = Enumerable.Range(0, 140).Select(async index =>
            {
                await store.AppendAsync(Point(index), default);
                var read = await store.ReadAsync(DateTime.MinValue, DateTime.MaxValue, 1000, default);
                Assert.Equal(read.OrderByDescending(point => point.ObservedUtc), read);
            });
            await Task.WhenAll(writers);
            var expected = Enumerable.Range(0, 140).Reverse().Select(index => (long)index);
            Assert.Equal(expected, (await store.ReadAsync(DateTime.MinValue, DateTime.MaxValue, 1000, default)).Select(point => point.Revision));
            Assert.Equal(expected, (await new SupervisorFileHistoryPersistence(environment).ReadAsync(DateTime.MinValue, DateTime.MaxValue, 1000, default)).Select(point => point.Revision));
            var range = await store.ReadAsync(Point(20).ObservedUtc, Point(30).ObservedUtc, 4, default);
            Assert.Equal(new long[] { 30, 29, 28, 27 }, range.Select(point => point.Revision));
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public async Task Cold_recovery_ignores_truncated_lines_and_bounds_retention_with_stable_ties()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-history-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Logs"));
            var points = Enumerable.Range(0, 10100).Select(index => Point(index) with { ObservedUtc = DateTime.UnixEpoch });
            await File.WriteAllLinesAsync(Path.Combine(root, "Logs", "actor-health-history.jsonl"),
                points.Select(point => System.Text.Json.JsonSerializer.Serialize(point, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))).Append("{truncated"));
            var store = new SupervisorFileHistoryPersistence(new Environment(root));
            Assert.Equal(Enumerable.Range(0, 10080).Select(index => (long)index),
                (await store.ReadAsync(DateTime.MinValue, DateTime.MaxValue, int.MaxValue, default)).Select(point => point.Revision));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await store.ReadAsync(DateTime.MinValue, DateTime.MaxValue, 1, cancelled.Token));
        }
        finally { Cleanup(root); }
    }

    static void Cleanup(string root)
    {
        var resolved = Path.GetFullPath(root);
        var parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        var name = Path.GetFileName(resolved);
        if (!string.Equals(Path.GetDirectoryName(resolved), parent, StringComparison.OrdinalIgnoreCase)
            || !name.StartsWith("ifm-history-", StringComparison.Ordinal)
            || !Guid.TryParseExact(name[12..], "N", out _)) throw new InvalidOperationException("Unexpected history fixture path.");
        if (Directory.Exists(resolved))
        {
            if ((File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Fixture root must not be a link.");
            Directory.Delete(resolved, true);
        }
    }

    sealed class Environment(string path) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Fixture";
        public string ContentRootPath { get; set; } = path;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
