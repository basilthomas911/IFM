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
        Assert.Equal(command.FuturesAnalyticsHistoricalDataLoaderParameters.StartDate, result.FuturesAnalyticsHistoricalDataLoaderParameters.StartDate);
        Assert.Equal(command.FuturesAnalyticsHistoricalDataLoaderParameters.EndDate, result.FuturesAnalyticsHistoricalDataLoaderParameters.EndDate);
        Assert.Equal(command.FuturesAnalyticsHistoricalDataLoaderParameters.MaximumCostUsd, result.FuturesAnalyticsHistoricalDataLoaderParameters.MaximumCostUsd);
        Assert.Equal(command.FuturesAnalyticsHistoricalDataLoaderParameters.MaximumBytes, result.FuturesAnalyticsHistoricalDataLoaderParameters.MaximumBytes);
        Assert.Equal(command.FuturesAnalyticsHistoricalDataLoaderParameters.NormalizationVersion, result.FuturesAnalyticsHistoricalDataLoaderParameters.NormalizationVersion);
        Assert.Equal(command.FuturesAnalyticsHistoricalDataLoaderParameters.CalculationConfigurationVersion,
            result.FuturesAnalyticsHistoricalDataLoaderParameters.CalculationConfigurationVersion);
        Assert.Equal(command.FuturesAnalyticsHistoricalDataLoaderParameters.RequestedBy, result.FuturesAnalyticsHistoricalDataLoaderParameters.RequestedBy);
        Assert.Equal(command.FuturesAnalyticsHistoricalDataLoaderParameters.SignalFamilies, result.FuturesAnalyticsHistoricalDataLoaderParameters.SignalFamilies);
        Assert.Equal(command.FuturesAnalyticsHistoricalDataLoaderParameters.Series, result.FuturesAnalyticsHistoricalDataLoaderParameters.Series);
        Assert.DoesNotContain("Databento", result.FuturesAnalyticsHistoricalDataLoaderParameters.GetType().AssemblyQualifiedName!,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Accepts one Requested transition and rejects a duplicate attempt in reconstructed state.</summary>
    [Fact]
    public void HistoricalDataLoadState_AcceptsRequestedEventExactlyOnce()
    {
        var state = new FuturesAnalyticsHistoricalDataLoaderCommandState();
        var command = CreateCommand();
        var earliestExpectedReceivedOn = DateTime.UtcNow;

        var first = command.Execute(state);
        var second = command.Execute(state);

        Assert.IsType<ServiceOk<GuidResult>>(first);
        Assert.IsType<ServiceFailed<GuidResult>>(second);
        Assert.True(state.IsRequested);
        Assert.Equal(command.FuturesAnalyticsHistoricalDataLoaderParameters, state.FuturesAnalyticsHistoricalDataLoaderParameters);
        var requested = Assert.IsType<FuturesAnalyticsHistoricalDataLoaderRequestedEvent>(
            Assert.Single(state.Events));
        Assert.InRange(requested.ReceivedOn, earliestExpectedReceivedOn, DateTime.UtcNow);
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
            FuturesAnalyticsHistoricalDataLoaderParameters = new()
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
