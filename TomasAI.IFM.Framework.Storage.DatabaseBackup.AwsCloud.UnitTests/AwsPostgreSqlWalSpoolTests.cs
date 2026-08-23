using FluentAssertions;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.PostgreSql;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsPostgreSqlWalSpoolTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), $"ifm-gate9-wal-{Guid.NewGuid():N}");

    [Fact]
    [Trait("Category", "Gate9Unit")]
    public void Full_spool_rejects_new_wal_without_discarding_the_required_segment()
    {
        Directory.CreateDirectory(_root);
        var source = WriteSource("source-1", "required");
        var overflow = WriteSource("source-2", "x");
        var spool = new AwsPostgreSqlWalSpool(Options(8));

        var retained = spool.Enqueue(source, "000000010000000000000001");
        var action = () => spool.Enqueue(overflow, "000000010000000000000002");

        action.Should().Throw<IOException>().WithMessage("*full*not discarded*");
        File.ReadAllText(retained).Should().Be("required");
        Directory.EnumerateFiles(Path.Combine(_root, "spool")).Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "Gate9Unit")]
    public void Persisted_segment_survives_restart_and_changed_same_length_replay_is_rejected()
    {
        Directory.CreateDirectory(_root);
        var original = WriteSource("original", "wal-one");
        var changed = WriteSource("changed", "wal-two");
        var options = Options(64);
        var firstProcess = new AwsPostgreSqlWalSpool(options);
        var retained = firstProcess.Enqueue(original, "000000010000000000000001");

        var restartedProcess = new AwsPostgreSqlWalSpool(options);
        restartedProcess.Enqueue(original, "000000010000000000000001").Should().Be(retained);
        var action = () => restartedProcess.Enqueue(changed, "000000010000000000000001");

        action.Should().Throw<InvalidDataException>().WithMessage("*different content*");
        File.ReadAllText(retained).Should().Be("wal-one");
    }

    string WriteSource(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    AwsCloudDatabaseBackupOptions Options(long maximumBytes) => new()
    {
        WalSpoolPath = Path.Combine(_root, "spool"),
        MaximumWalSpoolBytes = maximumBytes
    };

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
