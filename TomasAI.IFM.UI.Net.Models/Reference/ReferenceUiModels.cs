using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.UI.Net.Models.Reference;

/// <summary>Represents one lookup definition used by presentation selectors and editors.</summary>
/// <param name="LookupTypeName">The lookup category name.</param>
/// <param name="ShortCode">The stable short code.</param>
/// <param name="OrderId">The presentation order within the category.</param>
/// <param name="Description">The display description.</param>
/// <param name="CreatedOn">The creation timestamp.</param>
/// <param name="CreatedBy">The creating user.</param>
public sealed record LookupTypeUiModel(
    string LookupTypeName,
    string ShortCode,
    int OrderId,
    string Description,
    DateTime CreatedOn,
    string CreatedBy)
{
    /// <summary>Gets whether the required lookup values are populated.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(LookupTypeName)
        && !string.IsNullOrWhiteSpace(ShortCode);
}

/// <summary>Represents a lookup short code and its presentation order.</summary>
/// <param name="ShortCode">The stable short code.</param>
/// <param name="OrderId">The presentation order within the category.</param>
public sealed record LookupTypeShortCodeUiModel(string ShortCode, int OrderId);

/// <summary>Represents default futures-contract selector values.</summary>
/// <param name="Currency">The default currency.</param>
/// <param name="Exchange">The default exchange.</param>
/// <param name="Multiplier">The default contract multiplier.</param>
/// <param name="SecurityType">The default futures security type.</param>
/// <param name="OptionSecurityType">The default futures-option security type.</param>
/// <param name="Symbol">The default base symbol.</param>
public sealed record DefaultFuturesContractDefinitionsUiModel(
    string Currency,
    string Exchange,
    string Multiplier,
    string SecurityType,
    string OptionSecurityType,
    string Symbol);

/// <summary>Represents futures-option strike-price limits used by presentation workflows.</summary>
/// <param name="Minimum">The minimum supported strike.</param>
/// <param name="Maximum">The maximum supported strike.</param>
/// <param name="Increment">The supported strike increment.</param>
public sealed record FuturesOptionStrikePriceUiModel(int Minimum, int Maximum, int Increment);

/// <summary>Represents an MDI forward-loss ratio used by presentation workflows.</summary>
/// <param name="Mdi">The market-direction indicator value.</param>
/// <param name="TrendDirection">The intrinsic-time trend direction.</param>
/// <param name="TradeType">The applicable trade type.</param>
/// <param name="ForwardLossRatio">The configured forward-loss ratio.</param>
/// <param name="CreatedBy">The creating user.</param>
/// <param name="CreatedOn">The optional creation timestamp.</param>
/// <param name="UpdatedBy">The last updating user.</param>
/// <param name="UpdatedOn">The optional last update timestamp.</param>
public sealed record MdiForwardLossRatioUiModel(
    int Mdi,
    IntrinsicTimeTrendType TrendDirection,
    TradeType TradeType,
    double ForwardLossRatio,
    string CreatedBy,
    DateTime? CreatedOn,
    string UpdatedBy,
    DateTime? UpdatedOn);

/// <summary>Represents one economic-calendar entry displayed or edited by the UI.</summary>
/// <param name="EventDate">The event timestamp in UTC.</param>
/// <param name="CountryCode">The event country code.</param>
/// <param name="EventName">The event name.</param>
/// <param name="Actual">The reported value.</param>
/// <param name="Forecast">The forecast value.</param>
/// <param name="Prior">The prior value.</param>
/// <param name="CreatedOn">The creation timestamp.</param>
/// <param name="CreatedBy">The creating user.</param>
/// <param name="Impact">The optional impact classification.</param>
/// <param name="Unit">The optional value unit.</param>
/// <param name="Change">The optional absolute change.</param>
/// <param name="ChangePercentage">The optional percentage change.</param>
public sealed record EconomicCalendarUiModel(
    DateTime EventDate,
    string CountryCode,
    string EventName,
    string? Actual,
    string? Forecast,
    string? Prior,
    DateTime CreatedOn,
    string CreatedBy,
    string? Impact = null,
    string? Unit = null,
    string? Change = null,
    string? ChangePercentage = null)
{
    /// <summary>Gets whether the required calendar values are populated.</summary>
    public bool IsValid => EventDate > DateTime.MinValue && !string.IsNullOrWhiteSpace(CountryCode);
}

/// <summary>Represents one economic-calendar country-code selector value.</summary>
/// <param name="CountryCode">The country code.</param>
public sealed record EconomicCalendarCountryCodeUiModel(string CountryCode);
