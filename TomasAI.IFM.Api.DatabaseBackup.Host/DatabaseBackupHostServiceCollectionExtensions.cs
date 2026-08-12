using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Api.DatabaseBackup.Host.Services;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Journal;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Api.DatabaseBackup.Host;

public static class DatabaseBackupHostServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseBackupHost(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var hostOptions = configuration.GetSection(LocalWorkstationDatabaseBackupOptions.SectionName)
            .Get<LocalWorkstationDatabaseBackupOptions>() ?? new LocalWorkstationDatabaseBackupOptions();
        hostOptions.Validate();
        var journalOptions = configuration.GetSection(DatabaseBackupJournalOptions.SectionName)
            .Get<DatabaseBackupJournalOptions>() ?? new DatabaseBackupJournalOptions();
        _ = journalOptions.ValidateAndResolvePath();
        var listenerOptions = configuration.GetSection(NatsJetStreamEventListenerOptions.SectionName)
            .Get<NatsJetStreamEventListenerOptions>() ?? new NatsJetStreamEventListenerOptions();
        listenerOptions.Validate();
        var producerOptions = configuration.GetSection("Nats:JetStreamProducer")
            .Get<NatsJetStreamProducerOptions>() ?? new NatsJetStreamProducerOptions();

        services.AddSingleton(hostOptions);
        services.AddSingleton(journalOptions);
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
        services.AddSingleton<IPostgreSqlBackupCapability, FakePostgreSqlBackupCapability>();
        services.AddSingleton<IScyllaBackupCapability, FakeScyllaBackupCapability>();
        services.AddSingleton<LocalWorkstationDatabaseRecoveryProcessor>();
        services.AddSingleton<IDatabaseRecoveryProcessor>(static provider =>
            provider.GetRequiredService<LocalWorkstationDatabaseRecoveryProcessor>());
        services.AddSingleton<IDatabaseRecoveryOperationExecutor>(static provider =>
            provider.GetRequiredService<LocalWorkstationDatabaseRecoveryProcessor>());
        services.AddSingleton<IDatabaseRecoveryProcessorRegistry, DatabaseRecoveryProcessorRegistry>();
        services.AddSingleton<IDatabaseBackupServiceEventTransport, JetStreamDatabaseBackupServiceEventTransport>();
        services.AddSingleton<DatabaseBackupHostHealthState>();

        services.AddSingleton<DatabaseBackupJournalInitializationService>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupJournalInitializationService>());
        services.AddSingleton<DatabaseBackupOutboxPublisher>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupOutboxPublisher>());
        services.AddSingleton<DatabaseBackupStartupReconciliationService>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupStartupReconciliationService>());
        services.AddSingleton<DatabaseBackupExecutionDispatcher>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupExecutionDispatcher>());
        services.AddSingleton<DatabaseBackupInboundListener>();
        services.AddHostedService(static provider => provider.GetRequiredService<DatabaseBackupInboundListener>());

        services.AddHealthChecks()
            .AddCheck<DatabaseBackupLivenessHealthCheck>("database-backup-liveness", tags: ["live"])
            .AddCheck<DatabaseBackupReadinessHealthCheck>("database-backup-readiness", tags: ["ready"]);
        return services;
    }
}
