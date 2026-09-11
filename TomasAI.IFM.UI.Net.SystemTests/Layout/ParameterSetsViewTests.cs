using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Windows.Forms;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Views.Reference.ParameterSets;
namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class ParameterSetsViewTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily,26)]
    [InlineData(TimeFrameType.Weekly,34)]
    [InlineData(TimeFrameType.Monthly,26)]
    public void Add_uses_the_shared_edit_flow_and_binds_the_selected_horizon(TimeFrameType horizon,int count)
    {
        var api=Api();
        using var host=new Form{Width=1600,Height=900,ShowInTaskbar=false,Opacity=0};
        using var view=new ParameterSetsReferenceView(api){Dock=DockStyle.Fill};
        host.Controls.Add(view);host.Show();System.Windows.Forms.Application.DoEvents();
        var command=(IControlCommand)view;
        command.Load(null!,_=>{});PumpUntil(()=>!view.IsBusy);

        var all=Descendants(view).ToArray();
        var grid=all.OfType<DataGridView>().Single(x=>x.AccessibleName=="Regime Discovery signal requirements");
        var observationGrid=all.OfType<DataGridView>().Single(x=>x.AccessibleName=="Regime Discovery observation requirements");
        grid.ReadOnly.Should().BeTrue();
        observationGrid.ReadOnly.Should().BeTrue();
        all.OfType<TabControl>().Single().TabPages.Cast<TabPage>().Select(page=>page.Text)
            .Should().ContainInOrder("Signals","Observations","Calculation parameters","Validation");
        grid.Columns.Cast<DataGridViewColumn>().Single(column=>column.DataPropertyName=="Metric")
            .Should().BeOfType<DataGridViewComboBoxColumn>().Which.DataSource.Should().BeAssignableTo<object[]>()
            .Which.Should().BeEquivalentTo(Enum.GetValues<SignalMetricsType>().Where(value=>value!=SignalMetricsType.Unknown));
        grid.Columns.Cast<DataGridViewColumn>().Single(column=>column.DataPropertyName=="TimeFrame")
            .Should().BeOfType<DataGridViewComboBoxColumn>();
        grid.Columns.Cast<DataGridViewColumn>().Should().Contain(column=>column.DataPropertyName=="PeriodLength");
        observationGrid.Columns.Cast<DataGridViewColumn>().Single(column=>column.DataPropertyName=="Metric")
            .Should().BeOfType<DataGridViewComboBoxColumn>().Which.DataSource.Should().BeAssignableTo<object[]>()
            .Which.Should().BeEquivalentTo(Enum.GetValues<ObservationMetricsType>().Where(value=>value!=ObservationMetricsType.Unknown));
        all.OfType<Label>().Should().Contain(x=>x.Text=="Parameter Set");
        var split=all.OfType<SplitContainer>().Single(x=>x.AccessibleName=="Parameter set master detail layout");
        split.Orientation.Should().Be(Orientation.Vertical);
        Descendants(split.Panel1).OfType<ListBox>().Select(x=>x.AccessibleName).Should().BeEquivalentTo("Parameter Set","Parameter set components");
        Descendants(split.Panel2).OfType<ListBox>().Single(x=>x.AccessibleName=="Parameter set versions").AccessibleName.Should().Be("Parameter set versions");
        Descendants(split.Panel2).OfType<DataGridView>().Should().Contain(x=>x.AccessibleName=="Regime Discovery signal requirements");
        var master=all.OfType<ListBox>().Single(x=>x.AccessibleName=="Parameter Set");
        master.GetItemText(master.SelectedItem).Should().Be("Strategy Workflow");
        all.OfType<Label>().Single(x=>x.AccessibleName=="Parameter detail list title").Text.Should().Be("Strategy Workflow");
        var componentList=all.OfType<ListBox>().Single(x=>x.AccessibleName=="Parameter set components");
        componentList.GetItemText(componentList.SelectedItem).Should().Be("Regime Discovery");
        all.OfType<Label>().Single(x=>x.AccessibleName=="Parameter editor title").Text.Should().Be("Regime Discovery");

        var selector=all.OfType<ComboBox>().Single(x=>x.AccessibleName=="New parameter set horizon");
        selector.SelectedItem=horizon;
        var callback=true;
        command.Add(value=>callback=value);PumpUntil(()=>view.IsAdding&&!view.IsBusy);

        callback.Should().BeFalse();
        grid.ReadOnly.Should().BeFalse();
        observationGrid.ReadOnly.Should().BeFalse();
        grid.Rows.Cast<DataGridViewRow>().Count(row=>!row.IsNewRow).Should().Be(count);
        observationGrid.Rows.Cast<DataGridViewRow>().Count(row=>!row.IsNewRow).Should().Be(3);
        grid.AllowUserToAddRows.Should().BeTrue();observationGrid.AllowUserToAddRows.Should().BeTrue();
        var calculationGroups=all.OfType<ListBox>().Single(x=>x.AccessibleName=="Calculation parameter groups");
        calculationGroups.Items.Cast<string>().Should().Equal("Trend","Volatility","Market Structure","Fusion","Freshness","Data Quality");
        calculationGroups.SelectedItem.Should().Be("Trend");
        all.OfType<SplitContainer>().Single(x=>x.AccessibleName=="Calculation parameter group layout").Orientation.Should().Be(Orientation.Vertical);
        all.OfType<Label>().Single(x=>x.AccessibleName=="Calculation parameter group title").Text.Should().Be("Trend");
        var calculationGrid=all.OfType<DataGridView>().Single(x=>x.AccessibleName=="Regime Discovery calculation parameters");
        calculationGrid.ReadOnly.Should().BeFalse();
        calculationGrid.Columns.Cast<DataGridViewColumn>().Select(x=>x.DataPropertyName).Should().Equal("Name","Value");
        calculationGrid.Rows.Cast<DataGridViewRow>().Where(row=>!row.IsNewRow).Should().OnlyContain(row=>
            ((ParameterSetsReferenceView.FieldRow)row.DataBoundItem!).Group=="Trend"&&!row.Cells[0].Value!.ToString()!.Contains('/'));
        var editedCalculation=(ParameterSetsReferenceView.FieldRow)calculationGrid.Rows[0].DataBoundItem!;
        var editedPath=editedCalculation.Path;editedCalculation.Value="0.123";
        calculationGroups.SelectedItem="Volatility";System.Windows.Forms.Application.DoEvents();
        calculationGrid.Rows.Cast<DataGridViewRow>().Where(row=>!row.IsNewRow).Should().OnlyContain(row=>
            ((ParameterSetsReferenceView.FieldRow)row.DataBoundItem!).Group=="Volatility");
        calculationGroups.SelectedItem="Trend";System.Windows.Forms.Application.DoEvents();
        calculationGrid.Rows.Cast<DataGridViewRow>().Where(row=>!row.IsNewRow)
            .Select(row=>(ParameterSetsReferenceView.FieldRow)row.DataBoundItem!).Single(row=>row.Path==editedPath).Value.Should().Be("0.123");
        all.OfType<TextBox>().Single(x=>x.AccessibleName=="Parameter set name").Text.Should().Be($"Regime Discovery {horizon}");
        all.OfType<Label>().Single(x=>x.AccessibleName=="Parameter editor title").Text.Should().Be("Regime Discovery");
        all.OfType<Button>().Should().Contain(x=>x.Text=="Intervals");
        all.OfType<Button>().Should().NotContain(x=>x.Text=="New signal set"||x.Text=="Edit as new draft"||x.Text=="Save draft"||x.Text=="Rename"||x.Text=="Retire");
        api.DidNotReceive().CreateAsync(Arg.Any<CreateParameterSetCommand>(),Arg.Any<CancellationToken>());

        var target=Environment.GetEnvironmentVariable("IFM_PARAMETERSETS_UI_EVIDENCE_DIR");
        if(!string.IsNullOrWhiteSpace(target))
        {
            Directory.CreateDirectory(target);host.PerformLayout();view.PerformLayout();
            using var bitmap=new Bitmap(view.Width,view.Height);
            view.DrawToBitmap(bitmap,new Rectangle(Point.Empty,view.Size));
            Enumerable.Range(0,bitmap.Width/20).SelectMany(x=>Enumerable.Range(0,bitmap.Height/20).Select(y=>bitmap.GetPixel(x*20,y*20).ToArgb())).Distinct().Count().Should().BeGreaterThan(3);
            bitmap.Save(Path.Combine(target,$"parameter-sets-{horizon}.png"),ImageFormat.Png);
        }

        command.Close(_=>{}).Should().BeFalse();
        view.IsEditing.Should().BeFalse();
        grid.ReadOnly.Should().BeTrue();
    }

    [Fact]
    public void Change_opens_an_editable_copy_without_saving_or_allocating_a_version()
    {
        var api=Api();var id=Guid.NewGuid();var seed=RegimeDiscoveryParameterModel.CreateExplicitSeed(id,TimeFrameType.Daily);
        var saved=new ParameterSetVersion(new(id,1,RegimeDiscoveryParameterModel.ComponentCode,new string('a',64)),"Regime Discovery Daily","",seed.SchemaVersion,ParameterVersionStatus.Draft,JsonSerializer.Serialize(seed),DateTime.UtcNow,"test",CatalogRevision:3);
        api.VersionsAsync(Arg.Any<Guid?>(),Arg.Any<CancellationToken>(),Arg.Any<string>()).Returns(new ServiceOk<ParameterSetVersion[]>([saved]));
        api.PreviewAsync(id,Arg.Any<CancellationToken>(),(int)TimeFrameType.Daily,Arg.Any<string?>(),Arg.Any<bool>()).Returns(new ServiceOk<string>(JsonSerializer.Serialize(seed)));
        using var host=new Form{Width=1600,Height=900,ShowInTaskbar=false,Opacity=0};
        using var view=new ParameterSetsReferenceView(api){Dock=DockStyle.Fill};host.Controls.Add(view);host.Show();System.Windows.Forms.Application.DoEvents();
        var command=(IControlCommand)view;command.Load(null!,_=>{});PumpUntil(()=>!view.IsBusy&&view.CanChange);

        var all=Descendants(view).ToArray();
        var master=all.OfType<ListBox>().Single(x=>x.AccessibleName=="Parameter Set");
        var childTitle=all.OfType<Label>().Single(x=>x.AccessibleName=="Parameter detail list title");
        childTitle.Text.Should().Be(master.GetItemText(master.SelectedItem));
        all.OfType<Label>().Single(x=>x.AccessibleName=="Parameter editor title").Text.Should().Be("Regime Discovery");
        all.OfType<DataGridView>().Single(x=>x.AccessibleName=="Regime Discovery signal requirements").ReadOnly.Should().BeTrue();

        var callback=true;command.Change(value=>callback=value);PumpUntil(()=>view.IsChanging&&!view.IsBusy);
        callback.Should().BeFalse();
        all.OfType<DataGridView>().Single(x=>x.AccessibleName=="Regime Discovery signal requirements").ReadOnly.Should().BeFalse();
        api.DidNotReceive().SaveAsync(Arg.Any<SaveParameterDraftCommand>(),Arg.Any<CancellationToken>());
        command.Close(_=>{}).Should().BeFalse();
    }

    [Fact]
    public void Navigation_groups_registered_components_by_application_area()
    {
        var api=Api();
        api.ComponentsAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<ParameterComponentSummary[]>(
        [
            new RegimeDiscoveryParameterModel().Summary,
            new("strategy-workflow","Strategy Workflow","strategy-workflow.trade-selection","Trade Selection",[1],false),
            new("risk-management","Risk Management","risk-management.limits","Risk Limits",[1],false)
        ]));
        using var host=new Form{Width=1600,Height=900,ShowInTaskbar=false,Opacity=0};
        using var view=new ParameterSetsReferenceView(api){Dock=DockStyle.Fill};host.Controls.Add(view);host.Show();System.Windows.Forms.Application.DoEvents();
        ((IControlCommand)view).Load(null!,_=>{});PumpUntil(()=>!view.IsBusy);
        var all=Descendants(view).ToArray();var master=all.OfType<ListBox>().Single(x=>x.AccessibleName=="Parameter Set");
        var children=all.OfType<ListBox>().Single(x=>x.AccessibleName=="Parameter set components");
        master.Items.Cast<object>().Select(master.GetItemText).Should().Equal("Strategy Workflow","Risk Management");
        children.Items.Cast<object>().Select(children.GetItemText).Should().Equal("Regime Discovery","Trade Selection");

        master.SelectedIndex=1;PumpUntil(()=>!view.IsBusy&&children.Items.Count==1);
        all.OfType<Label>().Single(x=>x.AccessibleName=="Parameter detail list title").Text.Should().Be("Risk Management");
        children.GetItemText(children.SelectedItem).Should().Be("Risk Limits");
        all.OfType<Label>().Single(x=>x.AccessibleName=="Parameter editor title").Text.Should().Be("Risk Limits");
        view.CanAdd.Should().BeFalse("the server reported that no editor is registered for this component");
    }
    static IParameterSetsApi Api()
    {
        var api=Substitute.For<IParameterSetsApi>();
        api.ComponentsAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<ParameterComponentSummary[]>([new RegimeDiscoveryParameterModel().Summary]));
        api.VersionsAsync(Arg.Any<Guid?>(),Arg.Any<CancellationToken>(),Arg.Any<string>()).Returns(new ServiceOk<ParameterSetVersion[]>([]));
        api.PreviewAsync(Arg.Any<Guid>(),Arg.Any<CancellationToken>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<bool>())
            .Returns(call=>new ServiceOk<string>(JsonSerializer.Serialize(RegimeDiscoveryParameterModel.CreateExplicitSeed(call.ArgAt<Guid>(0),(TimeFrameType)call.ArgAt<int>(2)))));
        return api;
    }
    static void PumpUntil(Func<bool> condition)
    {
        var end=DateTime.UtcNow.AddSeconds(5);
        while(!condition()&&DateTime.UtcNow<end){System.Windows.Forms.Application.DoEvents();Thread.Sleep(10);}
        condition().Should().BeTrue("the asynchronous UI operation should complete");
    }
    static IEnumerable<Control> Descendants(Control root)
    {
        foreach(Control child in root.Controls)
        {
            yield return child;
            foreach(var nested in Descendants(child))yield return nested;
        }
    }
}

