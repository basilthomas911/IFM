using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Domain;
using global::TomasAI.IFM.Shared.EventModelActor;
using global::TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Validation;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;

using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Validation;

/// <summary>Checks reference-dependent command fields outside the actor.</summary>
public static class FuturesOptionTickDataReferenceValidation
{
    /// <summary>Validates fields against the resolved reference lookup service.</summary>
    public static List<ValidationError> ValidateReferenceData(
        this ICommand command,
        IReferenceLookupService referenceLookupService)
        => command switch
        {
            StartFuturesOptionTickDataStreamingCommand start => new List<ValidationError>()
                .ValidateFuturesOptionContract(
                    start.Contract,
                    referenceLookupService,
                    start.CommandName),
            InsertFuturesOptionTickDataCommand or StopFuturesOptionTickDataStreamingCommand => [],
            _ => throw new InvalidOperationException(
                $"Unable to validate FuturesOptionTickDataCommandActor reference data for command: {command.Subject}")
        };

}
