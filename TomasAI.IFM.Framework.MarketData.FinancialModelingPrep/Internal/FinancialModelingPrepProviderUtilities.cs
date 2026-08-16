using System.Globalization;
using System.Text.Json;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

internal static class FinancialModelingPrepProviderUtilities
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
