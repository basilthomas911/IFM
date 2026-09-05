using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class TradeFamilyCatalogUiTests
{
    static TradeStrategyFamilyReadModel[] Catalog() => TradeStrategyFamilySeed.Definitions
        .Select((x, i) => x.Create(71 + i, DateTime.UtcNow, "test")).ToArray();
    static PortfolioReadModel Portfolio() => new() { PortfolioId = 1, PortfolioVersion = 1 };
    [Fact]
    public void Duplicate_system_keys_are_disambiguated_by_exact_id_version_and_not_legacy_text()
    {
        var es = Catalog()[1];
        var nq = es with { TradeStrategyFamilyId = 99, Symbol = "NQ", Description = "Weekly NQ spread" };
        var fund = Fund(es.SystemKey) with { SchemaVersion = 2, PermittedTradeStrategyFamilies = [TradeStrategyFamilyReference.From(nq)] };
        using var mandate = new FundMandateEditorForm(1, 2, fund, [es, nq]);
        InvokeSave(mandate);
        mandate.Value!.PermittedTradeStrategyFamilies.Should().Equal(TradeStrategyFamilyReference.From(nq));
        using var assignment = new FundAssignmentEditorForm(Portfolio(), fund, [es, nq]);
        Field<ComboBox>(assignment, "_family").Items.Count.Should().Be(1);
        PopulateAssignment(assignment); InvokeSave(assignment);
        assignment.Value!.TradeStrategyFamily.Should().Be(TradeStrategyFamilyReference.From(nq));
        using var legacy = new FundMandateEditorForm(1, 2, Fund(es.SystemKey), [es, nq]);
        InvokeSave(legacy); legacy.Value.Should().BeNull("ambiguous legacy names must be explicitly reselected");
        using var legacyAssignment = new FundAssignmentEditorForm(Portfolio(), Fund(es.SystemKey), [es, nq]);
        Field<ComboBox>(legacyAssignment, "_family").Items.Count.Should().Be(0);
    }
    static FundMandateReadModel Fund(params string[] families) => new()
    {
        PortfolioId = 1, FundId = 2, FundMandateVersion = 1, TradingYear = 2026,
        FundCode = "ES", Name = "ES Fund", Objective = "Test", OperatingState = FundOperatingState.Draft,
        DecisionHorizon = "Weekly", UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["FuturesOption"],
        PermittedTradeFamilies = families, CreatedOnUtc = DateTime.UtcNow, CreatedBy = "test"
    };

    [Fact]
    public void Mandate_displays_catalog_descriptions_restores_checked_keys_and_saves_multiple_system_keys()
    {
        var catalog = Catalog();
        using var form = new FundMandateEditorForm(1, 2, Fund(catalog[1].SystemKey), catalog);
        var list = Field<CheckedListBox>(form, "_families");
        list.CheckOnClick.Should().BeTrue();
        list.BackColor.Should().Be(Color.Black);
        list.ForeColor.Should().Be(Color.White);
        list.Items.Count.Should().Be(3);
        Keys(list.CheckedItems).Should().Equal(catalog[1].SystemKey);
        list.Items.Cast<object>().Select(x => x.ToString()).Should().Contain(x => x!.Contains(catalog[1].Description));
        list.SetItemChecked(IndexOf(list, catalog[2].SystemKey), true);
        InvokeSave(form);
        form.Value.Should().NotBeNull();
        form.Value!.PermittedTradeFamilies.Should().BeEquivalentTo(catalog[1].SystemKey, catalog[2].SystemKey);
        form.Value.FundMandateVersion.Should().Be(2);
    }

    [Fact]
    public void New_mandate_does_not_implicitly_permit_all_catalog_families()
    {
        using var form = new FundMandateEditorForm(1, 2, catalog: Catalog());
        Field<CheckedListBox>(form, "_families").CheckedItems.Count.Should().Be(0);
        InvokeSave(form);
        form.Value.Should().BeNull();
        Field<Label>(form, "_error").Text.Should().Contain("Select at least one");
    }

    [Theory]
    [InlineData("FUTURES")]
    [InlineData("arbitrary text")]
    [InlineData("futures-futures")]
    public void Unresolved_existing_family_remains_visible_and_blocks_save_until_explicitly_removed(string oldKey)
    {
        var catalog = Catalog();
        using var form = new FundMandateEditorForm(1, 2, Fund(catalog[1].SystemKey, oldKey), catalog);
        var list = Field<CheckedListBox>(form, "_families");
        Keys(list.CheckedItems).Should().Contain(oldKey);
        list.Items[IndexOf(list, oldKey)].ToString().Should().Contain("Unavailable");
        InvokeSave(form);
        form.Value.Should().BeNull();
        list.SetItemChecked(IndexOf(list, oldKey), false);
        InvokeSave(form);
        form.Value!.PermittedTradeFamilies.Should().Equal(catalog[1].SystemKey);
    }

    [Fact]
    public void Retired_family_is_not_offered_as_a_new_permission_and_cannot_be_saved_if_previously_checked()
    {
        var catalog = Catalog();
        catalog[0] = catalog[0] with { State = TradeStrategyFamilyState.Retired };
        using var fresh = new FundMandateEditorForm(1, 2, catalog: catalog);
        Keys(Field<CheckedListBox>(fresh, "_families").Items).Should().NotContain(catalog[0].SystemKey);
        using var existing = new FundMandateEditorForm(1, 2, Fund(catalog[0].SystemKey), catalog);
        InvokeSave(existing);
        existing.Value.Should().BeNull();
    }

    [Fact]
    public void Assignment_is_a_non_editable_dropdown_limited_to_active_permitted_families_and_saves_key()
    {
        var catalog = Catalog();
        catalog[2] = catalog[2] with { State = TradeStrategyFamilyState.Retired };
        using var form = new FundAssignmentEditorForm(Portfolio(), Fund(catalog[1].SystemKey, catalog[2].SystemKey, "UNKNOWN"), catalog);
        var combo = Field<ComboBox>(form, "_family");
        combo.DropDownStyle.Should().Be(ComboBoxStyle.DropDownList);
        Keys(combo.Items).Should().Equal(catalog[1].SystemKey);
        combo.SelectedIndex.Should().Be(0);
        PopulateAssignment(form);
        InvokeSave(form);
        form.Value.Should().NotBeNull();
        form.Value!.TradeFamily.Should().Be(catalog[1].SystemKey);
    }

    [Fact]
    public void Multiple_permitted_families_require_explicit_assignment_choice()
    {
        var catalog = Catalog();
        using var form = new FundAssignmentEditorForm(Portfolio(), Fund(catalog[0].SystemKey, catalog[1].SystemKey), catalog);
        var combo = Field<ComboBox>(form, "_family");
        combo.Items.Count.Should().Be(2);
        combo.SelectedIndex.Should().Be(-1);
        PopulateAssignment(form);
        combo.Text = "invented family";
        InvokeSave(form);
        form.Value.Should().BeNull();
        combo.SelectedIndex = 1;
        InvokeSave(form);
        form.Value!.TradeFamily.Should().Be(Key(combo.SelectedItem!));
    }

    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("FUTURES")]
    public void Assignment_does_not_fall_back_to_an_unpermitted_family(string unknown)
    {
        using var form = new FundAssignmentEditorForm(Portfolio(), Fund(unknown), Catalog());
        Field<ComboBox>(form, "_family").Items.Count.Should().Be(0);
        PopulateAssignment(form);
        InvokeSave(form);
        form.Value.Should().BeNull();
    }

    [Fact]
    public void Missing_catalog_does_not_fall_back_to_hard_coded_seeds()
    {
        using var mandate = new FundMandateEditorForm(1, 2, Fund("Futures-Futures"));
        InvokeSave(mandate);
        mandate.Value.Should().BeNull();
        using var assignment = new FundAssignmentEditorForm(Portfolio(), Fund("Futures-Futures"));
        Field<ComboBox>(assignment, "_family").Items.Count.Should().Be(0);
        InvokeSave(assignment);
        assignment.Value.Should().BeNull();
    }

    [Fact]
    public async Task Administration_queries_fresh_catalog_and_returns_only_active_rows()
    {
        var catalog = Catalog();
        catalog[2] = catalog[2] with { State = TradeStrategyFamilyState.Retired };
        var references = Substitute.For<IReferenceQueryApi>();
        references.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategyFamilyReadModel[]>(catalog));
        using var form = Administration(references);
        (await LoadCatalog(form))!.Select(x => x.SystemKey).Should().BeEquivalentTo(catalog.Take(2).Select(x => x.SystemKey));
        await LoadCatalog(form);
        await references.Received(2).GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("throws")]
    [InlineData("empty")]
    [InlineData("retired")]
    [InlineData("invalid")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    public async Task Administration_blocks_unavailable_or_ambiguous_catalogs(string scenario)
    {
        var references = Substitute.For<IReferenceQueryApi>();
        var catalog = Catalog();
        switch (scenario)
        {
            case "failed": references.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(new ServiceFailed<TradeStrategyFamilyReadModel[]>(503, "offline")); break;
            case "throws": references.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<ServiceResult<TradeStrategyFamilyReadModel[]>>(new InvalidOperationException("offline"))); break;
            default:
                catalog = scenario switch
                {
                    "empty" => [],
                    "retired" => catalog.Select(x => x with { State = TradeStrategyFamilyState.Retired }).ToArray(),
                    "invalid" => [catalog[0] with { SystemKey = "BAD" }],
                    "duplicate" => [catalog[0], catalog[0]],
                    _ => catalog
                };
                references.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategyFamilyReadModel[]>(catalog));
                break;
        }
        using var form = Administration(scenario == "missing" ? null : references);
        (await LoadCatalog(form)).Should().BeNull();
        Field<Label>(form, "_status").Text.Should().NotBeNullOrWhiteSpace().And.NotBe("Trade strategy family catalog loaded.");
    }

    static void PopulateAssignment(FundAssignmentEditorForm form)
    {
        foreach (var field in new[] { "_template", "_hint", "_composition" }) Field<TextBox>(form, field).Text = Guid.NewGuid().ToString();
    }
    static PortfolioAdministrationForm Administration(IReferenceQueryApi? references)
    {
        var form = new PortfolioAdministrationForm();
        form.GetType().GetField("_referenceQueries", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, references);
        return form;
    }
    static Task<TradeStrategyFamilyReadModel[]?> LoadCatalog(PortfolioAdministrationForm form) =>
        (Task<TradeStrategyFamilyReadModel[]?>)form.GetType().GetMethod("LoadTradeFamilyCatalogAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
    static void InvokeSave(Form form) => form.GetType().GetMethod(form is FundMandateEditorForm ? "Save" : "SaveCore", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    static string Key(object item) => (string)item.GetType().GetProperty("SystemKey")!.GetValue(item)!;
    static string[] Keys(System.Collections.IEnumerable items) => items.Cast<object>().Select(Key).ToArray();
    static int IndexOf(CheckedListBox list, string key) => Array.FindIndex(Keys(list.Items), x => x == key);
}
