using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Validation;

/// <summary>Checks reference-dependent command fields outside the actor.</summary>
public static class FuturesOptionContractReferenceValidation
{
    /// <summary>Validates fields against the resolved reference lookup service.</summary>
    public static List<ValidationError> ValidateReferenceData(
        this ICommand command,
        IReferenceLookupService referenceLookupService)
        => command switch
        {
            AddFuturesOptionContractCommand add => new List<ValidationError>()
                .ValidateFuturesOptionContract(add.Contract, referenceLookupService),
            AddFuturesOptionContractsCommand addMany => new List<ValidationError>()
                .ValidateFuturesOptionContracts(addMany.Contracts, referenceLookupService),
            ChangeFuturesOptionContractCommand change => new List<ValidationError>()
                .ValidateFuturesOptionContract(change.Contract, referenceLookupService),
            RemoveFuturesOptionContractCommand => [],
            _ => throw new InvalidOperationException(
                $"Unable to validate FuturesOptionContractCommandActor reference data for command: {command.Subject}")
        };

}
