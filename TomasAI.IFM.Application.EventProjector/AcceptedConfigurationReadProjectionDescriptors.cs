using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventProjector;

namespace TomasAI.IFM.Application.EventProjector;

/// <summary>Explicit offline materialization of accepted configuration migration baselines.</summary>
public static class AcceptedConfigurationReadProjectionDescriptors
{
    /// <summary>Provides a baseline rebuild action without notifications, subscriptions or workflow activation.</summary>
    public static EventProjectionDescriptor Create(IPostgresEventTransaction transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        return new EventProjectionDescriptor(typeof(ConfigurationProjectionBaselineEvent), EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            (_, _) => throw new InvalidOperationException("Use the configuration baseline materialization-only action."),
            _ => null, (_, _) => null, publishProcessingEvent: false, useDurableReplay: false, publishTerminalEvent: false)
            .WithReadModelRebuild((source, token) => new ConfigurationProjectionBaseline(transactions).MaterializeAsync((ConfigurationProjectionBaselineEvent)source, token));
    }
}
