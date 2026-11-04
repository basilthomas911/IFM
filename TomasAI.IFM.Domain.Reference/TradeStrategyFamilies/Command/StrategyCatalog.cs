using TomasAI.IFM.Domain.Reference.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.StrategyCatalog.Command.Actor;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command;

/// <summary>Handles the active ConfigurationDb strategy catalog command.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1724", Justification = "The typed command/query extension name follows its published message family; the storage namespace is a separate contract.")]
public static class StrategyCatalog
{

    /// <param name="command">
    /// The catalog command with its operation identity.
    /// </param>
    extension(StrategyCatalogCommand command)
    {
        /// <summary>Validates and applies one catalog operation.</summary>
        ///
        /// <param name="service">The ConfigurationDb transactional catalog owner.</param>
        /// <param name="cancellationToken">Cancels catalog execution.</param>
        /// <returns>Acceptance after the catalog transaction completes.</returns>
        /// <exception cref="InvalidOperationException">The catalog service is unavailable or rejects the transition.</exception>
        /// <exception cref="ArgumentException">The operation and command identities differ.</exception>
        public async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(StrategyCatalogService? service,
            ICommandActorContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            if (service is null)
                throw new InvalidOperationException("ConfigurationDb catalog service is unavailable.");
            var request = StrategyCatalogJson.Read<CatalogCommandRequest>(command.RequestJson);
            if (request.OperationId != command.CommandId)
                throw new ArgumentException("Catalog OperationId must match CommandId.", nameof(command));
            var key = request.Operation == CatalogCommandOperation.SaveDraft ? request.Definition?.Key : request.Key;
            if (key is null)
                throw new InvalidOperationException("Catalog version identity is required.");
            var projector = context.Container.Resolve<IEventProjector<StrategyCatalogCommandActor>>();
            await service.ExecuteEventSourcedAsync(request, command.OriginatedBy, projector,
                StrategyCatalogLifecycleCommand.Address(key), cancellationToken).ConfigureAwait(false);
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        }
    }
}
