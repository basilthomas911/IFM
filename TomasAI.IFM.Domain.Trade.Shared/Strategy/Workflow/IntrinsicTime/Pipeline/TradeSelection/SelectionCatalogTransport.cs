using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

public static class SelectionCatalogTransport
{
    public static SelectionCapability From(CatalogCapability x) => new() { Role=x.Role, Code=x.Code, Version=x.Version };
    public static CatalogCapability ToSource(SelectionCapability x) => new(x.Role, x.Code, x.Version);
    public static SelectionExpiryGroup From(CatalogExpiryGroup x) => new() { Key=x.Key, AfterGroup=x.AfterGroup };
    public static CatalogExpiryGroup ToSource(SelectionExpiryGroup x) => new(x.Key, x.AfterGroup);
    public static SelectionLeg From(CatalogLeg x) => new() { Key=x.Key, InstrumentClass=x.InstrumentClass, Side=x.Side, OptionRight=x.OptionRight, Ratio=x.Ratio, ExpiryGroup=x.ExpiryGroup };
    public static CatalogLeg ToSource(SelectionLeg x) => new(x.Key, x.InstrumentClass, x.Side, x.OptionRight, x.Ratio, x.ExpiryGroup);
    public static SelectionVariantLeg From(CatalogVariantLeg x) => new() { LegKey=x.LegKey, Side=x.Side, Ratio=x.Ratio };
    public static CatalogVariantLeg ToSource(SelectionVariantLeg x) => new(x.LegKey, x.Side, x.Ratio);
    public static SelectionProduct From(CatalogProduct x) => new() { ProductId=x.ProductId, Symbol=x.Symbol, Exchange=x.Exchange, Currency=x.Currency };
    public static CatalogProduct ToSource(SelectionProduct x) => new(x.ProductId, x.Symbol, x.Exchange, x.Currency);
    public static SelectionPipelineParameter From(CatalogPipelineParameter x) => new() { Role=x.Role, Kind=x.Kind, Id=x.Id, Version=x.Version, Hash=x.Hash };
    public static CatalogPipelineParameter ToSource(SelectionPipelineParameter x) => new(x.Role, x.Kind, x.Id, x.Version, x.Hash);
    public static SelectionParameterBinding From(CatalogParameterBinding x) => new() { Role=x.Role, ParameterSet=x.ParameterSet };
    public static CatalogParameterBinding ToSource(SelectionParameterBinding x) => new(x.Role, x.ParameterSet);
    public static SelectionLegacyFamily From(CatalogLegacyFamily x) => new() { Id=x.Id, Version=x.Version };
    public static CatalogLegacyFamily ToSource(SelectionLegacyFamily x) => new(x.Id, x.Version);
    public static SelectionCatalogDefinitionSnapshot From(StoredStrategyCatalogDefinition row)
    {
        var d=StrategyCatalogValidation.Freeze(row.Definition);
        if(StrategyCatalogValidation.ContentHash(d)!=row.ContentHash) throw new ArgumentException("TS.CONTRACT.HASH: Catalog node hash mismatch.");
        return new()
        {
            SchemaVersion=1, DefinitionSchemaVersion=d.SchemaVersion, SettingsJson=StrategyCatalogValidation.CanonicalJson(d.Settings),
            Key=d.Key,
            Code=d.Code,
            Name=d.Name,
            Description=d.Description,
            Parent=d.Parent,
            Horizon=d.Horizon,
            Side=d.Side,
            Bias=d.Bias,
            PremiumMode=d.PremiumMode,
            Families=d.Families,
            Structures=d.Structures,
            Variants=d.Variants,
            Capabilities=d.Capabilities.Select(From).ToArray(),
            ExpiryGroups=d.ExpiryGroups.Select(From).ToArray(),
            Legs=d.Legs.Select(From).ToArray(),
            VariantLegs=d.VariantLegs.Select(From).ToArray(),
            Products=d.Products.Select(From).ToArray(),
            PipelineParameters=d.PipelineParameters.Select(From).ToArray(),
            Parameters=d.Parameters.Select(From).ToArray(),
            LegacyFamilies=d.LegacyFamilies.Select(From).ToArray(),
            ContentHash=row.ContentHash,Status=row.Status,CreatedUtc=row.CreatedUtc,CreatedBy=row.CreatedBy,
            EffectiveFromUtc=row.EffectiveFromUtc,PublishedBy=row.PublishedBy,RetiredAtUtc=row.RetiredAtUtc,RetiredBy=row.RetiredBy
        };
    }
    public static StoredStrategyCatalogDefinition ToSource(SelectionCatalogDefinitionSnapshot row)
    {
        using var settings=JsonDocument.Parse(row.SettingsJson);
        return new(new StrategyCatalogDefinition
        {
            SchemaVersion=row.DefinitionSchemaVersion,Settings=settings.RootElement.Clone(),
            Key=row.Key,
            Code=row.Code,
            Name=row.Name,
            Description=row.Description,
            Parent=row.Parent,
            Horizon=row.Horizon,
            Side=row.Side,
            Bias=row.Bias,
            PremiumMode=row.PremiumMode,
            Families=row.Families,
            Structures=row.Structures,
            Variants=row.Variants,
            Capabilities=row.Capabilities.Select(ToSource).ToArray(),
            ExpiryGroups=row.ExpiryGroups.Select(ToSource).ToArray(),
            Legs=row.Legs.Select(ToSource).ToArray(),
            VariantLegs=row.VariantLegs.Select(ToSource).ToArray(),
            Products=row.Products.Select(ToSource).ToArray(),
            PipelineParameters=row.PipelineParameters.Select(ToSource).ToArray(),
            Parameters=row.Parameters.Select(ToSource).ToArray(),
            LegacyFamilies=row.LegacyFamilies.Select(ToSource).ToArray(),
        },row.ContentHash,row.Status,row.CreatedUtc,row.CreatedBy,row.EffectiveFromUtc,row.PublishedBy,row.RetiredAtUtc,row.RetiredBy);
    }
    public static string GraphHash(CatalogKey deployment,IEnumerable<StoredStrategyCatalogDefinition> definitions)
    {
        var ordered=definitions.OrderBy(x=>x.Definition.Key.Kind).ThenBy(x=>x.Definition.Key.Id).ThenBy(x=>x.Definition.Key.Version).ToArray();
        return StrategyCatalogValidation.Sha(StrategyCatalogValidation.CanonicalJson(JsonSerializer.SerializeToElement(new
        {
            SchemaVersion=1,Deployment=deployment,Definitions=ordered.Select(x=>new { x.Definition.Key,x.ContentHash }).ToArray()
        },StrategyCatalogValidation.JsonOptions)));
    }
}
