using TomasAI.IFM.Shared.MarketData.Definitions;
using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

/// <summary>Initializes a new DatabentoContractDetailsQueryResult instance.</summary>
/// <param name="Status">The status.</param>
/// <param name="Details">The details.</param>
/// <param name="ErrorMessage">The error message.</param>
public sealed record DatabentoContractDetailsQueryResult(
    DatabentoFeedStatus Status,
    IReadOnlyList<ContractDetail?> Details,
    string? ErrorMessage)
{
    public bool IsSuccess => Status == DatabentoFeedStatus.Ok;

    /// <summary>Creates a successful query result containing the retrieved contract details.</summary>
    /// <param name="details">The details.</param>
    /// <returns>The success result.</returns>
    public static DatabentoContractDetailsQueryResult Success(
        IReadOnlyList<ContractDetail?> details) =>
        new(DatabentoFeedStatus.Ok, details, null);

    /// <summary>Creates an unsuccessful query result containing the provider status and error detail.</summary>
    /// <param name="status">The native provider result status.</param>
    /// <param name="errorMessage">The provider&apos;s diagnostic failure message.</param>
    /// <returns>The failure result.</returns>
    public static DatabentoContractDetailsQueryResult Failure(
        DatabentoFeedStatus status,
        string errorMessage) =>
        new(status, [], errorMessage);
}
