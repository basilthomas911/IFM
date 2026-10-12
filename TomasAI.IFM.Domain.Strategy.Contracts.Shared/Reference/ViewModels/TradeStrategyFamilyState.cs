// LEGACY: retained for migration/replay and UI comparison only. Active authoring uses ConfigurationDb.
// Removal criteria: Domain.Reference/Docs/Strategy-Catalog-Legacy-Retirement.md.
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

public enum TradeStrategyFamilyState { Unknown = 0, Active = 1, Retired = 2 }
