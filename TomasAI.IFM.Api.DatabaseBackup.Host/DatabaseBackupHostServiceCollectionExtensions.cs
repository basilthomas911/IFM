using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Api.DatabaseBackup.Host.Services;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Journal;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.PostgreSql;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Publication;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Scylla;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Startup;

namespace TomasAI.IFM.Api.DatabaseBackup.Host;

public static class DatabaseBackupHostServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseBackupHost(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var hostOptions = configuration.GetSection(DatabaseBackupHostOptions.SectionName)
            .Get<DatabaseBackupHostOptions>() ?? new DatabaseBackupHostOptions();
        hostOptions.Validate();
        var journalOptions = configuration.GetSection(DatabaseBackupJournalOptions.SectionName)
            .Get<DatabaseBackupJournalOptions>() ?? new DatabaseBackupJournalOptions();
        _ = journalOptions.ValidateAndResolvePath();
        var listenerOptions = configuration.GetSection(NatsJetStreamEventListenerOptions.SectionName)
            .Get<NatsJetStreamEventListenerOptions>() ?? new NatsJetStreamEventListenerOptions();
        listenerOptions.Validate();
        var producerOptions = configuration.GetSection("Nats:JetStreamProducer")
            .Get<NatsJetStreamProducerOptions>() ?? new NatsJetStreamProducerOptions();
        var sourceOptions = configuration.GetSection(LocalWorkstationSourceOptions.SectionName)
            .Get<LocalWorkstationSourceOptions>() ?? new LocalWorkstationSourceOptions();
        sourceOptions.Validate();
        var awsOptions = configuration.GetSection(AwsCloudDatabaseBackupOptions.SectionName)
            .Get<AwsCloudDatabaseBackupOptions>() ?? new AwsCloudDatabaseBackupOptions();
        awsOptions.Validate();
        var postgreSqlOptions = configuration.GetSection(PostgreSqlBackupOptions.SectionName)
            .Get<PostgreSqlBackupOptions>() ?? new PostgreSqlBackupOptions();
        var scyllaOptions = configuration.GetSection(ScyllaBackupOptions.SectionName)
            .Get<ScyllaBackupOptions>() ?? new ScyllaBackupOptions();
        var publicationOptions = configuration.GetSection(DatabaseBackupPublicationOptions.SectionName)
            .Get<DatabaseBackupPublicationOptions>() ?? new DatabaseBackupPublicationOptions();
        var useNativePostgreSql = sourceOptions.Enabled && !sourceOptions.DryRun
            && sourceOptions.PostgreSqlEnabled;
        var useNativeScylla = sourceOptions.Enabled && !sourceOptions.DryRun
            && sourceOptions.ScyllaEnabled;
        if (useNativePostgreSql)
            postgreSqlOptions.Validate();
        if (useNativeScylla)
            scyllaOptions.Validate();
        if (useNativePostgreSql || useNativeScylla)
            publicationOptions.Validate(requirePrivateKey: true);

        services.AddSingleton(hostOptions);
        services.AddSingleton(journalOptions);
        services.AddSingleton(sourceOptions);
        services.AddSingleton(postgreSqlOptions);
        services.AddSingleton(scyllaOptions);
        services.AddSingleton(publicationOptions);
        services.AddSingleton(publicationOptions.Manifest);
        services.AddAwsCloudDatabaseBackup(awsOptions);
        if (awsOptions.Enabled)
        {
            services.AddSingleton<IDatabaseNativeArtifactSource, LocalDatabaseNativeArtifactSource>();
            services.AddSingleton<IDatabaseNativeRestoreArtifactSink, LocalDatabaseNativeRestoreArtifactSink>();
        }
        services.AddSingleton<INatsJetStreamEventListenerOptions>(listenerOptions);
        services.AddSingleton<INatsJetStreamProducerOptions>(producerOptions);
        services.AddSingleton<NatsConnectionManager>();
        services.AddSingleton<IJSActorEventListener>(provider => new NatsJetStreamEventListener(
            provider.GetRequiredService<INatsJetStreamEventListenerOptions>(),
            provider.GetRequiredService<ILogger<NatsJetStreamEventListener>>(),
            provider.GetRequiredService<NatsConnectionManager>()));
        services.AddSingleton<IJSActorProducer>(provider => new NatsJetStreamActorProducer(
            provider.GetRequiredService<INatsJetStreamProducerOptions>(),
            provider.GetRequiredService<ILogger<NatsJetStreamActorProducer>>(),
            provider.GetRequiredService<NatsConnectionManager>()));

        services.AddSingleton<IDatabaseBackupExecutionJournal, SqliteDatabaseBackupExecutionJournal>();
        if (useNativePostgreSql)
        {
            services.AddSingleton<PostgreSqlBackupCapability>();
            services.AddSingleton<IPostgreSqlBackupCapability>(static provider =>
                provider.GetRequiredService<PostgreSqlBackupCapability>());
            services.AddSingleton<IDatabaseNativeCapabilityValidation>(static provider =>
                provider.GetRequiredService<PostgreSqlBackupCapability>());
        }
        else
        {
            services.AddSingleton<IPostgreSqlBackupCapability, FakePostgreSqlBackupCapability>();
        }
        if (useNativeScylla)
        {
            if (scyllaOptions.PortableSnapshot.Enabled)
                services.AddSingleton<IScyllaSnapshotArtifactTransport, S3ScyllaSnapshotArtifactTransport>();
            services.AddSingleton<ScyllaBackupCapability>();
            services.AddSingleton<IScyllaBackupCapability>(static provider =>
                provider.GetRequiredService<ScyllaBackupCapability>());
            services.AddSingleton<IDatabaseNativeCapabilityValidation>(static provider =>
                provider.GetRequiredService<ScyllaBackupCapability>());
        }
        else
        {
            services.AddSingleton<IScyllaBackupCapability, FakeScyllaBackupCapability>();
        }
        if (useNativePostgreSql || useNativeScylla)
        {
            services.AddSingleton<IBackupPathPolicy, LocalBackupPathPolicy>();
            services.AddSingleton<IArtifactChecksumService, Sha256ArtifactChecksumService>();
            services.AddSingleton<ILocalBackupCapacityReader, LocalBackupCapacityReader>();
            services.AddSingleton<IManifestSignatureService, EcdsaManifestSignatureService>();
            services.AddSingleton<LocalBackupManifestStore>();
            services.AddSingleton<IDatabaseBackupManifestWriter>(static provider =>
                provider.GetRequiredService<LocalBackupManifestStore>());
            services.AddSingleton<IDatabaseBackupManifestReader>(static provider =>
                provider.GetRequiredService<LocalBackupManifestStore>());
            services.AddSingleton<LocalBackupRepository>();
            services.AddSingleton<IDatabaseBackupPublicationCapability>(static provider =>
                provider.GetRequiredService<LocalBackupRepository>());
            services.AddSingleton<IDatabaseRestoreSourceCapability>(static provider =>
                provider.GetRequiredService<LocalBackupRepository>());
            services.AddSingleton<ILocalBackupVault>(static provider =>
                provider.GetRequiredService<LocalBackupRepository>());
            services.AddSingleton<IOfflineBackupMediaProvider>(static provider =>
                provider.GetRequiredService<LocalBackupRepository>());
            services.AddSingleton<IRestoreWorkspace>(static provider =>
                provider.GetRequiredService<LocalBackupRepository>());
            services.AddSingleton<IDatabaseBackupCatalog>(static provider =>
                provider.GetRequiredService<LocalBackupRepository>());
            services.AddSingleton<LocalBackupChainPlanner>();
            services.AddSingleton<IDatabaseBackupChainPlanner>(static provider =>
                provider.GetRequiredService<LocalBackupChainPlanner>());
            services.AddSingleton<LocalBackupGovernanceStore>();
            services.AddSingleton<IDatabaseRetentionCapability>(static provider =>
                provider.GetRequiredService<LocalBackupGovernanceStore>());
            services.AddSingleton<IDatabaseRecoveryEvidenceStore>(static provider =>
                provider.GetRequiredService<LocalBackupGovernanceStore>());
            services.AddSingleton<IDatabaseRecoveryRunStatsCollector, DatabaseRecoveryRunStatsCollector>();
        }
        else
        {
            services.AddSingleton<IDatabaseBackupPublicationCapability, FakeDatabaseBackupPublicationCapability>();
            services.AddSingleton<IDatabaseRestoreSourceCapability, FakeDatabaseRestoreSourceCapability>();
            services.AddSingleton<IDatabaseRecoveryEvidenceStore, FakeDatabaseRecoveryEvidenceStore>();
            services.AddSingleton<IDatabaseBackupChainPlanner, FakeDatabaseBackupChainPlanner>();
        }
        services.AddSingleton<IDatabaseRecoveryEngineSelector, LocalWorkstationDatabaseRecoveryEngineSelector>();
        services.AddSingleton<LocalWorkstationDatabaseRecoveryProcessor>();
        services.AddSingleton<IDatabaseRecoveryProcessor>(static provider =>
            provider.GetRequiredService<LocalWorkstationDatabaseRecoveryProcessor>());
        services.AddSingleton<DatabaseRecoveryProcessorRegistry>();
        services.AddSingleton<IDatabaseRecoveryProcessorRegistry>(static provider => provider.GetRequiredService<DatabaseRecoveryProcessorRegistry>());
        services.AddSingleton<IDatabaseRecoveryOperationExecutor>(static provider => provider.GetRequiredService<DatabaseRecoveryProcessorRegistry>());
        services.AddSingleton<IDatabaseBackupServiceEventTransport, JetStreamDatabaseBackupServiceEventTransport>();
        services.AddSingleton<DatabaseBackupHostHealthState>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DatabaseBackupSourceHealthRegistry>(provider =>
        {
            var registry = new DatabaseBackupSourceHealthRegistry(provider.GetRequiredService<TimeProvider>());
            registry.Set(BackupSource.LocalWorkstation, sourceOptions.Enabled, ready: true,
                sourceOptions.Enabled ? "configured" : "disabled-dry-run");
            registry.Set(BackupSource.AwsCloud, awsOptions.Enabled, ready: !awsOptions.Enabled,
                awsOptions.Enabled ? "identity-preflight-pending" : "disabled");
            return registry;
        });
        services.AddSingleton<IDatabaseBackupSourceHealthRegistry>(static provider => provider.GetRequiredService<DatabaseBackupSourceHealthRegistry>());

        services.AddSingleton<DatabaseBackupJournalInitializationService>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupJournalInitializationService>());
        services.AddSingleton<DatabaseBackupNativeCapabilityValidationService>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupNativeCapabilityValidationService>());
        services.AddSingleton<AwsCloudIdentityValidationService>();
        services.AddHostedService(static provider => provider.GetRequiredService<AwsCloudIdentityValidationService>());
        services.AddSingleton<DatabaseBackupOutboxPublisher>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupOutboxPublisher>());
        services.AddSingleton<DatabaseBackupStartupReconciliationService>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupStartupReconciliationService>());
        services.AddSingleton<DatabaseBackupExecutionDispatcher>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupExecutionDispatcher>());
        if (awsOptions.Enabled)
        {
            services.AddSingleton<AwsDatabaseBackupRuntimeService>();
            services.AddHostedService(static provider => provider.GetRequiredService<AwsDatabaseBackupRuntimeService>());
            if (awsOptions.CloudWatchMetricsEnabled)
            {
                services.AddSingleton<AwsCloudWatchMetricExportService>();
                services.AddHostedService(static provider => provider.GetRequiredService<AwsCloudWatchMetricExportService>());
            }
        }
        services.AddSingleton<DatabaseBackupInboundListener>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupInboundListener>());

        services.AddHealthChecks()
            .AddCheck<DatabaseBackupLivenessHealthCheck>("database-backup-liveness", tags: ["live"])
            .AddCheck<DatabaseBackupReadinessHealthCheck>("database-backup-readiness", tags: ["ready"])
            .AddCheck<DatabaseBackupSourcesHealthCheck>("database-backup-sources", tags: ["ready"]);
        return services;
    }
}
