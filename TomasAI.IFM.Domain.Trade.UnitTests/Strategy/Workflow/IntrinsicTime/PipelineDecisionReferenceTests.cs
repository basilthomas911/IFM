using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.DataExport;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Reference;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Queries;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Reference;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Query.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

public sealed class PipelineDecisionReferenceTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), $"ifm-pdr-{Guid.NewGuid():N}");

    public PipelineDecisionReferenceTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Generators_return_stable_non_authoritative_twelve_case_catalogs()
    {
        var regimes = new RegimeDiscoveryDecisionReferenceGenerator().Generate();
        var conditions = new MarketConditionDecisionReferenceGenerator().Generate();

        regimes.Should().HaveCount(12);
        conditions.Should().HaveCount(12);
        regimes.Select(x => x.CaseCode).Should().Equal(Enumerable.Range(1, 12).Select(x => $"RD-REF-{x:D3}"));
        conditions.Select(x => x.CaseCode).Should().Equal(Enumerable.Range(1, 12).Select(x => $"MC-REF-{x:D3}"));
        regimes.Should().OnlyContain(row => !row.IsAuthoritative && !row.IsCompleteEnumeration);
        conditions.Should().OnlyContain(row => !row.IsAuthoritative && !row.IsCompleteEnumeration);
        new RegimeDiscoveryDecisionReferenceGenerator().Generate().Should().BeEquivalentTo(regimes,
            options => options.WithStrictOrdering());
        new MarketConditionDecisionReferenceGenerator().Generate().Should().BeEquivalentTo(conditions,
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Query_contracts_and_result_collections_round_trip_with_append_only_messagepack()
    {
        var subject = new ActorSubject(ActorType.Query, GetRegimeDiscoveryDecisionReferenceQuery.Actor,
            GetRegimeDiscoveryDecisionReferenceQuery.Verb, "decision-reference");
        var query = new GetRegimeDiscoveryDecisionReferenceQuery
        {
            Subject = subject,
            EntityId = new ActorEntityId("decision-reference")
        };

        MessagePackSerializer.Deserialize<GetRegimeDiscoveryDecisionReferenceQuery>(
            MessagePackSerializer.Serialize(query)).Should().BeEquivalentTo(query);
        var regimes = new RegimeDiscoveryDecisionReferenceGenerator().Generate();
        MessagePackSerializer.Deserialize<RegimeDiscoveryDecisionReferenceDto[]>(
            MessagePackSerializer.Serialize(regimes)).Should().BeEquivalentTo(regimes,
                options => options.WithStrictOrdering());
        var conditions = new MarketConditionDecisionReferenceGenerator().Generate();
        MessagePackSerializer.Deserialize<MarketConditionDecisionReferenceDto[]>(
            MessagePackSerializer.Serialize(conditions)).Should().BeEquivalentTo(conditions,
                options => options.WithStrictOrdering());
    }

    [Fact]
    public void Query_actor_maps_include_storage_free_reference_queries()
    {
        Map(typeof(RegimeDiscoveryQueryActor), "_parseMap").Keys.Cast<string>()
            .Should().Contain(GetRegimeDiscoveryDecisionReferenceQuery.Verb);
        Map(typeof(RegimeDiscoveryQueryActor), "_receiveMap").Keys.Cast<Type>()
            .Should().Contain(typeof(GetRegimeDiscoveryDecisionReferenceQuery));
        Map(typeof(MarketConditionQueryActor), "_parseMap").Keys.Cast<string>()
            .Should().Contain(GetMarketConditionDecisionReferenceQuery.Verb);
        Map(typeof(MarketConditionQueryActor), "_receiveMap").Keys.Cast<Type>()
            .Should().Contain(typeof(GetMarketConditionDecisionReferenceQuery));
    }

    [Fact]
    public async Task Csv_adapters_write_excel_compatible_deterministic_rows_and_default_to_overwrite()
    {
        var regimeFile = Path.Combine(_directory, "regimes.csv");
        var conditionFile = Path.Combine(_directory, "conditions.csv");
        var regimes = new RegimeDiscoveryDecisionReferenceGenerator().Generate();
        regimes[0] = regimes[0] with { Name = "Bullish, \"quoted\"\r\nreference" };
        var conditions = new MarketConditionDecisionReferenceGenerator().Generate();

        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-CA");
            await new RegimeDiscoveryDecisionReferenceCsvAdapter().ExportAsync(regimes, regimeFile);
            await new MarketConditionDecisionReferenceCsvAdapter().ExportAsync(conditions, conditionFile);
            await new RegimeDiscoveryDecisionReferenceCsvAdapter().ExportAsync(regimes, regimeFile);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        var regimeBytes = await File.ReadAllBytesAsync(regimeFile);
        regimeBytes.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());
        var regimeText = await File.ReadAllTextAsync(regimeFile);
        regimeText.Should().Contain("\"Bullish, \"\"quoted\"\"\r\nreference\"");
        regimeText.Should().Contain("0.8").And.NotContain("0,8");
        regimeText.Split("\r\n").First().Should().StartWith("PipelineStage,GeneratorVersion");
        (await File.ReadAllLinesAsync(conditionFile)).Should().HaveCount(13);
    }

    [Fact]
    public async Task Csv_adapter_honors_create_new_empty_collection_cancellation_and_invalid_directory()
    {
        var file = Path.Combine(_directory, "empty.csv");
        var adapter = new RegimeDiscoveryDecisionReferenceCsvAdapter();
        await adapter.ExportAsync([], file);
        (await File.ReadAllLinesAsync(file)).Should().ContainSingle();
        await FluentActions.Awaiting(() => adapter.ExportAsync([], file, overwrite: false))
            .Should().ThrowAsync<IOException>();
        var original = await File.ReadAllTextAsync(file);
        (await File.ReadAllTextAsync(file)).Should().Be(original);

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await FluentActions.Awaiting(() => adapter.ExportAsync([], Path.Combine(_directory, "cancel.csv"),
                cancellationToken: cancelled.Token))
            .Should().ThrowAsync<OperationCanceledException>();
        File.Exists(Path.Combine(_directory, "cancel.csv")).Should().BeFalse();
        await FluentActions.Awaiting(() => adapter.ExportAsync([], Path.Combine(_directory, "missing", "x.csv")))
            .Should().ThrowAsync<DirectoryNotFoundException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    static IDictionary Map(Type type, string name) => (IDictionary)type
        .GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
}
