using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record AssessmentSourceBinding(
    [property: Key(0)] string SourceId,
    [property: Key(1)] bool Required,
    [property: Key(2)] int MaximumAgeSeconds);
