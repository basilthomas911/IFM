namespace TomasAI.IFM.Application.MarketData.Worker;

internal sealed record DatasetWorkerArguments(
    string ControlIn,
    string ControlOut,
    string PublicationOut,
    string RetentionAckIn,
    string Dataset,
    DateOnly ValueDate,
    Guid WorkerInstanceId,
    Guid GenerationId,
    TomasAI.IFM.Framework.MarketData.DataBento.FeedDeploymentProfile DeploymentProfile,
    TomasAI.IFM.Framework.MarketData.DataBento.FeedDataSourceMode DataSource,
    TomasAI.IFM.Framework.MarketData.DataBento.SyntheticFeedOptions Synthetic,
    TomasAI.IFM.Application.MarketData.Pricing.OptionPricingRefreshPolicy OptionPricingRefresh)
{
    public static bool TryParse(string[] values, out DatasetWorkerArguments result, out string error)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < values.Length; index += 2)
            map[values[index]] = values[index + 1];
        string? input = null, output = null, publication = null, dataset = null, retentionAck = null;
        var valueDate = default(DateOnly);
        var workerId = Guid.Empty;
        var generationId = Guid.Empty;
        var valid = map.TryGetValue("--control-in", out input)
            && map.TryGetValue("--control-out", out output)
            && map.TryGetValue("--publication-out", out publication)
            && map.TryGetValue("--retention-ack-in", out retentionAck)
            && map.TryGetValue("--dataset", out dataset)
            && map.TryGetValue("--value-date", out var valueDateText)
            && DateOnly.TryParseExact(valueDateText, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out valueDate)
            && map.TryGetValue("--worker-id", out var workerText)
            && Guid.TryParse(workerText, out workerId)
            && map.TryGetValue("--generation-id", out var generationText)
            && Guid.TryParse(generationText, out generationId)
            && !string.IsNullOrWhiteSpace(dataset)
            && workerId != Guid.Empty
            && generationId != Guid.Empty;
        if (!valid)
        {
            result = null!;
            error = "Dataset worker bootstrap arguments are invalid.";
            return false;
        }
        var profile = ParseEnum(map, "--deployment-profile",
            TomasAI.IFM.Framework.MarketData.DataBento.FeedDeploymentProfile.SyntheticCi);
        var dataSource = ParseEnum(map, "--data-source",
            TomasAI.IFM.Framework.MarketData.DataBento.FeedDataSourceMode.Synthetic);
        var synthetic = new TomasAI.IFM.Framework.MarketData.DataBento.SyntheticFeedOptions
        {
            RecordCount = ParseInt(map, "--synthetic-record-count", 1_000_000),
            RecordsPerSecond = ParseInt(map, "--synthetic-records-per-second", 100),
            StartSequence = ParseUlong(map, "--synthetic-start-sequence", 1)
        };
        TomasAI.IFM.Application.MarketData.Pricing.OptionPricingRefreshPolicy refresh;
        try
        {
            refresh = map.TryGetValue("--option-pricing-refresh", out var json)
                ? System.Text.Json.JsonSerializer.Deserialize<TomasAI.IFM.Application.MarketData.Pricing.OptionPricingRefreshPolicy>(json)
                    ?? throw new ArgumentException("Missing option refresh policy.", nameof(values))
                : new();
            refresh.Validate();
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or ArgumentException)
        {
            result = null!;
            error = "Invalid option pricing refresh policy.";
            return false;
        }
        result = new(input!, output!, publication!, retentionAck!, dataset!, valueDate, workerId, generationId,
            profile, dataSource, synthetic, refresh);
        error = string.Empty;
        return true;
    }

    static T ParseEnum<T>(Dictionary<string, string> values, string key, T fallback)
        where T : struct, Enum => values.TryGetValue(key, out var text)
            && Enum.TryParse<T>(text, true, out var value) ? value : fallback;
    static int ParseInt(Dictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var text) && int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
    static ulong ParseUlong(Dictionary<string, string> values, string key, ulong fallback) =>
        values.TryGetValue(key, out var text) && ulong.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
}

