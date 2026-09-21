using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

public partial class SecuritiesDbContext
{
    async Task<ReferenceVersionStore.Pending?> StageReferenceAsync(FuturesContractV3ReadModel value, string? originalId = null)
    {
        var previous = await GetFuturesContractAsync(originalId ?? value.ContractId);
        if (value.SchemaVersion == 0)
        {
            if (previous?.SchemaVersion > 0) throw new InvalidOperationException("A provider-bound contract cannot lose its reference metadata.");
            return null;
        }
        if (originalId is not null && originalId != value.ContractId
            || previous is not null && (previous.Symbol != value.Symbol || previous.LastTradeDate != value.LastTradeDate))
            throw new InvalidOperationException("Contract identity changes require Add, not replacement.");
        if (previous?.ReviewState == ReferenceReviewState.Reviewed && value.ReviewState != ReferenceReviewState.Reviewed)
            throw new InvalidOperationException("A reviewed reference cannot be replaced by an unqualified draft.");
        return await new ReferenceVersionStore(this).StageAsync(value);
    }
    async Task<ReferenceVersionStore.Pending?> StageReferenceAsync(FuturesOptionContractReadModel value, string? originalId = null)
    {
        var previous = await GetFuturesOptionContractAsync(originalId ?? value.ContractId);
        if (value.SchemaVersion == 0)
        {
            if (previous?.SchemaVersion > 0) throw new InvalidOperationException("A provider-bound contract cannot lose its reference metadata.");
            return null;
        }
        if (originalId is not null && originalId != value.ContractId
            || previous is not null && (previous.Symbol != value.Symbol || previous.ContractMonth != value.ContractMonth
                || previous.OptionType != value.OptionType || previous.GetExactStrikePrice() != value.GetExactStrikePrice()))
            throw new InvalidOperationException("Contract identity changes require Add, not replacement.");
        if (previous?.ReviewState == ReferenceReviewState.Reviewed && value.ReviewState != ReferenceReviewState.Reviewed)
            throw new InvalidOperationException("A reviewed reference cannot be replaced by an unqualified draft.");
        if (string.IsNullOrWhiteSpace(value.UnderlyingContractId))
            throw new InvalidOperationException("An exact existing underlying futures contract is required.");
        var underlying = await GetFuturesContractAsync(value.UnderlyingContractId);
        if (underlying is null || underlying.SecurityType != "FUT" || underlying.SchemaVersion != 1
            || underlying.Dataset != value.Dataset || underlying.Symbol != value.Symbol
            || value.UnderlyingInstrumentId is { } providerId && providerId != underlying.InstrumentId
            || value.UnderlyingPublisherId is { } publisherId && publisherId != underlying.PublisherId
            || value.ExpirationUtc is not null && (underlying.ExpirationUtc is null || underlying.ExpirationUtc < value.ExpirationUtc)
            || value.ReviewState == ReferenceReviewState.Reviewed && FuturesReferenceQualification.Errors(underlying).Count != 0)
            throw new InvalidOperationException("The exact underlying future is missing or incompatible with the option.");
        if (value.ReviewState == ReferenceReviewState.Reviewed
            && await new ReferenceVersionStore(this).GetAsync(underlying.ContractId, underlying.MappingVersion!) is null)
            throw new InvalidOperationException("The underlying future's reviewed version has not completed publication.");
        return await new ReferenceVersionStore(this).StageAsync(value);
    }
    Task CommitReferenceAsync(ReferenceVersionStore.Pending? value) =>
        value is null ? Task.CompletedTask : new ReferenceVersionStore(this).CommitAsync(value);
}
