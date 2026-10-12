using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Collection wrapper for a set of futures option contract view models.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractsReadModel
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
    [Key(0)]
    public FuturesOptionContractReadModel[] Contracts { get; init; } = [];

    public FuturesOptionContractsReadModel() { }

    public FuturesOptionContractsReadModel(FuturesOptionContractReadModel[] contracts)
        => Contracts = contracts ?? [];
}
