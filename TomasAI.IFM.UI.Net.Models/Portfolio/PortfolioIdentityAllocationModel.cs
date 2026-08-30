using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.UI.Net.Models.Portfolio;

/// <summary>UI-safe result of allocating a Portfolio identity.</summary>
public sealed record PortfolioIdentityAllocationModel
{
    public PortfolioId? PortfolioId { get; init; }
    public string DisplayId => PortfolioId?.Format() ?? string.Empty;
    public string Error { get; init; } = string.Empty;
    public bool IsSuccessful => PortfolioId is not null && PortfolioId.Validate().Count == 0 && string.IsNullOrEmpty(Error);

    public static PortfolioIdentityAllocationModel Success(PortfolioId id) =>
        id.Validate().Count == 0
            ? new() { PortfolioId = id }
            : throw new ArgumentException("A positive Portfolio ID is required.", nameof(id));

    public static PortfolioIdentityAllocationModel Failure(string error) =>
        string.IsNullOrWhiteSpace(error)
            ? throw new ArgumentException("An allocation error is required.", nameof(error))
            : new() { Error = error.Trim() };
}
