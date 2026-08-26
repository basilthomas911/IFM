using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.HistoricalDataLoader;

/// <summary>Verifies the MDSI-3 data load message and event-sourced state boundary.</summary>
public sealed class HistoricalDataLoaderContractTests
{
    /// <summary>Round-trips the complete parameter-only command payload without provider records.</summary>
    [Fact]
    public void HistoricalDataLoadCommand_RoundTripsProviderNeutralParameters()
    {
        var command = CreateCommand();

        var result = MessagePackSerializer.Deserialize<LoadFuturesAnalyticsHistoricalDataCommand>(
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
    public void HistoricalDataLoadState_AcceptsRequestedEventExactlyOnce()
    {
        var state = new FuturesAnalyticsHistoricalDataLoaderCommandState();
        var command = CreateCommand();

        var first = command.Execute(state);
        var second = command.Execute(state);

        Assert.IsType<ServiceOk<GuidResult>>(first);
        Assert.IsType<ServiceFailed<GuidResult>>(second);
        Assert.True(state.IsRequested);
        Assert.Equal(command.Parameters, state.Parameters);
    }

    static LoadFuturesAnalyticsHistoricalDataCommand CreateCommand()
    {
        var attemptId = Guid.NewGuid();
        var entityId = new FuturesAnalyticsHistoricalDataLoaderEntityId(attemptId);
        return new()
        {
            CommandId = attemptId,
            EntityId = entityId,
            Subject = new ActorSubject(
                ActorType.Command,
                LoadFuturesAnalyticsHistoricalDataCommand.Actor,
                LoadFuturesAnalyticsHistoricalDataCommand.Verb,
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
