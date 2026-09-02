using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Validation;

internal static class MarketOutlookSnapshotValidation
{
    internal static List<ValidationError> ValidateSnapshot(
        this List<ValidationError> errors,
        InsertMarketOutlookSnapshotCommand command)
    {
        var snapshot = command.MarketOutlook;
        var eod = snapshot.FuturesEodData;
        if (string.IsNullOrWhiteSpace(snapshot.ContractId) || snapshot.ValueDate == default)
            errors.Add(new("A Market Outlook snapshot identity is required."));
        if (snapshot.ContractId != command.EntityId.ContractId
            || snapshot.ValueDate != command.EntityId.ValueDate
            || command.Subject.EntityId != command.EntityId.Format())
            errors.Add(new("Command routing identity must match the Market Outlook snapshot."));
        if (!string.Equals(eod.ContractId, snapshot.ContractId, StringComparison.Ordinal)
            || eod.ValueDate != snapshot.ValueDate
            || !string.Equals(eod.Symbol, "ES", StringComparison.OrdinalIgnoreCase))
            errors.Add(new("A matching ES EOD identity is required."));
        if (eod.OpenPrice <= 0m || eod.HighPrice <= 0m || eod.LowPrice <= 0m || eod.ClosePrice <= 0m)
            errors.Add(new("Market Outlook OHLC prices must all be positive."));
        if (eod.HighPrice < eod.LowPrice
            || eod.OpenPrice < eod.LowPrice || eod.OpenPrice > eod.HighPrice
            || eod.ClosePrice < eod.LowPrice || eod.ClosePrice > eod.HighPrice)
            errors.Add(new("Market Outlook OHLC prices are inconsistent."));
        if (eod.Volume < 0)
            errors.Add(new("Market Outlook EOD volume cannot be negative."));
        if (snapshot.UpdatedAtUtc == default
            || snapshot.UpdatedAtUtc.Kind != DateTimeKind.Utc
            || snapshot.MarketDataAsOfUtc == default
            || snapshot.MarketDataAsOfUtc.Kind != DateTimeKind.Utc)
            errors.Add(new("Market Outlook timestamps must be non-empty UTC values."));
        return errors;
    }
}
