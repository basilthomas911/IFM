namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
public sealed record LiveUiStreamEvidence(string Symbol, DateTime? ReceivedBarUtc, DateTime? RenderedBarUtc,
    string ContractId = "");
