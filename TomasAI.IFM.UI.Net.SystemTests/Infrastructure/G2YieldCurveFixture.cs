using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

/// <summary>
/// Isolated manual yield-curve row and the live editor definition used by G2-016 through G2-019.
/// </summary>
public sealed record G2YieldCurveFixture(
    DateOnly ManualDate,
    DateOnly ImportDate,
    YieldCurveRateReadModel AddedRate,
    YieldCurveRateReadModel ChangedRate,
    string DefinitionDescription)
{
    public static async Task<G2YieldCurveFixture> CreateAsync(
        G0QuerySession queries,
        G2Configuration configuration,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(configuration);

        var definitions = Require(
                await queries.Reference.GetMarketDataDefinitionTypesAsync()
                    .WaitAsync(timeout, cancellationToken),
                "MarketDataDefinitionType lookup")
            .ToArray();
        var description = definitions.SingleOrDefault(value => string.Equals(
                value.ShortCode,
                "YieldCurveRates",
                StringComparison.OrdinalIgnoreCase))?.Description
            ?? throw new G0DependencyException(
                "The MarketDataDefinitionType lookup does not contain required short code 'YieldCurveRates'.");

        return new G2YieldCurveFixture(
            configuration.YieldCurveManualDate,
            configuration.ImportDate,
            Rate(configuration.YieldCurveManualDate, 1.01),
            Rate(configuration.YieldCurveManualDate, 2.01),
            description);
    }

    static YieldCurveRateReadModel Rate(DateOnly date, double first)
        => new(
            date,
            Rounded(first, 0),
            Rounded(first, 1),
            Rounded(first, 2),
            Rounded(first, 3),
            Rounded(first, 4),
            Rounded(first, 5),
            Rounded(first, 6),
            Rounded(first, 7),
            Rounded(first, 8),
            Rounded(first, 9),
            Rounded(first, 10),
            Rounded(first, 11));

    static double Rounded(double first, int hundredths)
        => Math.Round(first + hundredths / 100d, 2, MidpointRounding.AwayFromZero);

    static T Require<T>(ServiceResult<T> result, string queryName)
        where T : class
    {
        if (!result.Success || result.Value is null)
            throw new G0DependencyException(
                $"Typed {queryName} query failed: code={result.ErrorCode}; message={result.ErrorMessage}");
        return result.Value;
    }
}
