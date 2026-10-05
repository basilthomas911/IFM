using System.Globalization;
using System.Text.Json;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

internal static class FinancialModelingPrepProviderUtilities
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Validates date ordering and the configured maximum inclusive request range.</summary>
    /// <param name="fromInclusive">The first date to include.</param>
    /// <param name="toInclusive">The last date to include.</param>
    /// <param name="options">The configuration governing provider or feed operation.</param>
    public static void ValidateRange(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        FinancialModelingPrepOptions options)
    {
        if (fromInclusive > toInclusive)
        {
            throw new FinancialModelingPrepValidationException("The inclusive FMP date range has its start after its end.");
        }

        var dayCount = toInclusive.DayNumber - fromInclusive.DayNumber + 1;
        if (dayCount > options.MaximumRequestRangeDays)
        {
            throw new FinancialModelingPrepValidationException(
                $"The requested FMP date range exceeds the configured {options.MaximumRequestRangeDays}-day limit.");
        }
    }

    /// <summary>Partitions an inclusive date range into contiguous bounded request windows.</summary>
    /// <param name="fromInclusive">The first date to include.</param>
    /// <param name="toInclusive">The last date to include.</param>
    /// <param name="maximumWindowDays">The maximum number of inclusive dates in each provider request window.</param>
    /// <returns>The chunk range result.</returns>
    public static IEnumerable<(DateOnly From, DateOnly To)> ChunkRange(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        int maximumWindowDays)
    {
        var chunkFrom = fromInclusive;
        while (chunkFrom <= toInclusive)
        {
            var targetDayNumber = Math.Min(
                toInclusive.DayNumber,
                chunkFrom.DayNumber + maximumWindowDays - 1);
            var chunkTo = DateOnly.FromDayNumber(targetDayNumber);
            yield return (chunkFrom, chunkTo);

            if (chunkTo == toInclusive)
            {
                yield break;
            }

            chunkFrom = chunkTo.AddDays(1);
        }
    }

    /// <summary>Deserializes a provider JSON array, reporting malformed or missing payloads as contract failures.</summary>
    /// <param name="payload">The provider JSON response bytes.</param>
    /// <param name="dataset">The Databento dataset identifier.</param>
    /// <typeparam name="T">The type of the operation result or provider record.</typeparam>
    /// <returns>The deserialize array result.</returns>
    public static IReadOnlyList<T> DeserializeArray<T>(byte[] payload, string dataset)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(payload, JsonOptions)
                ?? throw new FinancialModelingPrepContractException($"FMP returned a null {dataset} payload.");
        }
        catch (JsonException exception)
        {
            throw new FinancialModelingPrepContractException($"FMP returned malformed {dataset} JSON.", exception);
        }
    }

    /// <summary>Runs the operation with caller cancellation and the configured overall provider timeout.</summary>
    /// <param name="options">The configuration governing provider or feed operation.</param>
    /// <param name="callerToken">The caller&apos;s cancellation token.</param>
    /// <param name="operation">The provider operation to execute.</param>
    /// <typeparam name="T">The type of the operation result or provider record.</typeparam>
    /// <returns>An awaitable that completes when the operation finishes.</returns>
    public static async Task<T> RunBoundedAsync<T>(
        FinancialModelingPrepOptions options,
        CancellationToken callerToken,
        Func<CancellationToken, Task<T>> operation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        timeout.CancelAfter(options.TotalOperationTimeout);

        try
        {
            return await operation(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!callerToken.IsCancellationRequested)
        {
            throw new FinancialModelingPrepUnavailableException("The FMP operation exceeded its total timeout.", exception);
        }
    }

    /// <summary>Parses the provider event time into a UTC timestamp.</summary>
    /// <param name="value">The source value used to construct or publish the result.</param>
    /// <returns>The parse event time utc result.</returns>
    public static DateTimeOffset ParseEventTimeUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new FinancialModelingPrepContractException("An FMP economic-calendar row has a missing or invalid date.");
        }

        return parsed.ToUniversalTime();
    }

    /// <summary>Preserves a JSON string, number, or Boolean as text, returning null for a missing value.</summary>
    /// <param name="element">The JSON scalar value to preserve.</param>
    /// <param name="fieldName">The provider field name used in validation errors.</param>
    /// <returns>The preserve scalar result.</returns>
    public static string? PreserveScalar(JsonElement element, string fieldName)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            _ => throw new FinancialModelingPrepContractException(
                $"FMP economic-calendar field '{fieldName}' was not a scalar value.")
        };
    }
}
