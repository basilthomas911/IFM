using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Historical document decoding only. Current producers write concrete MessagePack values.</summary>
public static class ParameterHistoryReader
{
    public static ParameterSetVersion ReadVersion(this IParameterSetFact fact) 
    {
        ArgumentNullException.ThrowIfNull(fact);
        return AsTyped(fact.Version ??
        JsonSerializer.Deserialize<ParameterSetVersion>(fact.VersionJson) ?? throw new InvalidDataException("Missing parameter version fact"));
    }
    public static ParameterSetVersion AsTyped(this ParameterSetVersion value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if(value.Parameters is not null)return value;
        if(!ParameterSchemaRegistry.Default.CanEditLosslessly(value.Reference.ComponentCode,value.SchemaVersion,value.PayloadJson))
            throw new InvalidDataException("PARAM.LEGACY_FIELDS_UNSUPPORTED");
        var parameters=ReadValues(value.Reference.ComponentCode,value.PayloadJson);
        if(parameters.SetId!=value.Reference.SetId||parameters.Version!=value.Reference.Version||parameters.SchemaVersion!=value.SchemaVersion)
            throw new InvalidDataException("PARAM.LEGACY_IDENTITY_MISMATCH");
        return value with{Parameters=parameters,PayloadJson=string.Empty,ReferenceCodec="sorted-json-v1"};
    }
    public static ParameterAuditEntry? ReadAudit(this IParameterSetFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return UpgradeAudit(fact.Audit ??
        (string.IsNullOrEmpty(fact.AuditJson)?null:JsonSerializer.Deserialize<ParameterAuditEntry>(fact.AuditJson)));
    }
    public static ParameterAssignmentRevision ReadAssignment(this ParameterAssignmentChangedEvent fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var value=fact.Assignment??JsonSerializer.Deserialize<ParameterAssignmentRevision>(fact.AssignmentJson)??throw new InvalidDataException("Missing assignment fact");
        return value with{Scope=UpgradeScope(value.Scope)};
    }
    public static ParameterAuditEntry? ReadAudit(this ParameterAssignmentChangedEvent fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return UpgradeAudit(fact.Audit??
        (string.IsNullOrEmpty(fact.AuditJson)?null:JsonSerializer.Deserialize<ParameterAuditEntry>(fact.AuditJson)));
    }
    public static ParameterStartupRun? ReadRun(this ParameterStartupChangedEvent fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var value=fact.Run??(string.IsNullOrEmpty(fact.RunJson)?null:JsonSerializer.Deserialize<ParameterStartupRun>(fact.RunJson));
        return value is null?null:value with{Scopes=value.Scopes.Select(x=>x with{Scope=UpgradeScope(x.Scope)}).ToArray(),Versions=value.Versions.Select(AsTyped).ToArray()};
    }
    public static ParameterSignalStartupReport? ReadReport(this ParameterStartupChangedEvent fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Report??
        (string.IsNullOrEmpty(fact.ReportJson)?null:JsonSerializer.Deserialize<ParameterSignalStartupReport>(fact.ReportJson));
    }
    public static ParameterAssignmentScope UpgradeScope(ParameterAssignmentScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if(string.IsNullOrEmpty(scope.ScopeJson))return scope;
        using var document=JsonDocument.Parse(scope.ScopeJson);var root=document.RootElement;
        if(root.EnumerateObject().Count()!=1)throw new InvalidDataException("Unsupported historical scope fields");
        if(root.TryGetProperty("TargetHorizon",out var horizon))
            return scope with{ScopeJson=string.Empty,TargetHorizon=Enum.Parse<TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType>(horizon.GetString()!),Codec="sorted-json-v1"};
        if(root.TryGetProperty("Strategy",out var strategy))return scope with{ScopeJson=string.Empty,Strategy=strategy.GetString(),Codec="sorted-json-v1"};
        throw new InvalidDataException("Unsupported historical scope");
    }
    static ParameterAuditEntry? UpgradeAudit(ParameterAuditEntry? audit)
    {
        if(audit is null)return null;
        ParameterAuditValue? ReadAuditValue(string json)
        {
            if(string.IsNullOrEmpty(json)||json=="null")return null;
            using var document=JsonDocument.Parse(json);
            if(document.RootElement.TryGetProperty("AssignmentId",out _))return new(Assignment:JsonSerializer.Deserialize<ParameterAssignmentRevision>(json));
            return JsonSerializer.Deserialize<ParameterAuditValue>(json);
        }
        ParameterAuditValue? Normalize(ParameterAuditValue? value)=>value?.Assignment is {} assignment?value with{Assignment=assignment with{Scope=UpgradeScope(assignment.Scope)}}:value;
        return audit with{Before=Normalize(audit.Before??ReadAuditValue(audit.BeforeJson)),After=Normalize(audit.After??ReadAuditValue(audit.AfterJson)),BeforeJson=string.Empty,AfterJson=string.Empty};
    }
    public static ParameterSetValue Values(this ParameterSetVersion version) 
    {
        ArgumentNullException.ThrowIfNull(version);
        return version.Parameters ?? ReadValues(version.Reference.ComponentCode,version.PayloadJson);
    }
    public static ParameterSetValue ReadValues(string component,string historicalJson) => component switch
    {
        ParameterSchemaRegistry.RegimeComponent=>ParameterSetValue.From(Read<RegimeDiscoveryParameterSet>(historicalJson)),
        ParameterSchemaRegistry.FuturesItiSignalComponent=>ParameterSetValue.From(Read<FuturesItiSignalParameterSet>(historicalJson)),
        ParameterSchemaRegistry.OptionVolatilitySeriesComponent=>ParameterSetValue.From(Read<OptionVolatilitySeriesParameterSet>(historicalJson)),
        ParameterSchemaRegistry.OptionVolatilityConsumerRulesComponent=>ParameterSetValue.From(Read<OptionVolatilityConsumerRulesParameterSet>(historicalJson)),
        ParameterSchemaRegistry.OptionVolatilityRetentionComponent=>ParameterSetValue.From(Read<OptionVolatilityRetentionParameterSet>(historicalJson)),
        ParameterSchemaRegistry.IronCondorMarketSelectionComponent=>ParameterSetValue.From(Read<IronCondorMarketSelectionParameterSet>(historicalJson)),
        ParameterSchemaRegistry.VerticalSpreadMarketSelectionComponent=>ParameterSetValue.From(Read<VerticalSpreadMarketSelectionParameterSet>(historicalJson)),
        ParameterSchemaRegistry.StrategyOptionChainCacheComponent=>ParameterSetValue.From(Read<StrategyOptionChainParameterSet>(historicalJson)),
        _=>throw new InvalidDataException("PARAM.COMPONENT_UNSUPPORTED")
    };
    static T Read<T>(string json)=>JsonSerializer.Deserialize<T>(json)??throw new InvalidDataException("Missing historical parameter value");
}
