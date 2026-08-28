using FluentAssertions;
using System;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

/// <summary>Verifies the evolved Wilder ATR projection against the development ScyllaDB schema.</summary>
public sealed class FuturesAtrSignalDbTests(MarketDataFixture testFixture) : IClassFixture<MarketDataFixture>
{
    /// <summary>Persists and reloads the complete Wilder values and source-observation lineage.</summary>
    [Fact]
    public async Task FuturesAtrSignal_WilderProjection_RoundTripsAllFields()
    {
        var valueDate = new DateOnly(2026, 8, 26);
        var calculatedAt = new DateTimeOffset(valueDate, new TimeOnly(14, 30), TimeSpan.Zero);
        var observationId = new FuturesTradeSessionBarId(Guid.NewGuid());
        var expected = new FuturesAtrSignalReadModel(
            contractId: $"ATRTEST{Guid.NewGuid():N}"[..18],
            valueDate,
            TimeFrameType.FifteenMinutes,
            periodLength: 14,
            timestamp: TimeOnly.FromDateTime(calculatedAt.UtcDateTime),
            futuresPrice: 6412.25m,
            atrValue: 12.75,
            trueRange: 15.25,
            atr: FuturesTrendDirectionType.UpTrending,
            atrStrength: FuturesTrendDirectionStrengthType.Medium)
        {
            PreviousAtrValue = 12.5,
            AtrBaseline = 11.75,
            AtrRatio = 12.75 / 11.75,
            IsWarm = true,
            Metadata = new MarketAnalyticsSignalMetadata
            {
                SignalKey = new(
                    MarketSeriesIdentity.ForContract("ESZ26"),
                    MarketAnalyticsSignalKind.Atr,
                    TimeFrameType.FifteenMinutes,
                    "atr-14-wilder-v1"),
                ContractId = "ESZ26",
                ValueDate = valueDate,
                ObservationId = observationId,
                MarketDataAsOfUtc = calculatedAt,
                CalculatedAtUtc = calculatedAt,
                SourceSequence = 789,
                SchemaVersion = 2,
                CalculationVersion = "atr-wilder-ohlc-v1",
                CalculationMethod = MarketSignalCalculationMethod.ClosedObservation,
                IsValid = true
            }
        };
        expected = expected with
        {
            Metadata = expected.Metadata! with
            {
                SignalKey = expected.Metadata.SignalKey with
                {
                    MarketSeriesIdentity = MarketSeriesIdentity.ForContract(expected.ContractId)
                },
                ContractId = expected.ContractId
            }
        };

        await testFixture.DevDatabase.DeleteFuturesAtrSignalAsync(
            expected.ContractId,
            expected.ValueDate,
            expected.TimePeriod,
            expected.PeriodLength);
        await testFixture.DevDatabase.DbWriter.InsertFuturesAtrSignalAsync(expected);

        var actual = await testFixture.DevDatabase.GetLastFuturesAtrSignalAsync(
            expected.ContractId,
            expected.ValueDate,
            expected.TimePeriod,
            expected.PeriodLength);

        actual.Should().NotBeNull();
        actual!.Should().BeEquivalentTo(expected, options => options
            .Excluding(x => x.Metadata!.CalculatedAtUtc)
            .Excluding(x => x.Metadata!.ValidationIssues));
    }
}
