using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Maps a cached provider definition using the same identity fences as the WinForms selector. Imports remain drafts.</summary>
public static class ProviderReferenceMapping
{
    public static FuturesContractV3ReadModel Future(InstrumentDefinitionSelection definition, string zone, DateTimeOffset at, FuturesContractV3ReadModel? original = null)
    {
        var draft = InstrumentDefinitionImport.Future(definition, zone, at);
        if (original is null) return draft;
        if (original.ReviewState == ReferenceReviewState.Reviewed) throw new ArgumentException("Review an existing qualified contract in the convention editor; draft import cannot replace its qualification.");
        ProviderIdentity(definition, original.SchemaVersion, original.Dataset, original.PublisherId, original.InstrumentId);
        if (original.Symbol != draft.Symbol || original.LastTradeDate != draft.LastTradeDate)
            throw new ArgumentException("Mapping changes the contract identity. Add a new contract instead.");
        return ReferenceConventionEditor.From(original).Apply(draft) with { ReviewState = ReferenceReviewState.Draft, ExchangeTimeZoneId = zone, ContractId = original.ContractId, OnTheRun = original.OnTheRun, Rollover = original.Rollover };
    }
    public static FuturesOptionContractReadModel Option(InstrumentDefinitionSelection definition, FuturesContractV3ReadModel underlying, string zone, DateTimeOffset at, FuturesOptionContractReadModel? original = null)
    {
        var draft = InstrumentDefinitionImport.Option(definition, underlying, zone, at);
        if (original is null) return draft;
        if (original.ReviewState == ReferenceReviewState.Reviewed) throw new ArgumentException("Review an existing qualified contract in the convention editor; draft import cannot replace its qualification.");
        ProviderIdentity(definition, original.SchemaVersion, original.Dataset, original.PublisherId, original.InstrumentId);
        if (original.Symbol != draft.Symbol || original.ContractMonth != draft.ContractMonth || original.GetExactStrikePrice() != draft.GetExactStrikePrice() || original.OptionType != draft.OptionType)
            throw new ArgumentException("Mapping changes the option identity. Add a new contract instead.");
        return ReferenceConventionEditor.From(original).Apply(draft) with { ReviewState = ReferenceReviewState.Draft, ExchangeTimeZoneId = zone, ContractId = original.ContractId };
    }
    static void ProviderIdentity(InstrumentDefinitionSelection definition, int schema, string? dataset, ushort? publisher, uint? instrument)
    {
        if (schema > 0 && (dataset != definition.Dataset || publisher != definition.PublisherId || instrument != definition.InstrumentId))
            throw new ArgumentException("Mapping cannot replace an existing provider instrument. Add a new contract instead.");
    }
}
