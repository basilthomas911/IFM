using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Read-only v2 storage shape, retained solely for identity-preserving catalog migration.</summary>
public sealed record LegacyTradeStrategyFamily(int TradeStrategyFamilyId, long DefinitionVersion,
    string SystemKey, string Name, TradeStrategyFamilyState State, DateTime CreatedOnUtc, string CreatedBy);
