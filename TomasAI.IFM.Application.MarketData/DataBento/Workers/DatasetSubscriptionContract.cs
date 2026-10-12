using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>
/// Resolved provider mapping, not a subscription lease. Stage 2 contract authority remains
/// responsible for assigning the fixed futures roles; workers must not reconstruct provider
/// symbols or role flags from a domain contract identifier.
/// </summary>
[MessagePackObject]
public sealed record DatasetSubscriptionContract
{
    [Key(0)] public required string DomainContractId { get; init; }
    [Key(1)] public required string ProviderContractName { get; init; }
    [Key(2)] public required AssetTypeId AssetTypeId { get; init; }
    [Key(3)] public required string RootSymbol { get; init; }
    [Key(4)] public required string Dataset { get; init; }
    [Key(5)] public bool OnTheRun { get; init; }
    [Key(6)] public bool Rollover { get; init; }

    public static DatasetSubscriptionContract FromRegistration(DatabentoContractRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        return new DatasetSubscriptionContract
        {
            DomainContractId = registration.DomainContractId,
            ProviderContractName = registration.ProviderContractName,
            AssetTypeId = registration.AssetTypeId,
            RootSymbol = registration.RootSymbol ?? string.Empty,
            Dataset = registration.Dataset ?? string.Empty,
            OnTheRun = registration.OnTheRun,
            Rollover = registration.Rollover
        };
    }

    public DatabentoContractRegistration ToRegistration() => new()
    {
        DomainContractId = DomainContractId,
        ProviderContractName = ProviderContractName,
        AssetTypeId = AssetTypeId,
        RootSymbol = RootSymbol,
        Dataset = Dataset,
        OnTheRun = OnTheRun,
        Rollover = Rollover
    };
}
