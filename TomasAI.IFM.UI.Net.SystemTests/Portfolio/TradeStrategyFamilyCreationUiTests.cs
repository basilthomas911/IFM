using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Views.Reference;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class TradeStrategyFamilyCreationUiTests
{
    static TradeStrategySymbolReadModel Product(int id = 101) => new() { Id = id, Symbol = "ES", Currency = "USD", Exchange = "XCME", Description = "ES futures options" };
    [Fact]
    public async Task Editor_loads_product_metadata_read_only_and_sends_selected_id_with_stable_retry_operation()
    {
        var queries = Substitute.For<IReferenceQueryApi>(); var commands = Substitute.For<IReferenceCommandApi>();
        queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption, Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([Product()]));
        List<CreateTradeStrategyFamilyRequest> sent = [];
        commands.CreateTradeStrategyFamilyAsync(Arg.Any<CreateTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            sent.Add(call.Arg<CreateTradeStrategyFamilyRequest>());
            return Task.FromResult<ServiceResult<Guid>>(sent.Count == 1 ? new ServiceFailed<Guid>(503, "timeout") : new ServiceOk<Guid>(sent[^1].OperationId));
        });
        using var form = new TradeStrategyFamilyEditorForm(queries, commands);
        Field<ComboBox>(form, "_family").SelectedItem = TradeStrategyFamilyType.FuturesOption;
        Field<ComboBox>(form, "_strategy").SelectedItem = TradeStrategyType.VerticalSpread;
        Field<ComboBox>(form, "_timeFrame").SelectedItem = TimeFrameType.Weekly;
        Field<ComboBox>(form, "_product").SelectedIndex = 0;
        Field<TextBox>(form, "_description").Text = "Weekly ES vertical";
        Field<TextBox>(form, "_currency").Text.Should().Be("USD"); Field<TextBox>(form, "_currency").ReadOnly.Should().BeTrue();
        Field<TextBox>(form, "_exchange").Text.Should().Be("XCME"); Field<TextBox>(form, "_exchange").ReadOnly.Should().BeTrue();
        Field<TextBox>(form, "_systemKey").Text.Should().Be("FuturesOption-VerticalSpread");
        Field<Button>(form, "_save").Enabled.Should().BeTrue();
        await Save(form); Field<Label>(form, "_status").Text.Should().Be("timeout");
        await Save(form);
        sent.Should().HaveCount(2); sent[0].Should().Be(sent[1]); sent[0].TradeStrategySymbolId.Should().Be(101);
        sent[0].OperationId.Should().NotBeEmpty(); form.DialogResult.Should().Be(DialogResult.OK);
    }

    [Theory]
    [InlineData("currency")]
    [InlineData("exchange")]
    [InlineData("symbol")]
    public void Invalid_product_metadata_never_becomes_a_selectable_choice(string field)
    {
        var product = field switch { "currency" => Product() with { Currency = "" }, "exchange" => Product() with { Exchange = "" }, _ => Product() with { Symbol = " " } };
        var queries = Substitute.For<IReferenceQueryApi>();
        queries.GetTradeStrategySymbolsAsync(Arg.Any<TradeStrategyFamilyType>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([product]));
        using var form = new TradeStrategyFamilyEditorForm(queries, Substitute.For<IReferenceCommandApi>());
        Field<ComboBox>(form, "_family").SelectedItem = TradeStrategyFamilyType.Futures;
        Field<ComboBox>(form, "_product").Items.Count.Should().Be(0); Field<Button>(form, "_save").Enabled.Should().BeFalse();
        Field<Label>(form, "_status").Text.Should().Contain("Creation is blocked");
    }

    [Fact]
    public async Task Out_of_order_family_lookup_cannot_replace_newer_choices()
    {
        var delayed = new TaskCompletionSource<ServiceResult<TradeStrategySymbolReadModel[]>>();
        var queries = Substitute.For<IReferenceQueryApi>();
        queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.Futures, Arg.Any<CancellationToken>()).Returns(delayed.Task);
        queries.GetTradeStrategySymbolsAsync(TradeStrategyFamilyType.FuturesOption, Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([Product(202)]));
        using var form = new TradeStrategyFamilyEditorForm(queries, Substitute.For<IReferenceCommandApi>());
        Field<ComboBox>(form, "_family").SelectedItem = TradeStrategyFamilyType.Futures;
        Field<ComboBox>(form, "_family").SelectedItem = TradeStrategyFamilyType.FuturesOption;
        delayed.SetResult(new ServiceOk<TradeStrategySymbolReadModel[]>([Product(101)]));
        await Task.Yield();
        var choice = Field<ComboBox>(form, "_product").Items[0];
        ((TradeStrategySymbolReadModel)choice.GetType().GetProperty("Value")!.GetValue(choice)!).Id.Should().Be(202);
    }

    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    static Task Save(Form form) => (Task)form.GetType().GetMethod("SaveAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
}
