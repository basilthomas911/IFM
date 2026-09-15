using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Validation;

/// <summary>Checks live and recovery VWAP command payloads before calculation.</summary>
public static class FuturesVwapSignalCommandValidation
{
    /// <summary>Checks a live trade against its VWAP stream.</summary>
    public static List<ValidationError> ValidateLiveInputs(
        this List<ValidationError> errors, UpdateFuturesVwapSignalCommand command)
    {
        ValidateConfiguration(errors, command.EntityId, command.Subject.EntityId, command.Configuration);
        ValidateTrade(errors, command.EntityId, command.Observation, true);
        return errors;
    }

    /// <summary>Checks bounded recovery identity, order, and trade payloads.</summary>
    public static List<ValidationError> ValidateRecoveryInputs(
        this List<ValidationError> errors, RecoverFuturesVwapSignalCommand command)
    {
        ValidateConfiguration(errors, command.EntityId, command.Subject.EntityId, command.Configuration);
        if (command.RecoveryGenerationId == Guid.Empty || command.BatchOrdinal < 0
            || (command.IsFirstBatch && command.BatchOrdinal != 0))
            errors.Add(new("VWAP.RECOVERY.LINEAGE", "Recovery generation and batch ordinal are invalid."));
        if (command.Trades is null || command.Trades.Length > 4096)
            errors.Add(new("VWAP.RECOVERY.SIZE", "A recovery batch must contain at most 4096 trades."));
        else
            foreach (var trade in command.Trades)
                ValidateTrade(errors, command.EntityId, trade, false);
        return errors;
    }

    static void ValidateConfiguration(List<ValidationError> errors,
        FuturesVwapSignalEntityId entityId, string routedId, FuturesVwapConfiguration? configuration)
    {
        if (configuration is null || string.IsNullOrWhiteSpace(configuration.ConfigurationId)
            || entityId.ConfigurationId != configuration.ConfigurationId)
            errors.Add(new("VWAP.CONFIG", "Configuration must match the VWAP stream."));
        if (routedId != entityId.Format())
            errors.Add(new("VWAP.ROUTE", "Command routing identity must match the VWAP stream."));
    }

    static void ValidateTrade(List<ValidationError> errors, FuturesVwapSignalEntityId entityId,
        FuturesVwapTradeObservation? trade, bool live)
    {
        if (trade is null)
        {
            errors.Add(new("VWAP.TRADE.NULL", "A futures trade observation is required."));
            return;
        }
        if (trade.ContractId != entityId.ContractId || trade.ValueDate != entityId.ValueDate)
            errors.Add(new("VWAP.TRADE.IDENTITY", "Trade contract and value date must match the VWAP stream."));
        if (trade.EventTimestampUtc.Offset != TimeSpan.Zero
            || trade.SessionStartUtc.Offset != TimeSpan.Zero
            || trade.SessionEndUtc.Offset != TimeSpan.Zero
            || trade.SessionStartUtc >= trade.SessionEndUtc
            || trade.EventTimestampUtc < trade.SessionStartUtc
            || trade.EventTimestampUtc > trade.SessionEndUtc)
            errors.Add(new("VWAP.TRADE.TIME", "Trade and session timestamps are invalid."));
        if (live && (trade.StreamEpochId == Guid.Empty || trade.TradeOrdinal <= 0))
            errors.Add(new("VWAP.TRADE.LINEAGE", "Live trade lineage is incomplete."));
    }
}
