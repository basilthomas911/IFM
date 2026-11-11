using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;

/// <summary>Prepares newly discovered futures definitions and submits them to the existing Securities catalog actor.</summary>
public sealed class FuturesDefinitionPublisher(ISecuritiesDbReadContext database, IActorProducer producer)
{
    static readonly ActorMailboxId Publisher = new(ActorType.Command, "FuturesDefinitionPreparation");

    /// <summary>Publishes an exact authoritative definition before the option calendar references it.</summary>
    /// <param name="definition">The qualified provider definition.</param>
    /// <param name="token">Cancels reference preparation and command transport.</param>
    /// <returns>Completion after the owning actor accepts the definition, or the same row is already committed.</returns>
    public async Task PublishAsync(FuturesContractV3ReadModel definition, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var previous = await database.GetPersistedFuturesContractAsync(definition.ContractId, token).ConfigureAwait(false);
        if (previous is not null)
        {
            if (previous.Id != definition.Id)
                throw new InvalidDataException($"Futures definition '{definition.ContractId}' conflicts with its catalog identity.");
            // Rollover and catalog amendments own existing rows and their assignment flags.
            return;
        }

        var body = JsonSerializer.SerializeToUtf8Bytes(new { operation = "RegisterDiscoveredFuturesDefinition", definition });
        var command = new AddFuturesContractCommand(definition)
        {
            CommandId = new Guid(SHA256.HashData(body).AsSpan(0, 16)),
            PostEvents = true, ErrorCode = AddFuturesContractCommand.ErrorId,
            Subject = new(ActorType.Command, AddFuturesContractCommand.Actor, AddFuturesContractCommand.Verb, definition.Id.Format())
        };
        await producer.StartAsync(Publisher, token).ConfigureAwait(false);
        var result = await producer.RequestAsync<AddFuturesContractCommand, FuturesContractId, GuidResult>(
            command.Subject, command, command.EntityId, token).ConfigureAwait(false);
        if (!result.Success)
        {
            // A different producer can register the same identity between our read and the command.
            var committed = await database.GetPersistedFuturesContractAsync(definition.ContractId, token).ConfigureAwait(false);
            if (committed?.Id != definition.Id)
                throw new InvalidOperationException($"Securities rejected discovered definition {definition.ContractId} ({result.ErrorCode}): {result.ErrorMessage}");
        }
    }
}
