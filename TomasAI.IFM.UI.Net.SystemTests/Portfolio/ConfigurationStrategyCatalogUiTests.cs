using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.Views.Reference;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class ConfigurationStrategyCatalogUiTests
{
    [Fact]
    public async Task Default_view_shows_only_three_families_and_full_catalog_is_explicitly_available()
    {
        var fixture = new CatalogFixture();
        using var view = new StrategyCatalogReferenceView(fixture.Queries, fixture.Commands);
        await view.LoadAsync();
        var list = Field<ListBox>(view, "list");
        list.Items.Cast<object>().Select(x => x.ToString()!.Split("  [")[0]).Should().Equal("Futures", "Vertical Spreads", "Iron Condor");
        Field<CheckBox>(view, "showAll").Checked = true;
        await Wait(() => list.Items.Count == 4);
        Field<CheckBox>(view, "showAll").Checked = false;
        await Wait(() => list.Items.Count == 3);
        await view.LoadAsync();
        list.Items.Count.Should().Be(3);
    }

    static StrategyCatalogSummary[] References() => StrategyCatalogExamples.Create().Select(x => new StrategyCatalogSummary(x.Key, x.Code, x.Name, CatalogLifecycleStatus.Draft, "hash")).ToArray();
    [Fact]
    public void Variant_controls_edit_bias_side_premium_and_numeric_parameters_independently()
    {
        var definition = StrategyCatalogExamples.Create().Single(x => x.Code == "ShortBalancedIronCondor");
        using var editor = new StrategyCatalogDefinitionEditor(definition, true, References());
        Field<ComboBox>(editor, "bias").SelectedItem = "Bullish";
        Field<NumericUpDown>(editor, "delta").Value = .17m;
        Field<NumericUpDown>(editor, "tolerance").Value = .03m;
        Field<CheckBox>(editor, "symmetric").Checked = false;
        var result = editor.ReadDefinition();
        result.Side.Should().Be("Short"); result.PremiumMode.Should().Be("Credit"); result.Bias.Should().Be("Bullish");
        result.Settings.GetProperty("TargetNetDelta").GetDecimal().Should().Be(.17m);
        result.Settings.GetProperty("BalanceTolerance").GetDecimal().Should().Be(.03m);
        result.Settings.GetProperty("SymmetricWings").GetBoolean().Should().BeFalse();
        Field<TextBox>(editor, "description").Multiline.Should().BeTrue();
    }

    [Fact]
    public void Product_selection_uses_catalog_metadata_and_nested_schema_fields_round_trip()
    {
        var definition = new StrategyCatalogDefinition { Key = new(StrategyCatalogKind.Deployment, Guid.NewGuid(), 1), Code = "WeeklyES", Name = "Weekly ES", Parent = StrategyCatalogExamples.Create().Single(x => x.Key.Kind == StrategyCatalogKind.Strategy).Key, Horizon = TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly };
        using var editor = new StrategyCatalogDefinitionEditor(definition, true, References(), [new(101, "ES", "XCME", "USD")]);
        var grid = Field<Dictionary<string, DataGridView>>(editor, "grids")["Products"];
        grid.Columns[0].Should().BeOfType<DataGridViewComboBoxColumn>();
        grid.Rows.Add("ES - XCME - USD [101]");
        editor.ReadDefinition().Products.Should().Equal(new CatalogProduct(101, "ES", "XCME", "USD"));
        grid.Columns[2].ReadOnly.Should().BeTrue(); grid.Columns[3].ReadOnly.Should().BeTrue();
        var shape = new CatalogParameterShape { Properties = new() { ["Legs"] = new() { Type = CatalogValueType.Array, MinLength = 1, MaxLength = 4, Items = new() { Properties = new() { ["Ratio"] = new() { Type = CatalogValueType.Decimal, Minimum = .1m } }, Required = ["Ratio"] } } }, Required = ["Legs"] };
        using var schema = new StrategyCatalogDefinitionEditor(new() { Key = new(StrategyCatalogKind.ParameterSchema, Guid.NewGuid(), 1), Code = "Custom", Name = "Custom", Settings = JsonSerializer.SerializeToElement(shape) }, true, References());
        schema.ReadDefinition().Settings.Deserialize<CatalogParameterShape>().Should().BeEquivalentTo(shape);
        Field<DataGridView>(schema, "schema").Rows.Add("$/properties/Legs/items/properties/Offset", "Decimal", false, "points", -2, 2);
        schema.ReadDefinition().Settings.Deserialize<CatalogParameterShape>()!.Properties["Legs"].Items!.Properties.Should().ContainKey("Offset");
    }

    [Fact]
    public async Task Save_failure_preserves_edit_and_retry_identity_while_cancel_discards_edit()
    {
        var fixture = new CatalogFixture(); using var view = new StrategyCatalogReferenceView(fixture.Queries, fixture.Commands);
        await view.LoadAsync(); view.Change(_ => { });
        Field<TextBox>(Field<StrategyCatalogDefinitionEditor>(view, "editor"), "name").Text = "Changed family";
        fixture.Fail = true; view.Change(_ => { }); await Field<Task>(view, "pending");
        view.IsEditing.Should().BeTrue(); var operation = fixture.Requests.Single().OperationId;
        fixture.Fail = false; view.Change(_ => { }); await Field<Task>(view, "pending");
        fixture.Requests.Last().OperationId.Should().Be(operation); view.IsEditing.Should().BeFalse();
        fixture.Rows.Should().HaveCount(5); fixture.Rows.Values.Last().Definition.Key.Version.Should().Be(2);
        view.Change(_ => { }); view.Close(_ => { }).Should().BeFalse(); view.IsEditing.Should().BeFalse();
        view.Close(_ => { }).Should().BeTrue(); fixture.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Actual_reference_dialog_hosts_new_catalog_and_cancel_then_close_work_on_rendered_buttons()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var fixture = new CatalogFixture(); var service = Substitute.For<IReferenceDataService>(); var app = Substitute.For<IAppRoot>();
                app.Services.ReferenceQueries.Returns(fixture.Queries); app.Services.ReferenceCommands.Returns(fixture.Commands);
                using var form = new ReferenceForm(app, service); form.LoadViewModel(new ReferenceViewModel(service));
                form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, typeof(ReferenceForm).GetMethod("ReferenceForm_Load", BindingFlags.Instance | BindingFlags.NonPublic)!);
                form.Shown += async (_, _) =>
                {
                    try
                    {
                        typeof(ReferenceForm).GetMethod("BindReferenceDataDefinitionTypes", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
                        Field<ComboBox>(form, "ddlReferenceDataSelector").SelectedItem = "trade strategy families";
                        await Wait(() => Field<Panel>(form, "pnlMarketData").Controls.OfType<StrategyCatalogReferenceView>().FirstOrDefault()?.CanChangeRemove == true);
                        var view = Field<Panel>(form, "pnlMarketData").Controls.OfType<StrategyCatalogReferenceView>().Single();
                        Field<ListBox>(view, "list").Items.Cast<object>().Select(x => x.ToString()!.Split("  [")[0]).Should().Equal("Futures", "Vertical Spreads", "Iron Condor");
                        Field<Button>(form, "btnChange").PerformClick(); view.IsEditing.Should().BeTrue();
                        Field<Button>(form, "btnClose").Text.Should().Be("Cancel");
                        form.Refresh();
                        if (Environment.GetEnvironmentVariable("IFM_REFERENCE_UI_RENDER_DIR") is { Length: > 0 } directory)
                        {
                            Directory.CreateDirectory(directory); using var bitmap = new Bitmap(form.Width, form.Height);
                            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(directory, "configuration-strategy-catalog.png"));
                        }
                        Field<Button>(form, "btnClose").PerformClick(); view.IsEditing.Should().BeFalse(); form.Visible.Should().BeTrue();
                        Field<Button>(form, "btnClose").Text.Should().Be("Close"); Field<Button>(form, "btnClose").PerformClick();
                    }
                    catch (Exception ex) { done.TrySetException(ex); form.Close(); }
                };
                form.ShowDialog(); done.TrySetResult();
            }
            catch (Exception ex) { done.TrySetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); await done.Task.WaitAsync(TimeSpan.FromSeconds(20));
        thread.Join(TimeSpan.FromSeconds(3)).Should().BeTrue();
    }

    sealed class CatalogFixture
    {
        public IReferenceQueryApi Queries { get; } = Substitute.For<IReferenceQueryApi>();
        public IReferenceCommandApi Commands { get; } = Substitute.For<IReferenceCommandApi>();
        public Dictionary<CatalogKey, StoredStrategyCatalogDefinition> Rows { get; } = [];
        public List<CatalogCommandRequest> Requests { get; } = [];
        public bool Fail;
        public CatalogFixture()
        {
            foreach (var definition in StrategyCatalogDefaults.Create().Where(x => x.Key.Kind == StrategyCatalogKind.Family).Append(StrategyCatalogExamples.Create()[0]))
                Rows.Add(definition.Key, new(definition, "hash", CatalogLifecycleStatus.Draft, DateTime.UtcNow, "test", null, null, null, null));
            Queries.QueryStrategyCatalogAsync(Arg.Any<CatalogQueryRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var query = call.Arg<CatalogQueryRequest>();
                return new ServiceOk<string>(query.Operation == CatalogQueryOperation.Exact ? StrategyCatalogJson.Write(Rows.GetValueOrDefault(query.Key!)) :
                    StrategyCatalogJson.Write(Rows.Values.Where(x => x.Definition.Key.Kind == query.Kind).GroupBy(x => x.Definition.Key.Id).Select(x => x.MaxBy(r => r.Definition.Key.Version)!).Select(x => new StrategyCatalogSummary(x.Definition.Key, x.Definition.Code, x.Definition.Name, x.Status, x.ContentHash)).ToArray()));
            });
            Queries.GetTradeStrategySymbolsAsync(Arg.Any<TradeStrategyFamilyType>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategySymbolReadModel[]>([new() { Id = 101, Symbol = "ES", Exchange = "XCME", Currency = "USD", Description = "ES" }]));
            Commands.ExecuteStrategyCatalogAsync(Arg.Any<CatalogCommandRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var request = call.Arg<CatalogCommandRequest>(); Requests.Add(request);
                if (Fail) return new ServiceFailed<Guid>(503, "offline");
                Rows[request.Definition!.Key] = new(request.Definition, "hash", CatalogLifecycleStatus.Draft, DateTime.UtcNow, "test", null, null, null, null);
                return (ServiceResult<Guid>)new ServiceOk<Guid>(request.OperationId);
            });
        }
    }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    static async Task Wait(Func<bool> condition)
    { var until = DateTime.UtcNow.AddSeconds(5); while (!condition()) { if (DateTime.UtcNow > until) throw new TimeoutException("Catalog did not load."); await Task.Delay(20); } }
}
