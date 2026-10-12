using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

[MessagePackObject]
public sealed record TradeStrategyFamilyReference(
    [property: Key(0)] int TradeStrategyFamilyId,
    [property: Key(1)] long DefinitionVersion)
{
    // Keys 0/1 are retained solely for legacy replay. New writes use the exact ConfigurationDb deployment.
    [Key(2), System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public StrategyCatalog.CatalogKey? CatalogDeployment { get; init; }
    [IgnoreMember]
    public bool IsValid => CatalogDeployment is { Kind: StrategyCatalog.StrategyCatalogKind.Deployment, Version: > 0 } key
        ? key.Id != Guid.Empty && TradeStrategyFamilyId == 0 && DefinitionVersion == 0
        : CatalogDeployment is null && TradeStrategyFamilyId > 0 && DefinitionVersion > 0;
    public static TradeStrategyFamilyReference From(TradeStrategyFamilyReadModel row) 
    {
        ArgumentNullException.ThrowIfNull(row);
        return new(row.TradeStrategyFamilyId, row.DefinitionVersion);
    }
    public static TradeStrategyFamilyReference From(StrategyCatalog.StrategyDeploymentChoice row) 
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Reference;
    }
}
