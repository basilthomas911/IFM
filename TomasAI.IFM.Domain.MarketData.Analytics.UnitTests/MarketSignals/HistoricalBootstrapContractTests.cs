using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketSignals;

/// <summary>Verifies the MDSI-3 bootstrap message and event-sourced state boundary.</summary>
public sealed class HistoricalBootstrapContractTests
{
    /// <summary>Round-trips the complete parameter-only command payload without provider records.</summary>
    [Fact]
    public void BootstrapCommand_RoundTripsProviderNeutralParameters()
    {
        var command = CreateCommand();

        var result = MessagePackSerializer.Deserialize<BootstrapFuturesAnalyticsHistoryCommand>(
            MessagePackSerializer.Serialize(command));

        Assert.Equal(command.CommandId, result.CommandId);
        Assert.Equal(command.EntityId, result.EntityId);
        Assert.Equal(command.Parameters.StartDate, result.Parameters.StartDate);
        Assert.Equal(command.Parameters.EndDate, result.Parameters.EndDate);
        Assert.Equal(command.Parameters.MaximumCostUsd, result.Parameters.MaximumCostUsd);
        Assert.Equal(command.Parameters.MaximumBytes, result.Parameters.MaximumBytes);
        Assert.Equal(command.Parameters.NormalizationVersion, result.Parameters.NormalizationVersion);
        Assert.Equal(command.Parameters.CalculationConfigurationVersion,
            result.Parameters.CalculationConfigurationVersion);
        Assert.Equal(command.Parameters.RequestedBy, result.Parameters.RequestedBy);
        Assert.Equal(command.Parameters.SignalFamilies, result.Parameters.SignalFamilies);
        Assert.Equal(command.Parameters.Series, result.Parameters.Series);
        Assert.DoesNotContain("Databento", result.Parameters.GetType().AssemblyQualifiedName!,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Accepts one Requested transition and rejects a duplicate attempt in reconstructed state.</summary>
    [Fact]
    public void BootstrapState_AcceptsRequestedEventExactlyOnce()
    {
        var state = new FuturesAnalyticsHistoryBootstrapCommandState();
        var command = CreateCommand();

        var first = command.Execute(state);
        var second = command.Execute(state);

        Assert.IsType<ServiceOk<GuidResult>>(first);
        Assert.IsType<ServiceFailed<GuidResult>>(second);
        Assert.True(state.IsRequested);
        Assert.Equal(command.Parameters, state.Parameters);
    }

    static BootstrapFuturesAnalyticsHistoryCommand CreateCommand()
    {
        var attemptId = Guid.NewGuid();
        var entityId = new FuturesAnalyticsHistoryBootstrapEntityId(attemptId);
        return new()
        {
            CommandId = attemptId,
            EntityId = entityId,
            Subject = new ActorSubject(
                ActorType.Command,
                BootstrapFuturesAnalyticsHistoryCommand.Actor,
                BootstrapFuturesAnalyticsHistoryCommand.Verb,
                entityId.Format()),
            Parameters = new()
            {
                Series =
                [
                    new()
                    {
                        MarketSeriesIdentity = MarketSeriesIdentity.ForFuturesSeries(
                            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1)),
                        Schema = FuturesAnalyticsHistoricalSchema.OhlcvOneMinute
                    }
                ],
                StartDate = new(2025, 8, 25),
                EndDate = new(2026, 8, 25),
                SignalFamilies = ["Ema", "BollingerBand"],
                MaximumCostUsd = 25m,
                MaximumBytes = 1_000_000_000,
                NormalizationVersion = "normalization-v1",
                CalculationConfigurationVersion = "analytics-v1",
                RequestedBy = "unit-test"
            }
        };
    }
}
