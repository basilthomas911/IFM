using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesItiSignal;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject]
public sealed record ParameterSchemaDefinition(
    [property: Key(0)] string ComponentCode,
    [property: Key(1)] int Version,
    [property: Key(2)] string Codec,
    [property: Key(3)] string JsonSchema,
    [property: Key(4)] string SchemaSha256);





