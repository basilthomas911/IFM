using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>Combines an accepted canonical future and its qualified immutable reference, when present.</summary>
public sealed class PreparedFuturesProjection
{
    PreparedFuturesProjection(FuturesContractV3ReadModel contract)
    {
        Contract = contract;
        Reference = contract.SchemaVersion > 0 ? PreparedReferenceVersion.Prepare(contract) : null;
    }

    public FuturesContractV3ReadModel Contract { get; }
    public PreparedReferenceVersion? Reference { get; }

    public static PreparedFuturesProjection Prepare(FuturesContractV3ReadModel contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return new(contract);
    }
}

/// <summary>Combines an accepted canonical option and its qualified immutable reference, when present.</summary>
public sealed class PreparedOptionProjection
{
    PreparedOptionProjection(FuturesOptionContractReadModel contract)
    {
        Contract = contract;
        Reference = contract.SchemaVersion > 0 ? PreparedReferenceVersion.Prepare(contract) : null;
    }

    public FuturesOptionContractReadModel Contract { get; }
    public PreparedReferenceVersion? Reference { get; }

    public static PreparedOptionProjection Prepare(FuturesOptionContractReadModel contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return new(contract);
    }
}
