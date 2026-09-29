namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Provides the authoritative, non-null futures value date.</summary>
public interface IValueDateProvider
{
    /// <summary>Gets the authoritative value date for the provider's current clock.</summary>
    DateOnly ValueDate { get; }

    /// <summary>Resolves the authoritative value date for a specific instant.</summary>
    DateOnly GetValueDate(DateTimeOffset instant);
}
