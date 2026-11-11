namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>A committed instrument definition and its stable key for continuation.</summary>
public sealed record InstrumentDefinitionSelectionRow(string Key, InstrumentDefinitionSelection Value);
