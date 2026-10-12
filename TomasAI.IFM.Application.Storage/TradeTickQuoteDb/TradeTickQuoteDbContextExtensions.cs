using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

/// <summary>Internal raw tick validation.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# 14 context extension API keeps the context receiver.")]
internal static class TradeTickQuoteDbContextExtensions
{
    extension(TradeTickQuoteDbContext context)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the context extension API.")]
        internal void ValidateScope(string contractId, DateOnly valueDate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
            if (valueDate == default) throw new ArgumentException("A tick value date is required.", nameof(valueDate));
        }
        internal void ValidateTickIdentity([System.Diagnostics.CodeAnalysis.NotNull] FuturesTickDataId? value)
        {
            ArgumentNullException.ThrowIfNull(value);
            context.ValidateScope(value.ContractId, value.ValueDate);
        }
        internal void ValidateOptionTickIdentity([System.Diagnostics.CodeAnalysis.NotNull] FuturesOptionTickDataId? value)
        {
            ArgumentNullException.ThrowIfNull(value);
            context.ValidateScope(value.ContractId, value.ValueDate);
        }
        internal void ValidateFuturesIdentity([System.Diagnostics.CodeAnalysis.NotNull] FuturesDataId? value)
        {
            ArgumentNullException.ThrowIfNull(value);
            context.ValidateScope(value.ContractId, value.ValueDate);
        }
        internal void ValidateTick([System.Diagnostics.CodeAnalysis.NotNull] FuturesTickDataV2ReadModel? value)
        {
            ArgumentNullException.ThrowIfNull(value);
            context.ValidateScope(value.ContractId, value.ValueDate);
        }
        internal void ValidateOptionTick([System.Diagnostics.CodeAnalysis.NotNull] FuturesOptionTickDataV2ReadModel? value)
        {
            ArgumentNullException.ThrowIfNull(value);
            context.ValidateScope(value.ContractId, value.ValueDate);
        }
        internal void ValidateTicks([System.Diagnostics.CodeAnalysis.NotNull] ICollection<FuturesTickDataV2ReadModel>? values)
        {
            ArgumentNullException.ThrowIfNull(values);
            foreach (var value in values) context.ValidateTick(value);
            if (values.Select(row => (row.ContractId, row.ValueDate, row.TickId)).Distinct().Count() != values.Count)
                throw new ArgumentException("Duplicate raw tick identities in one batch.", nameof(values));
        }
        internal void ValidateOptionTicks([System.Diagnostics.CodeAnalysis.NotNull] ICollection<FuturesOptionTickDataV2ReadModel>? values)
        {
            ArgumentNullException.ThrowIfNull(values);
            foreach (var value in values) context.ValidateOptionTick(value);
            if (values.Select(row => (row.ContractId, row.ValueDate, row.TickId)).Distinct().Count() != values.Count)
                throw new ArgumentException("Duplicate raw tick identities in one batch.", nameof(values));
        }
    }
}
