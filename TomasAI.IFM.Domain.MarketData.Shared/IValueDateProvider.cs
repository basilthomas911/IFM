namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Provides the authoritative, non-null futures value date.</summary>
public interface IValueDateProvider
{
    /// <summary>Gets the authoritative value date for the provider's current clock.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1721", Justification = "The read-model property is serialized data; the method implements an existing conversion/provider contract.")]
    DateOnly ValueDate { get; }

    /// <summary>Resolves the authoritative value date for a specific instant.</summary>
    DateOnly GetValueDate(DateTimeOffset instant);
}
