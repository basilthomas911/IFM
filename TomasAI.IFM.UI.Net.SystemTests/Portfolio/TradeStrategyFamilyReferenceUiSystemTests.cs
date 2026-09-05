using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.Views.Reference;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.UI.Net.Services.MarketData;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class TradeStrategyFamilyReferenceUiSystemTests
{
    static TradeStrategyFamilyReadModel Row(int id = 5901) => TradeStrategyFamilySeed.Definitions[0]
        .Create(id, new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc), "test") with { TradeStrategySymbolId = 101, Exchange = "XCME" };

    static IReferenceQueryApi Queries(params TradeStrategyFamilyReadModel[] rows)
    {
        var queries = Substitute.For<IReferenceQueryApi>();
        queries.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategyFamilyReadModel[]>(rows));
        return queries;
    }

    static MarketDataQueryService Symbols()
    {
        var queries = Substitute.For<IMarketDataQueryApi>();
        queries.GetTradeStrategySymbolsAsync(Arg.Any<TradeStrategyFamilyType>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([new() { Id = 101, Symbol = "ES", Currency = "USD", Exchange = "XCME", Description = "ES futures" }]));
        return new(queries, Substitute.For<IMarketDataFeedQueryApi>());
    }

    [Fact]
    public async Task Creation_is_enabled_after_success_and_disabled_when_catalog_reload_throws()
    {
        var queries = Queries();
        using var view = new TradeStrategyFamilyReferenceView(queries, Substitute.For<IReferenceCommandApi>(), Symbols());
        await view.LoadAsync(); view.CanAdd.Should().BeTrue();
        queries.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<ServiceResult<TradeStrategyFamilyReadModel[]>>(new InvalidOperationException("offline")));
        await view.LoadAsync(); view.CanAdd.Should().BeFalse();
        Field<Label>(view, "_error").Text.Should().Contain("offline");
    }

    [Fact]
    [Trait("Gate", "PF-22")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public async Task Shared_family_names_group_distinct_exact_definitions_in_read_only_details()
    {
        using var view = new TradeStrategyFamilyReferenceView(Queries(Row(), Row(5904) with { Symbol = "NQ", TimeFrame = TimeFrameType.Weekly }));
        await view.LoadAsync();
        Field<ListBox>(view, "_families").Items.Cast<TradeStrategyFamilyType>().Should().Equal(TradeStrategyFamilyType.Futures);
        var definitions = Field<ListBox>(view, "_strategies");
        definitions.Items.Count.Should().Be(2);
        var fields = Field<Dictionary<string, TextBox>>(view, "_fields");
        fields.Values.Should().OnlyContain(x => x.ReadOnly && x.BackColor == Color.Black && x.ForeColor == Color.White);
        fields.Keys.ToList().IndexOf("Exchange").Should().BeLessThan(fields.Keys.ToList().IndexOf("Description"));
        fields["ID"].Text.Should().Be("5901");
        definitions.SelectedIndex = 1;
        fields["ID"].Text.Should().Be("5904"); fields["Symbol"].Text.Should().Be("NQ");
        fields["TimeFrame"].Text.Should().Be("Weekly"); fields["Exchange"].Text.Should().Be("XCME");
        view.CanAdd.Should().BeFalse(); view.CanChangeRemove.Should().BeFalse(); view.CanImport.Should().BeFalse();
    }

    [Fact]
    public async Task Family_master_filters_strategy_definitions_and_selection_clears_unrelated_details()
    {
        var vertical = TradeStrategyFamilySeed.Definitions[1].Create(5902, DateTime.UtcNow, "test");
        var condor = TradeStrategyFamilySeed.Definitions[2].Create(5903, DateTime.UtcNow, "test");
        var queries = Queries(Row(), vertical, condor, vertical with { TradeStrategyFamilyId = 5904, Symbol = "NQ" });
        using var view = new TradeStrategyFamilyReferenceView(queries);
        await view.LoadAsync();
        var families = Field<ListBox>(view, "_families"); var strategies = Field<ListBox>(view, "_strategies");
        families.Items.Cast<TradeStrategyFamilyType>().Should().Equal(TradeStrategyFamilyType.Futures, TradeStrategyFamilyType.FuturesOption);
        families.SelectedItem = TradeStrategyFamilyType.FuturesOption;
        strategies.Items.Count.Should().Be(3);
        strategies.Items[0].ToString().Should().Be("IronCondor");
        var fields = Field<Dictionary<string, TextBox>>(view, "_fields");
        fields["Strategy"].Text.Should().Be("IronCondor");
        strategies.SelectedIndex = 1; fields["ID"].Text.Should().Be("5902");
        strategies.SelectedIndex = 2; fields["ID"].Text.Should().Be("5904");
        families.SelectedItem = TradeStrategyFamilyType.Futures;
        strategies.Items.Count.Should().Be(1);
        strategies.Items[0].ToString().Should().Be("Futures");
        fields["ID"].Text.Should().Be("5901");
        queries.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategyFamilyReadModel[]>([]));
        await view.LoadAsync();
        strategies.Items.Count.Should().Be(0);
        fields.Values.Should().OnlyContain(x => x.Text == "");
    }

    [Fact]
    public void Reference_font_applies_to_all_editors_and_dynamically_added_controls()
    {
        var app = Substitute.For<IAppRoot>(); var service = Substitute.For<IReferenceDataService>();
        using var form = new ReferenceForm(app, service, Substitute.For<IEconomicCalendarService>());
        var panel = Field<Panel>(form, "pnlMarketData");
        using var lookup = new LookupTypeEditorView(new LookupTypeEditorViewModel(app, service));
        using var economic = new EconomicCalendarEditorView(new EconomicCalendarEditorViewModel(app, Substitute.For<IEconomicCalendarService>()));
        panel.Controls.Add(lookup); AssertFont(form); panel.Controls.Remove(lookup);
        panel.Controls.Add(economic); AssertFont(form);
        var late = new Label { Font = new Font("Arial", 12F) }; economic.Controls.Add(late); AssertFont(form);
    }

    [Fact]
    public async Task Add_and_cancel_use_inline_details_without_writing_or_changing_selection()
    {
        var commands = Substitute.For<IReferenceCommandApi>();
        using var view = new TradeStrategyFamilyReferenceView(Queries(Row()), commands, Symbols());
        await view.LoadAsync();
        view.Add(_ => { }); view.IsEditing.Should().BeTrue(); view.CanSave.Should().BeFalse();
        Field<ListBox>(view, "_families").Enabled.Should().BeFalse();
        view.Close(_ => { }).Should().BeFalse();
        view.IsEditing.Should().BeFalse(); Field<ListBox>(view, "_families").Enabled.Should().BeTrue();
        Field<Dictionary<string, TextBox>>(view, "_fields")["ID"].Text.Should().Be("5901");
        view.Close(_ => { }).Should().BeTrue();
        await commands.DidNotReceive().CreateTradeStrategyFamilyAsync(Arg.Any<CreateTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Save_guards_pending_commands_and_only_success_returns_to_refreshed_browse(bool success)
    {
        var pending = new TaskCompletionSource<ServiceResult<Guid>>();
        var commands = Substitute.For<IReferenceCommandApi>(); var queries = Queries(Row());
        commands.CreateTradeStrategyFamilyAsync(Arg.Any<CreateTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        using var view = new TradeStrategyFamilyReferenceView(queries, commands, Symbols());
        await view.LoadAsync(); view.Add(_ => { }); Fill(Field<TradeStrategyFamilyEditorControl>(view, "_editor"));
        view.CanSave.Should().BeTrue();
        view.Add(_ => { }); var save = Field<Task>(view, "_pendingSave");
        view.IsSaving.Should().BeTrue(); view.CanSave.Should().BeFalse(); view.Close(_ => { }).Should().BeFalse();
        view.Add(_ => { });
        pending.SetResult(success ? new ServiceOk<Guid>(Guid.NewGuid()) : new ServiceFailed<Guid>(503, "offline"));
        await save;
        view.IsSaving.Should().BeFalse(); view.IsEditing.Should().Be(!success); view.CanSave.Should().Be(!success);
        await commands.Received(1).CreateTradeStrategyFamilyAsync(Arg.Any<CreateTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>());
        await queries.Received(success ? 2 : 1).GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Closing_cancels_load_and_ignores_a_late_result()
    {
        var pending = new TaskCompletionSource<ServiceResult<TradeStrategyFamilyReadModel[]>>();
        var queries = Queries(); CancellationToken requested = default;
        queries.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(call => { requested = call.Arg<CancellationToken>(); return pending.Task; });
        using var view = new TradeStrategyFamilyReferenceView(queries, Substitute.For<IReferenceCommandApi>());
        var loading = view.LoadAsync(); await view.CloseAsync(); view.Dispose();
        requested.IsCancellationRequested.Should().BeTrue();
        pending.SetResult(new ServiceOk<TradeStrategyFamilyReadModel[]>([Row()]));
        await loading; view.CanAdd.Should().BeFalse();
    }

    [Fact]
    public async Task Reference_selector_labels_and_shared_add_save_cancel_follow_family_editor_state()
    {
        // Isolate the WinForms synchronization context from xUnit's worker threads.
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, true);
            using var context = new ApplicationContext();
            using var dispatcher = new Control();
            _ = dispatcher.Handle;
            dispatcher.BeginInvoke(async () =>
            {
                try { await VerifyReferenceFormAsync(); completion.SetResult(); }
                catch (Exception ex) { completion.SetException(ex); }
                finally { context.ExitThread(); }
            });
            System.Windows.Forms.Application.Run(context);
        }) { IsBackground = true, Name = "Reference family UI verification" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    static async Task VerifyReferenceFormAsync()
    {
        var service = Substitute.For<IReferenceDataService>();
        service.GetReferenceDataDefinitionTypesAsync(Arg.Any<CancellationToken>()).Returns(
            UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success([
                new("Reference", "Placeholder", 0, "lookup type", DateTime.UtcNow, "test"),
                new("Reference", "EconomicCalendar", 1, "Long economic calendar definition description", DateTime.UtcNow, "test")]));
        var model = new ReferenceViewModel(service); await model.LoadReferenceDataDefinitionTypesOperation.ExecuteAsync();
        var queries = Queries(Row()); var commands = Substitute.For<IReferenceCommandApi>();
        var app = Substitute.For<IAppRoot>(); app.Services.ReferenceQueries.Returns(queries);
        app.Services.ReferenceCommands.Returns(commands);
        var symbols = Symbols(); app.Services.MarketDataQueries.Returns(symbols);
        using var form = new ReferenceForm(app, service, Substitute.For<IEconomicCalendarService>());
        form.LoadViewModel(model); Invoke(form, "BindReferenceDataDefinitionTypes");
        var selector = Field<ComboBox>(form, "ddlReferenceDataSelector");
        selector.Items.Cast<string>().Should().Equal("lookup type", "economic calendar definitions", "trade strategy families");
        model.GetReferenceDataDefinitionType(1)!.ShortCode.Should().Be("EconomicCalendar");
        selector.SelectedIndex = 2;
        var view = (TradeStrategyFamilyReferenceView)Field<Panel>(form, "pnlMarketData").Controls[0];
        PrepareForDisplay(form);
        AssertFont(form);
        Field<Button>(form, "btnAdd").Enabled.Should().BeTrue();
        foreach (var name in new[] { "btnChange", "btnRemove" }) Field<Button>(form, name).Enabled.Should().BeTrue();
        Field<Button>(form, "btnImport").Enabled.Should().BeFalse();
        RenderIfRequested(form, "browse");
        Invoke(form, "btnAdd_Click", form, EventArgs.Empty);
        Field<Button>(form, "btnAdd").Text.Should().Be("Save"); Field<Button>(form, "btnAdd").Enabled.Should().BeFalse();
        Field<Button>(form, "btnClose").Text.Should().Be("Cancel"); selector.Enabled.Should().BeFalse();
        Fill(Field<TradeStrategyFamilyEditorControl>(view, "_editor"));
        AssertFont(form);
        AssertInputColors(Field<TradeStrategyFamilyEditorControl>(view, "_editor"));
        Field<Button>(form, "btnAdd").Enabled.Should().BeTrue();
        RenderIfRequested(form, "add");
        Invoke(form, "btnClose_Click", form, EventArgs.Empty);
        view.IsEditing.Should().BeFalse(); selector.Enabled.Should().BeTrue(); Field<Button>(form, "btnClose").Text.Should().Be("Close");
        form.Visible.Should().BeTrue(); form.IsDisposed.Should().BeFalse();
        Invoke(form, "btnChange_Click", form, EventArgs.Empty); await Field<Task>(view, "_pendingSave");
        Field<Button>(form, "btnChange").Text.Should().Be("Save"); Field<Button>(form, "btnChange").Enabled.Should().BeTrue();
        Field<Button>(form, "btnAdd").Enabled.Should().BeFalse(); Field<Button>(form, "btnRemove").Enabled.Should().BeFalse();
        selector.Enabled.Should().BeFalse(); Field<Button>(form, "btnClose").Text.Should().Be("Cancel");
        RenderIfRequested(form, "change");
        Invoke(form, "btnClose_Click", form, EventArgs.Empty);
        Field<Button>(form, "btnChange").Text.Should().Be("C&hange"); Field<Button>(form, "btnRemove").Enabled.Should().BeTrue();
        Field<Button>(form, "btnClose").Text.Should().Be("Close");
        form.Visible.Should().BeTrue(); form.IsDisposed.Should().BeFalse();
        commands.ReceivedCalls().Should().BeEmpty("Cancel must not save an add or change operation");
        await queries.DidNotReceive().GetTradeStrategySymbolsAsync(Arg.Any<TradeStrategyFamilyType>(), Arg.Any<CancellationToken>());
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        form.FormClosed += (_, _) => closed.TrySetResult();
        Field<Button>(form, "btnClose").PerformClick();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        form.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Change_initializes_selected_values_and_cancel_does_not_write()
    {
        var commands = Substitute.For<IReferenceCommandApi>();
        using var view = new TradeStrategyFamilyReferenceView(Queries(Row()), commands, Symbols());
        await view.LoadAsync(); view.CanChangeRemove.Should().BeTrue();
        view.Change(_ => { }); await Field<Task>(view, "_pendingSave");
        view.IsChanging.Should().BeTrue(); view.CanAdd.Should().BeFalse(); view.CanChangeRemove.Should().BeFalse();
        var editor = Field<TradeStrategyFamilyEditorControl>(view, "_editor");
        Field<ComboBox>(editor, "_family").SelectedItem.Should().Be(Row().Family);
        Field<ComboBox>(editor, "_strategy").SelectedItem.Should().Be(Row().Strategy);
        Field<ComboBox>(editor, "_timeFrame").SelectedItem.Should().Be(Row().TimeFrame);
        Field<TextBox>(editor, "_description").Text.Should().Be(Row().Description);
        Field<TextBox>(editor, "_description").ReadOnly.Should().BeFalse();
        Field<TextBox>(editor, "_description").Multiline.Should().BeTrue();
        view.CanSave.Should().BeTrue(); view.Close(_ => { }).Should().BeFalse();
        view.IsEditing.Should().BeFalse(); view.CanChangeRemove.Should().BeTrue();
        commands.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Modal_reference_dialog_closes_on_the_first_close_button_click()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var service = Substitute.For<IReferenceDataService>();
                var model = new ReferenceViewModel(service);
                var queries = Queries(Row()); var commands = Substitute.For<IReferenceCommandApi>(); var symbols = Symbols();
                var app = Substitute.For<IAppRoot>(); app.Services.ReferenceQueries.Returns(queries);
                app.Services.ReferenceCommands.Returns(commands);
                app.Services.MarketDataQueries.Returns(symbols);
                using var form = new ReferenceForm(app, service, Substitute.For<IEconomicCalendarService>());
                form.LoadViewModel(model); Invoke(form, "BindReferenceDataDefinitionTypes");
                form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form,
                    typeof(ReferenceForm).GetMethod("ReferenceForm_Load", BindingFlags.Instance | BindingFlags.NonPublic)!);
                form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-2000, -2000);
                bool timedOut = false;
                using var watchdog = new System.Windows.Forms.Timer { Interval = 1500 };
                watchdog.Tick += (_, _) => { timedOut = true; form.Close(); };
                form.Shown += (_, _) => form.BeginInvoke(() => Field<Button>(form, "btnClose").PerformClick());
                watchdog.Start(); form.ShowDialog(); watchdog.Stop();
                timedOut.Should().BeFalse("one click on Close must dismiss the modal dialog");
                completion.SetResult();
            }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "Reference modal close verification" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Change_failure_preserves_draft_and_retry_identity_then_reloads_new_version()
    {
        var commands = Substitute.For<IReferenceCommandApi>(); var queries = Queries(Row());
        List<ChangeTradeStrategyFamilyRequest> requests = [];
        commands.ChangeTradeStrategyFamilyAsync(Arg.Any<ChangeTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => { requests.Add(call.Arg<ChangeTradeStrategyFamilyRequest>()); return new ServiceFailed<Guid>(503, "offline"); });
        using var view = new TradeStrategyFamilyReferenceView(queries, commands, Symbols());
        await view.LoadAsync(); view.Change(_ => { }); await Field<Task>(view, "_pendingSave");
        var editor = Field<TradeStrategyFamilyEditorControl>(view, "_editor");
        Field<ComboBox>(editor, "_timeFrame").SelectedItem = TimeFrameType.Weekly;
        Field<TextBox>(editor, "_description").Text = "Edited description";
        view.Change(_ => { }); await Field<Task>(view, "_pendingSave");
        view.IsChanging.Should().BeTrue(); Field<TextBox>(editor, "_description").Text.Should().Be("Edited description");
        commands.ChangeTradeStrategyFamilyAsync(Arg.Any<ChangeTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => { requests.Add(call.Arg<ChangeTradeStrategyFamilyRequest>()); return new ServiceOk<Guid>(requests[^1].OperationId); });
        queries.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategyFamilyReadModel[]>(
            [Row(), Row() with { DefinitionVersion = 2, TimeFrame = TimeFrameType.Weekly, Description = "Edited description" }]));
        view.Change(_ => { }); await Field<Task>(view, "_pendingSave");
        requests.Should().HaveCount(2); requests[0].Should().Be(requests[1]);
        requests[0].Target.Should().Be(TradeStrategyFamilyReference.From(Row()));
        requests[0].Definition.TimeFrame.Should().Be(TimeFrameType.Weekly);
        view.IsEditing.Should().BeFalse(); Field<ListBox>(view, "_strategies").Items.Count.Should().Be(1);
        Field<Dictionary<string, TextBox>>(view, "_fields")["Version"].Text.Should().Be("2");
        await commands.DidNotReceive().CreateTradeStrategyFamilyAsync(Arg.Any<CreateTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DialogResult.No)]
    [InlineData(DialogResult.Yes)]
    public async Task Remove_confirms_exact_label_and_only_yes_writes_and_hides_retired_strategy(DialogResult answer)
    {
        var commands = Substitute.For<IReferenceCommandApi>(); var queries = Queries(Row()); string? prompt = null;
        commands.RemoveTradeStrategyFamilyAsync(Arg.Any<RemoveTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                queries.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategyFamilyReadModel[]>(
                    [Row(), Row() with { DefinitionVersion = 2, State = TradeStrategyFamilyState.Retired }]));
                return new ServiceOk<Guid>(call.Arg<RemoveTradeStrategyFamilyRequest>().OperationId);
            });
        using var view = new TradeStrategyFamilyReferenceView(queries, commands, Symbols(), message => { prompt = message; return answer; });
        await view.LoadAsync(); view.Remove(); await Field<Task>(view, "_pendingSave");
        prompt.Should().Be("Remove Futures-ES-USD-Daily XCME ?");
        await commands.Received(answer == DialogResult.Yes ? 1 : 0).RemoveTradeStrategyFamilyAsync(
            Arg.Is<RemoveTradeStrategyFamilyRequest>(x => x.Target == TradeStrategyFamilyReference.From(Row()) && x.OperationId != Guid.Empty), Arg.Any<CancellationToken>());
        Field<ListBox>(view, "_strategies").Items.Count.Should().Be(answer == DialogResult.Yes ? 0 : 1);
        view.CanChangeRemove.Should().Be(answer == DialogResult.No);
    }

    [Fact]
    public async Task Pending_removal_blocks_duplicate_clicks_and_close_then_failure_allows_same_operation_retry()
    {
        var pending = new TaskCompletionSource<ServiceResult<Guid>>(); var commands = Substitute.For<IReferenceCommandApi>();
        List<RemoveTradeStrategyFamilyRequest> requests = [];
        commands.RemoveTradeStrategyFamilyAsync(Arg.Any<RemoveTradeStrategyFamilyRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => { requests.Add(call.Arg<RemoveTradeStrategyFamilyRequest>()); return pending.Task; });
        using var view = new TradeStrategyFamilyReferenceView(Queries(Row()), commands, Symbols(), _ => DialogResult.Yes);
        await view.LoadAsync(); view.Remove(); var save = Field<Task>(view, "_pendingSave");
        view.Remove(); view.Change(_ => { }); view.Add(_ => { }); view.Close(_ => { }).Should().BeFalse();
        requests.Should().HaveCount(1); view.IsSaving.Should().BeTrue();
        pending.SetResult(new ServiceFailed<Guid>(503, "offline")); await save;
        view.CanChangeRemove.Should().BeTrue(); Field<Label>(view, "_error").Text.Should().Be("offline");
        view.Remove(); await Field<Task>(view, "_pendingSave");
        requests.Should().HaveCount(2); requests[1].Should().Be(requests[0]);
    }

    static void Fill(TradeStrategyFamilyEditorControl editor)
    {
        Field<ComboBox>(editor, "_family").SelectedItem = TradeStrategyFamilyType.Futures;
        Field<ComboBox>(editor, "_timeFrame").SelectedItem = TimeFrameType.Daily;
        Field<ComboBox>(editor, "_product").SelectedIndex = 0;
        Field<TextBox>(editor, "_description").Text = "Daily ES futures";
    }
    static void AssertFont(Control control)
    {
        control.Font.Name.Should().Be("Microsoft Sans Serif"); control.Font.SizeInPoints.Should().Be(10F);
        foreach (Control child in control.Controls) AssertFont(child);
    }
    static void AssertInputColors(Control control)
    {
        if (control is TextBox or ComboBox) { control.BackColor.Should().Be(Color.Black); control.ForeColor.Should().Be(Color.White); }
        foreach (Control child in control.Controls) AssertInputColors(child);
    }
    static void PrepareForDisplay(Form form)
    {
        // Render only this test-owned off-screen form; do not reload its already-bound fixture.
        form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form,
            typeof(ReferenceForm).GetMethod("ReferenceForm_Load", BindingFlags.Instance | BindingFlags.NonPublic)!);
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-2000, -2000);
        form.Show();
    }
    static void RenderIfRequested(Form form, string state)
    {
        var directory = Environment.GetEnvironmentVariable("IFM_REFERENCE_UI_RENDER_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(Path.Combine(directory, $"family-{state}.png"));
    }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    static void Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
}
