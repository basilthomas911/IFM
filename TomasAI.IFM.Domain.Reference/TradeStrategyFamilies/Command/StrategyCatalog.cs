using TomasAI.IFM.Domain.Reference.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.Command;

/// <summary>Handles the active ConfigurationDb strategy catalog command.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1724", Justification = "The typed command/query extension name follows its published message family; the storage namespace is a separate contract.")]
public static class StrategyCatalog
{
    /// <summary>Validates and applies one catalog operation.</summary>
    /// <param name="command">The catalog command with its operation identity.</param>
    /// <param name="service">The ConfigurationDb transactional catalog owner.</param>
    /// <param name="cancellationToken">Cancels catalog execution.</param>
    /// <returns>Acceptance after the catalog transaction completes.</returns>
    /// <exception cref="InvalidOperationException">The catalog service is unavailable or rejects the transition.</exception>
    /// <exception cref="ArgumentException">The operation and command identities differ.</exception>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this StrategyCatalogCommand command, StrategyCatalogService? service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (service is null)
            throw new InvalidOperationException("ConfigurationDb catalog service is unavailable.");
        var request = StrategyCatalogJson.Read<CatalogCommandRequest>(command.RequestJson);
        if (request.OperationId != command.CommandId)
            throw new ArgumentException("Catalog OperationId must match CommandId.",nameof(command));
        await service.ExecuteAsync(request, command.OriginatedBy, cancellationToken).ConfigureAwait(false);
        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    }
}
