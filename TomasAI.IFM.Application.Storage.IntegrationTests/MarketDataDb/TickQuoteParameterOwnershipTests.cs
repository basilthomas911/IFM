using System;
using System.Linq;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class TickQuoteParameterOwnershipTests
{
    [Fact]
    public void Quote_parameters_cannot_be_rebound_after_udt_resolution()
    {
        var original = new object?[21];
        var binding = (IScyllaOwnedBindValues)new InsertTickQuoteData(original).Bind();

        Assert.Same(original, binding.TakeValues());
        Assert.Throws<InvalidOperationException>(() => binding.TakeValues());
    }
    [Fact]
    public void Raw_quote_parameters_transfer_the_array_once_and_retain_bind_lifetime()
    {
        var original = new object?[21];
        var encoded = new Storage.TradeTickQuoteDb.TickQuoteScyllaBindValue(
            new TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.FuturesTickQuoteDataSegment(
                [new(1, 2, 3, 4, 5, 5m, 6, 7, 8, 8m, 9, 10)], 1));
        var parameters = new Storage.TradeTickQuoteDb.InsertTickQuoteData(original, encoded);
        var copy = parameters;
        var binding = (IScyllaOwnedBindValues)parameters.Bind();
        Assert.Same(binding, copy.Bind());
        Assert.Same(encoded, binding.BindLifetime);
        Assert.Same(original, binding.TakeValues());
        Assert.Throws<InvalidOperationException>(() => binding.TakeValues());
        binding.BindLifetime!.Dispose();
        binding.BindLifetime.Dispose();
    }

    [Fact]
    public void Raw_quote_binding_matches_every_CQL_column_in_order()
    {
        var at = new DateTime(2026, 11, 10, 16, 17, 18, DateTimeKind.Utc);
        var date = DateOnly.FromDateTime(at);
        var quote = new FuturesTickQuoteDataInsertedEvent
        {
            AssetTypeId = AssetTypeId.Futures, TickDataId = new("ES20261218", date, 19, at), Dataset = "GLBX.MDP3",
            DefinitionDate = date.AddDays(-1), PublisherId = 23, InstrumentId = 24,
            Id = Guid.NewGuid(), EventId = 25, CommandId = Guid.NewGuid(), AggregateId = "fixture-aggregate",
            EventSource = "fixture-source", ReceivedOn = at.AddMilliseconds(123), EmissionReason = QuoteEmissionReason.BufferFull,
            QuoteCount = 1, QuoteData = new([new(1, 2, 3, 4, 5, 5m, 6, 7, 8, 8m, 9, 10)], 1)
        };
        var binder = (IScyllaOwnedBindValues)quote.CreateQuoteParameters().Bind();
        using var lifetime = binder.BindLifetime;
        var values = binder.TakeValues();
        var sql = TradeTickQuoteDbCql.InsertTickQuoteData;
        string[] expectedColumns = ["asset_type_id", "contract_id", "value_date", "aggregation_time", "sequence_id",
            "aggregation_timestamp_utc", "aggregation_timestamp_utc_ticks", "schema_version", "dataset", "definition_date",
            "publisher_id", "instrument_id", "actor_event_id", "actor_event_log_id", "command_id", "aggregate_id",
            "event_source", "received_on", "emission_reason", "quote_count", "quote_data"];
        Assert.Equal(expectedColumns, sql[(sql.IndexOf('(') + 1)..sql.IndexOf(')')].Split(',').Select(column => column.Trim()));
        Assert.Equal(sql.Count(character => character == '?'), values.Length);
        Assert.Equal(new object?[] { (sbyte)quote.AssetTypeId, "ES20261218", date, TimeOnly.FromDateTime(at), 19L, at, at.Ticks,
            (short)quote.SchemaVersion, "GLBX.MDP3", date.AddDays(-1), 23, 24L, quote.Id, 25L, quote.CommandId,
            "fixture-aggregate", "fixture-source", quote.ReceivedOn, (short)quote.EmissionReason, (short)1, lifetime }, values);
    }

}
