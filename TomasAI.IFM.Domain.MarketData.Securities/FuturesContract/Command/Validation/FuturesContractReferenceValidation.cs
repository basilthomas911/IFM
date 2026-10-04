using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.Validation;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Validation;

/// <summary>Checks reference-dependent command fields outside the actor.</summary>
public static class FuturesContractReferenceValidation
{
    /// <summary>Validates fields against the resolved reference lookup service.</summary>
    public static List<ValidationError> ValidateReferenceData(
        this ICommand command,
        IReferenceLookupService referenceLookupService)
        => command switch
        {
            AddFuturesContractCommand add => new List<ValidationError>()
                .ValidateFuturesContract(add.Contract, referenceLookupService),
            ChangeFuturesContractCommand change => new List<ValidationError>()
                .ValidateFuturesContract(change.Contract, referenceLookupService),
            RemoveFuturesContractCommand => [],
            _ => throw new InvalidOperationException(
                $"Unable to validate FuturesContractCommandActor reference data for command: {command.Subject}")
        };

}
