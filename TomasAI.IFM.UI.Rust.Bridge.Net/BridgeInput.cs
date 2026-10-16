using System.Collections;
using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.UI.Net.ViewModels.Reference.ParameterSets;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Explicit portable editor input schemas. Constructors/setters are statically bound.</summary>
static class BridgeInput
{
    public static T Decode<T>(ReadOnlyMemory<byte> bytes)
    {
        object result = typeof(T) == typeof(ParameterField[]) ? Read(bytes, m => new ParameterField(Text(m, "Path"), Text(m, "Value"), EnumValue<JsonValueKind>(m, "Kind"))) :
            typeof(T) == typeof(RegimeDiscoveryTimeFrameConfiguration[]) ? Read(bytes, m => new RegimeDiscoveryTimeFrameConfiguration { TimeFrame = EnumValue<TimeFrameType>(m, "TimeFrame"), Weight = Number<decimal>(m, "Weight"), IsRequired = Flag(m, "IsRequired"), MaximumAgeSeconds = Number<int>(m, "MaximumAgeSeconds") }) :
            typeof(T) == typeof(RegimeDiscoverySignalMetricConfiguration[]) ? Read(bytes, m => new RegimeDiscoverySignalMetricConfiguration { RequirementId = Id(m), Metric = EnumValue<SignalMetricsType>(m, "Metric"), TimeFrame = EnumValue<TimeFrameType>(m, "TimeFrame"), PeriodLength = Number<int>(m, "PeriodLength"), Enabled = Flag(m, "Enabled", true), MaximumAgeSeconds = Number<int>(m, "MaximumAgeSeconds"), PrepareAtStartup = Flag(m, "PrepareAtStartup", true), Monitor = Flag(m, "Monitor", true) }) :
            typeof(T) == typeof(RegimeDiscoveryObservationMetricConfiguration[]) ? Read(bytes, m => new RegimeDiscoveryObservationMetricConfiguration { RequirementId = Id(m), Metric = EnumValue<ObservationMetricsType>(m, "Metric"), Enabled = Flag(m, "Enabled", true), MaximumAgeSeconds = Number<int>(m, "MaximumAgeSeconds"), PrepareAtStartup = Flag(m, "PrepareAtStartup", true), Monitor = Flag(m, "Monitor", true) }) :
            throw new NotSupportedException($"No editor input schema for {typeof(T)}");
        return (T)result;
    }
    static T[] Read<T>(ReadOnlyMemory<byte> bytes, Func<IDictionary, T> create)
    {
        var value = BridgeWire.Deserialize<object>(bytes); if (value is null) return [];
        if (value is not object[] rows) throw new ArgumentException("Editor document must be an array");
        var result = new T[rows.Length]; for (var i = 0; i < rows.Length; i++) result[i] = create(rows[i] as IDictionary ?? throw new ArgumentException("Editor row must be a map")); return result;
    }
    static object? Get(IDictionary map, string name) => map.Contains(name) ? map[name] : map[char.ToLowerInvariant(name[0]) + name[1..]];
    static string Text(IDictionary map, string name) => Convert.ToString(Get(map, name), CultureInfo.InvariantCulture) ?? "";
    static T Number<T>(IDictionary map, string name) => (T)Convert.ChangeType(Get(map, name) ?? 0, typeof(T), CultureInfo.InvariantCulture);
    static bool Flag(IDictionary map, string name, bool fallback = false) => Get(map, name) is { } value ? Convert.ToBoolean(value, CultureInfo.InvariantCulture) : fallback;
    static Guid Id(IDictionary map) => Get(map, "RequirementId") is { } value ? Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!) : Guid.Empty;
    static T EnumValue<T>(IDictionary map, string name) where T : struct, Enum => Enum.Parse<T>(Text(map, name).Length == 0 ? "0" : Text(map, name));
}
