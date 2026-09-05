using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Views.Reference;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.UI.Net.Services.MarketData;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class TradeStrategyFamilyCreationUiTests
{
    static MarketDataQueryService Service(IMarketDataQueryApi api) => new(api, Substitute.For<IMarketDataFeedQueryApi>());
    static TradeStrategySymbolReadModel Product(int id = 101) => new() { Id = id, Symbol = "ES", Currency = "USD", Exchange = "XCME", Description = "ES futures options" };
    [Fact]
    public async Task Any_provider_symbol_can_be_selected_and_saved_not_only_ES()
    {
        var queries = Substitute.For<IMarketDataQueryApi>(); var commands = Substitute.For<IReferenceCommandApi>();
        queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([Product(), Product(202) with { Symbol = "CL", Exchange = "XNYM", Description = "CL futures options" }]));
        commands.CreateTradeStrategyFamilyAsync(Arg.Any<CreateTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new ServiceOk<Guid>(call.Arg<CreateTradeStrategyFamilyRequest>().OperationId));
        using var editor = new TradeStrategyFamilyEditorControl(Service(queries), commands);
        Field<ComboBox>(editor, "_family").SelectedItem = TradeStrategyFamilyType.FuturesOption;
        var symbols = Field<ComboBox>(editor, "_product"); symbols.Items.Count.Should().Be(2); symbols.SelectedIndex = 1;
        Field<TextBox>(editor, "_exchange").Text.Should().Be("XNYM");
        Field<TextBox>(editor, "_description").Text.Should().Be("CL futures options");
        Field<ComboBox>(editor, "_strategy").SelectedItem = TradeStrategyType.VerticalSpread;
        Field<ComboBox>(editor, "_timeFrame").SelectedItem = TimeFrameType.Weekly;
        await editor.SaveAsync(); editor.HasCreated.Should().BeTrue();
        await commands.Received(1).CreateTradeStrategyFamilyAsync(Arg.Is<CreateTradeStrategyFamilyRequest>(x => x.TradeStrategySymbolId == 202), Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task Family_load_selects_single_symbol_and_populates_metadata_with_editable_description()
    {
        var queries = Substitute.For<IMarketDataQueryApi>();
        queries.GetTradeStrategySymbolsAsync(Arg.Any<TradeStrategyFamilyType>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([Product()]));
        using var editor = new TradeStrategyFamilyEditorControl(Service(queries), Substitute.For<IReferenceCommandApi>());
        Field<ComboBox>(editor, "_family").SelectedItem = TradeStrategyFamilyType.FuturesOption;
        Field<ComboBox>(editor, "_product").AccessibleName.Should().Be("Symbol");
        Field<ComboBox>(editor, "_product").SelectedIndex.Should().Be(0);
        Field<TextBox>(editor, "_currency").Text.Should().Be("USD");
        var exchange = Field<TextBox>(editor, "_exchange"); var description = Field<TextBox>(editor, "_description");
        exchange.Text.Should().Be("XCME"); description.Text.Should().Be("ES futures options");
        description.ReadOnly.Should().BeFalse(); description.BackColor.Should().Be(exchange.BackColor); description.ForeColor.Should().Be(exchange.ForeColor);
        description.Text = "My custom strategy description";
        Field<ComboBox>(editor, "_family").SelectedItem = TradeStrategyFamilyType.Futures;
        description.Text.Should().Be("My custom strategy description");
        await queries.Received(1).GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption, Arg.Any<CancellationToken>());
        await queries.Received(1).GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Provider_failure_is_visible_and_never_enables_saving()
    {
        var queries = Substitute.For<IMarketDataQueryApi>();
        queries.GetTradeStrategySymbolsAsync(Arg.Any<TradeStrategyFamilyType>(), Arg.Any<CancellationToken>()).Returns(new ServiceFailed<TradeStrategySymbolReadModel[]>(503, "Databento metadata unavailable"));
        using var editor = new TradeStrategyFamilyEditorControl(Service(queries), Substitute.For<IReferenceCommandApi>());
        Field<ComboBox>(editor, "_family").SelectedItem = TradeStrategyFamilyType.Futures;
        Field<Label>(editor, "_status").Text.Should().Be("Databento metadata unavailable");
        Field<ComboBox>(editor, "_product").Items.Count.Should().Be(0); editor.CanSave.Should().BeFalse();
    }
    [Fact]
    public async Task Editor_loads_product_metadata_read_only_and_sends_selected_id_with_stable_retry_operation()
    {
        var queries = Substitute.For<IMarketDataQueryApi>(); var commands = Substitute.For<IReferenceCommandApi>();
        queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption, Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([Product()]));
        List<CreateTradeStrategyFamilyRequest> sent = [];
        commands.CreateTradeStrategyFamilyAsync(Arg.Any<CreateTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            sent.Add(call.Arg<CreateTradeStrategyFamilyRequest>());
            return Task.FromResult<ServiceResult<Guid>>(sent.Count == 1 ? new ServiceFailed<Guid>(503, "timeout") : new ServiceOk<Guid>(sent[^1].OperationId));
        });
        using var form = new TradeStrategyFamilyEditorControl(Service(queries), commands);
        Field<ComboBox>(form, "_family").SelectedItem = TradeStrategyFamilyType.FuturesOption;
        Field<ComboBox>(form, "_strategy").SelectedItem = TradeStrategyType.VerticalSpread;
        Field<ComboBox>(form, "_timeFrame").SelectedItem = TimeFrameType.Weekly;
        Field<ComboBox>(form, "_product").SelectedIndex = 0;
        Field<TextBox>(form, "_description").Text = "Weekly ES vertical";
        Field<TextBox>(form, "_currency").Text.Should().Be("USD"); Field<TextBox>(form, "_currency").ReadOnly.Should().BeTrue();
        Field<TextBox>(form, "_exchange").Text.Should().Be("XCME"); Field<TextBox>(form, "_exchange").ReadOnly.Should().BeTrue();
        Field<TextBox>(form, "_systemKey").Text.Should().Be("FuturesOption-VerticalSpread");
        form.CanSave.Should().BeTrue();
        await Save(form); Field<Label>(form, "_status").Text.Should().Be("timeout");
        await Save(form);
        sent.Should().HaveCount(2); sent[0].Should().Be(sent[1]); sent[0].TradeStrategySymbolId.Should().Be(101);
        sent[0].OperationId.Should().NotBeEmpty(); form.HasCreated.Should().BeTrue();
    }

    [Theory]
    [InlineData("currency")]
    [InlineData("exchange")]
    [InlineData("symbol")]
    public void Invalid_product_metadata_never_becomes_a_selectable_choice(string field)
    {
        var product = field switch { "currency" => Product() with { Currency = "" }, "exchange" => Product() with { Exchange = "" }, _ => Product() with { Symbol = " " } };
        var queries = Substitute.For<IMarketDataQueryApi>();
        queries.GetTradeStrategySymbolsAsync(Arg.Any<TradeStrategyFamilyType>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([product]));
        using var form = new TradeStrategyFamilyEditorControl(Service(queries), Substitute.For<IReferenceCommandApi>());
        Field<ComboBox>(form, "_family").SelectedItem = TradeStrategyFamilyType.Futures;
        Field<ComboBox>(form, "_product").Items.Count.Should().Be(0); form.CanSave.Should().BeFalse();
        Field<Label>(form, "_status").Text.Should().Contain("Creation is blocked");
    }

    [Fact]
    public async Task Out_of_order_family_lookup_cannot_replace_newer_choices()
    {
        var delayed = new TaskCompletionSource<ServiceResult<TradeStrategySymbolReadModel[]>>();
        var queries = Substitute.For<IMarketDataQueryApi>();
        queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, Arg.Any<CancellationToken>()).Returns(delayed.Task);
        queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption, Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([Product(202)]));
        using var form = new TradeStrategyFamilyEditorControl(Service(queries), Substitute.For<IReferenceCommandApi>());
        Field<ComboBox>(form, "_family").SelectedItem = TradeStrategyFamilyType.Futures;
        Field<ComboBox>(form, "_family").SelectedItem = TradeStrategyFamilyType.FuturesOption;
        delayed.SetResult(new ServiceOk<TradeStrategySymbolReadModel[]>([Product(101)]));
        await Task.Yield();
        var choice = Field<ComboBox>(form, "_product").Items[0];
        ((TradeStrategySymbolReadModel)choice.GetType().GetProperty("Value")!.GetValue(choice)!).Id.Should().Be(202);
    }

    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    static Task Save(TradeStrategyFamilyEditorControl form) => form.SaveAsync();
}
