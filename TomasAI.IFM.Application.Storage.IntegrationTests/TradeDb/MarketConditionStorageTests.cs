using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

public sealed class MarketConditionStorageTests(TradeDbFixture fixture) : IClassFixture<TradeDbFixture>
{
    [Fact]
    public async Task Exact_latest_and_bounded_history_are_idempotent_and_hash_preserving()
    {
        var fundId = Math.Abs(Guid.NewGuid().GetHashCode()) + 1;
        var first = Model(fundId, DateTime.UtcNow.AddSeconds(-2));
        var second = Model(fundId, first.EvaluatedAtUtc.AddSeconds(1));

        await fixture.TradeDb.UpsertMarketConditionAsync(first);
        await fixture.TradeDb.UpsertMarketConditionAsync(first);
        await fixture.TradeDb.UpsertMarketConditionAsync(second);

        var exact = await fixture.TradeDb.GetMarketConditionAsync(first.WorkflowId);
        var history = await fixture.TradeDb.GetMarketConditionHistoryAsync(
            fundId, "ES", TimeFrameType.Daily, DateTime.MaxValue, 10);

        exact.Should().BeEquivalentTo(first, options => options
            .Excluding(x => x.ResultPayload)
            .Excluding(x => x.EvidencePayload)
            .Excluding(x => x.ConflictingEvidencePayload)
            .Excluding(x => x.BlockingReasonsPayload)
            .Excluding(x => x.ReasonsPayload));
        exact!.ResultPayload.ToArray().Should().Equal(first.ResultPayload.ToArray());
        exact.EvidencePayload.ToArray().Should().Equal(first.EvidencePayload.ToArray());
        exact.ConflictingEvidencePayload.ToArray().Should().Equal(first.ConflictingEvidencePayload.ToArray());
        exact.BlockingReasonsPayload.ToArray().Should().Equal(first.BlockingReasonsPayload.ToArray());
        exact.ReasonsPayload.ToArray().Should().Equal(first.ReasonsPayload.ToArray());
        history.Where(x => x.WorkflowId == first.WorkflowId).Should().ContainSingle();
        history.Where(x => x.WorkflowId == second.WorkflowId).Should().ContainSingle();
        history.Take(2).Select(x => x.WorkflowId).Should().Equal(second.WorkflowId, first.WorkflowId);
        MessagePackSerializer.Deserialize<string[]>(exact!.ReasonsPayload).Should().Equal("MC.DATA.FIT");
        exact.ResultPayloadSha256.Should().Be(first.ResultPayloadSha256);
    }

    static MarketConditionReadModel Model(int fundId, DateTime evaluatedAtUtc)
    {
        evaluatedAtUtc = new DateTime(
            evaluatedAtUtc.Ticks - evaluatedAtUtc.Ticks % TimeSpan.TicksPerMillisecond,
            DateTimeKind.Utc);
        var workflowId = new StrategyWorkflowId(Guid.CreateVersion7());
        return new MarketConditionReadModel
        {
            WorkflowId = workflowId,
            WorkflowEntityId = $"IntrinsicTimeStrategy.ES.{workflowId}",
            InputWorkflowRevision = 2,
            CommandId = Guid.NewGuid(),
            SourceEventId = Guid.NewGuid(),
            FundId = fundId,
            InstrumentRoot = "ES",
            TargetHorizon = TimeFrameType.Daily,
            ParameterSetId = Guid.NewGuid(),
            ParameterSetVersion = 1,
            ParameterPayloadSha256 = new string('A', 64),
            SnapshotId = Guid.NewGuid(),
            SnapshotSha256 = new string('B', 64),
            Tradeability = MarketTradeability.Tradeable,
            ConditionType = MarketConditionType.Directional,
            Direction = MarketConditionDirection.Bullish,
            Phase = MarketConditionPhase.Confirmed,
            Strength = 80m,
            Confidence = 0.8m,
            PrimaryReasonCode = MarketConditionReasonCodes.Directional,
            ResultPayload = new byte[] { 1, 2, 3 },
            ResultPayloadSha256 = new string('C', 64),
            EvaluatedAtUtc = evaluatedAtUtc,
            ValidUntilUtc = evaluatedAtUtc.AddSeconds(30),
            MarketDataAsOfUtc = evaluatedAtUtc,
            CompletedAtUtc = evaluatedAtUtc,
            UpdatedAtUtc = evaluatedAtUtc,
            VolatilityBehavior = MarketConditionVolatilityBehavior.Stable,
            LiquidityQuality = MarketConditionLiquidityQuality.Healthy,
            DataQuality = MarketConditionDataQuality.Healthy,
            UpstreamAlignment = MarketConditionUpstreamAlignment.Aligned,
            EvidencePayload = MessagePackSerializer.Serialize(Array.Empty<MarketConditionEvidenceItem>()),
            ConflictingEvidencePayload = MessagePackSerializer.Serialize(Array.Empty<MarketConditionEvidenceItem>()),
            BlockingReasonsPayload = MessagePackSerializer.Serialize(Array.Empty<MarketConditionBlockingReason>()),
            ReasonsPayload = MessagePackSerializer.Serialize(new[] { "MC.DATA.FIT" }),
            SummaryText = "storage integration result"
        };
    }
}
