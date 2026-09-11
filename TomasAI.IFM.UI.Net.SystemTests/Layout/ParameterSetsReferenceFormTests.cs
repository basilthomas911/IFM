using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.Views.Reference;
using TomasAI.IFM.UI.Net.Views.Reference.ParameterSets;
namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class ParameterSetsReferenceFormTests
{
    [Fact]
    public void Local_reference_editors_remain_available_while_remote_catalogue_is_pending()
    {
        var pending = new TaskCompletionSource<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Substitute.For<IReferenceDataService>();
        service.GetReferenceDataDefinitionTypesAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<UiOperationResult<IReadOnlyList<LookupTypeUiModel>>>(pending.Task));

        using var form = new ReferenceForm(Substitute.For<IAppRoot>(), service, Substitute.For<IParameterSetsApi>());
        var model = new ReferenceViewModel(service);
        form.LoadViewModel(model);
        form.Show();
        PumpUntil(() => Selector(form).Items.Count == 2);

        var selector = Selector(form);
        selector.Items.Cast<object>().Select(item => item.ToString()).Should().Equal(
            "trade strategy families",
            "parameter sets");
        selector.Enabled.Should().BeTrue();
        selector.SelectedIndex.Should().Be(-1);

        pending.SetResult(UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success([]));
        PumpUntil(() => !model.LoadReferenceDataDefinitionTypesOperation.IsRunning);
    }
    [Fact]
    public void Shared_reference_buttons_drive_add_save_and_cancel_states()
    {
        var service=Substitute.For<IReferenceDataService>();
        service.GetReferenceDataDefinitionTypesAsync(Arg.Any<CancellationToken>()).Returns(
            UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success([new("Reference","LookupTypes",1,"lookup types",DateTime.UtcNow,"test")]));
        service.GetLookupTypesAsync(Arg.Any<CancellationToken>()).Returns(UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success([]));
        service.GetLookupTypeNamesAsync(Arg.Any<CancellationToken>()).Returns(UiOperationResult<IReadOnlyList<string>>.Success([]));
        service.GetLookupTypeShortCodesAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<IReadOnlyList<LookupTypeShortCodeUiModel>>.Success([]));
        var api=Substitute.For<IParameterSetsApi>();
        api.ComponentsAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<ParameterComponentSummary[]>([new RegimeDiscoveryParameterModel().Summary]));
        api.VersionsAsync(Arg.Any<Guid?>(),Arg.Any<CancellationToken>(),Arg.Any<string>()).Returns(new ServiceOk<ParameterSetVersion[]>([]));
        api.PreviewAsync(Arg.Any<Guid>(),Arg.Any<CancellationToken>(),Arg.Any<int>(),Arg.Any<string?>(),Arg.Any<bool>())
            .Returns(call=>new ServiceOk<string>(System.Text.Json.JsonSerializer.Serialize(RegimeDiscoveryParameterModel.CreateExplicitSeed(call.ArgAt<Guid>(0),(Domain.MarketData.Analytics.Shared.TimeFrameType)call.ArgAt<int>(2)))));

        using var form=new ReferenceForm(Substitute.For<IAppRoot>(),service,api);
        form.LoadViewModel(new ReferenceViewModel(service));form.Show();PumpUntil(()=>Selector(form).Items.Count>=3);
        var selector=Selector(form);selector.Items.Cast<object>().Select(x=>x.ToString()).Should().Contain("parameter sets");
        selector.SelectedItem="parameter sets";PumpUntil(()=>Field<Panel>(form,"pnlMarketData").Controls.OfType<ParameterSetsReferenceView>().Any());
        var view=Field<Panel>(form,"pnlMarketData").Controls.OfType<ParameterSetsReferenceView>().Single();PumpUntil(()=>!view.IsBusy&&view.CanAdd);
        var add=Field<Button>(form,"btnAdd");var change=Field<Button>(form,"btnChange");var remove=Field<Button>(form,"btnRemove");var close=Field<Button>(form,"btnClose");
        add.Text.Should().Be("&Add");change.Enabled.Should().BeFalse();remove.Enabled.Should().BeFalse();close.Text.Should().Be("Close");

        add.PerformClick();PumpUntil(()=>view.IsAdding&&!view.IsBusy);
        add.Text.Should().Be("Save");change.Enabled.Should().BeFalse();remove.Enabled.Should().BeFalse();close.Text.Should().Be("Cancel");selector.Enabled.Should().BeFalse();
        api.DidNotReceive().CreateAsync(Arg.Any<CreateParameterSetCommand>(),Arg.Any<CancellationToken>());

        close.PerformClick();PumpUntil(()=>!view.IsEditing);
        add.Text.Should().Be("&Add");close.Text.Should().Be("Close");selector.Enabled.Should().BeTrue();
    }
    static ComboBox Selector(ReferenceForm form)=>Field<ComboBox>(form,"ddlReferenceDataSelector");
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    static void PumpUntil(Func<bool> condition)
    {
        var end=DateTime.UtcNow.AddSeconds(5);
        while(!condition()&&DateTime.UtcNow<end){System.Windows.Forms.Application.DoEvents();Thread.Sleep(10);}
        condition().Should().BeTrue("the asynchronous Reference Data UI operation should complete");
    }
}