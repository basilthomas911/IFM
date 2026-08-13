using FluentAssertions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class DatabaseRecoveryEngineSelectorTests
{
    [Fact]
    public void Native_mode_maps_only_explicitly_enabled_engines()
    {
        var selector = CreateSelector(postgreSqlEnabled: true, scyllaEnabled: false);

        selector.Select(new DatabaseProtectionSetId("core-postgresql"))
            .Should().Be(DatabaseEngine.PostgreSql);
        selector.CanSelect(new DatabaseProtectionSetId("core-postgresql")).Should().BeTrue();
        selector.CanSelect(new DatabaseProtectionSetId("read-model-scylla")).Should().BeFalse();
        var selectScylla = () => selector.Select(new DatabaseProtectionSetId("read-model-scylla"));
        selectScylla.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Dry_run_mode_keeps_both_fake_engine_routes_available()
    {
        var selector = CreateSelector(postgreSqlEnabled: false, scyllaEnabled: false, dryRun: true);

        selector.Select(new DatabaseProtectionSetId("core-postgresql"))
            .Should().Be(DatabaseEngine.PostgreSql);
        selector.Select(new DatabaseProtectionSetId("read-model-scylla"))
            .Should().Be(DatabaseEngine.ScyllaDb);
    }

    [Fact]
    public void Native_mode_requires_at_least_one_enabled_engine()
    {
        var options = new LocalWorkstationSourceOptions
        {
            Enabled = true,
            DryRun = false,
            PostgreSqlEnabled = false,
            ScyllaEnabled = false
        };

        var validate = options.Validate;

        validate.Should().Throw<InvalidOperationException>();
    }

    static LocalWorkstationDatabaseRecoveryEngineSelector CreateSelector(
        bool postgreSqlEnabled,
        bool scyllaEnabled,
        bool dryRun = false)
        => new(
            new PostgreSqlBackupOptions { AllowedProtectionSets = ["core-postgresql"] },
            new ScyllaBackupOptions
            {
                ProtectionSets = new Dictionary<string, ScyllaProtectionSetOptions>(StringComparer.OrdinalIgnoreCase)
                {
                    ["read-model-scylla"] = new()
                }
            },
            new LocalWorkstationSourceOptions
            {
                Enabled = true,
                DryRun = dryRun,
                PostgreSqlEnabled = postgreSqlEnabled,
                ScyllaEnabled = scyllaEnabled
            });
}
