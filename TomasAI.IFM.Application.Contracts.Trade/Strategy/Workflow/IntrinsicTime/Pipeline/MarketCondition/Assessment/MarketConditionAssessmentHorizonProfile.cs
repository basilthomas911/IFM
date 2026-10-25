using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

[MessagePackObject]
public sealed record MarketConditionAssessmentHorizonProfile
{
    [Key(0)] public TimeFrameType Horizon { get; init; }
    [Key(1)] public Guid RegimeProfileId { get; init; }
    [Key(2)] public int RegimeProfileVersion { get; init; }
    [Key(3)] public int RegimeMaximumAgeSeconds { get; init; } = 120;
    [Key(4)] public int ResultLifetimeSeconds { get; init; } = 30;
    [Key(5)] public string SummaryTemplateVersion { get; init; } = "mc-assessment-v1";
}
