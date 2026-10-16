using System.Collections;
using System.Reflection;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Builds detached concrete working copies without serializing parameter documents.</summary>
public static class TypedParameterDraftModel
{
    public static ParameterSetValue Preview(Guid setId, string component, TimeFrameType horizon,
        ParameterSetValue? source = null, bool rebuildIntervals = false)
    {
        if (setId == Guid.Empty) throw new ArgumentException("Parameter-set identity is required", nameof(setId));
        var descriptor = ParameterComponentModelRegistry.Get(component);
        if (source is not null && (source.ComponentCode != component || source.SetId != setId))
            throw new ArgumentException("Source parameter identity does not match the preview request", nameof(source));
        var value = source ?? descriptor.CreateTypedDraft(setId);
        if (component == ParameterSchemaRegistry.RegimeComponent)
        {
            if (horizon is not (TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly))
                throw new ArgumentOutOfRangeException(nameof(horizon));
            value = ParameterSetValue.From(source is null
                ? RegimeDiscoveryParameterModel.CreateExplicitSeed(setId, horizon)
                : RegimeDiscoveryParameterModel.UpgradeToExplicitSet(value.Get<RegimeDiscoveryParameterSet>(), rebuildIntervals));
        }
        else if (rebuildIntervals) throw new ArgumentException("Interval rebuilding is only supported for Regime Discovery", nameof(rebuildIntervals));
        if (!descriptor.Summary.SchemaVersions.Contains(value.SchemaVersion)) throw new ArgumentException("Unsupported parameter schema", nameof(source));
        return ParameterValueCodec.Snapshot(value);
    }

    public static ParameterValidationReport Validate(string component, int schemaVersion, ParameterSetValue? value)
    {
        if (value is null) return new(string.Empty,[new("PARAM.OBJECT_REQUIRED","Parameters","A typed parameter configuration is required")]);
        var required = ValidateShape(component,schemaVersion,value);
        var issues = required.Length == 0 ? ParameterComponentModelRegistry.Get(component).ValidateTyped(value, schemaVersion) : required;
        return new(ParameterValueCodec.Hash(value), issues);
    }

    public static ParameterValidationIssue[] ValidateShape(string component,int schemaVersion,ParameterSetValue value)
    {
        var required=RequiredValues(value);
        if(required.Length!=0) return required;
        if(value.ComponentCode!=component || value.SchemaVersion!=schemaVersion)
            return [new("PARAM.SCHEMA_MISMATCH","Parameters","The configuration does not match the selected component or schema")];
        if(!ParameterComponentModelRegistry.Get(component).Summary.SchemaVersions.Contains(schemaVersion))
            return [new("PARAM.SCHEMA_UNSUPPORTED","Parameters","Unsupported parameter schema")];
        return [];
    }

    static readonly NullabilityInfoContext Nullability = new();
    static readonly object NullabilityLock = new();
    static ParameterValidationIssue[] RequiredValues(ParameterSetValue value)
    {
        var issues = new List<ParameterValidationIssue>();
        void Visit(object? instance, Type type, string path, bool required)
        {
            if (instance is null)
            {
                if (required) issues.Add(new("PARAM.VALUE_REQUIRED", path, "A required parameter value is missing"));
                return;
            }
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeSpan)) return;
            if (instance is IEnumerable collection)
            {
                // Default immutable arrays are rejected by their component's business validator.
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Immutable.ImmutableArray<>) &&
                    (bool)type.GetProperty("IsDefault")!.GetValue(instance)!) return;
                var elementType = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
                var index = 0;
                foreach (var item in collection) Visit(item, elementType, path + "/" + index++, true);
                return;
            }
            foreach (var property in type.GetProperties().Where(p => p.GetCustomAttribute<KeyAttribute>() is not null))
            {
                bool isRequired;
                lock (NullabilityLock) isRequired = Nullability.Create(property).ReadState == NullabilityState.NotNull;
                Visit(property.GetValue(instance), property.PropertyType, path + "/" + property.Name, isRequired);
            }
        }
        var property = value.GetType().GetProperty("Value")!;
        Visit(property.GetValue(value),property.PropertyType,"Parameters",true);
        return issues.ToArray();
    }
}
