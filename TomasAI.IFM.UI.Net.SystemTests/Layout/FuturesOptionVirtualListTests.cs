using System.Drawing;
using System.Windows.Forms;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Views.MarketData;
using TomasAI.IFM.UI.Net.Views.Presentation;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class FuturesOptionVirtualListTests
{
    [Fact]
    public async Task Real_editor_renders_first_page_and_scroll_loads_next_without_losing_selection()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, true);
            using var context = new ApplicationContext();
            using var dispatcher = new Control();
            _ = dispatcher.Handle;
            dispatcher.BeginInvoke((Action)(async () =>
            {
                try { await VerifyEditorAsync(); completion.SetResult(); }
                catch (Exception error) { completion.SetException(error); }
                finally { context.ExitThread(); }
            }));
            System.Windows.Forms.Application.Run(context);
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    static async Task VerifyEditorAsync()
    {
        var app = Substitute.For<IAppRoot>();
        var reference = Substitute.For<IReferenceDataService>();
        foreach (var (category, code) in new[] { ("Symbol", "ES"), ("SecurityType", "FOP"), ("Currency", "USD"),
                     ("Exchange", "CME"), ("Multiplier", "50"), ("OptionType", "Call") })
        {
            reference.GetLookupTypesAsync(category, Arg.Any<CancellationToken>()).Returns(
                UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success([
                    new(category, code, 0, code, DateTime.UtcNow, "test")]));
        }
        var api = Substitute.For<IMarketDataQueryApi>();
        var contracts = Enumerable.Range(0, 205).Select(i => new FuturesOptionContractReadModel(
            $"ES20260918C{4000 + i}", "Option contract", "ES", $"ES {4000 + i}", "FOP", "USD", "CME", "50",
            new DateOnly(2026, 9, 18), 4000 + i, "Call")).ToArray();
        var nextPage = new TaskCompletionSource<ServiceResult<FuturesOptionContractPageReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<GetFuturesOptionContractsPageParameter>().ContinuationToken is null
                ? Task.FromResult<ServiceResult<FuturesOptionContractPageReadModel>>(new ServiceOk<FuturesOptionContractPageReadModel>(new(contracts[..200], "next")))
                : nextPage.Task);
        app.Services.MarketDataQueries.Returns(new MarketDataQueryService(api, Substitute.For<IMarketDataFeedQueryApi>()));
        app.Services.MarketDataCommands.Returns(new MarketDataCommandService(Substitute.For<IMarketDataCommandApi>()));
        app.Services.MarketDataEvents.Returns(new MarketDataEventService(Substitute.For<IMarketDataUIEventConsumer>()));
        await using var vm = new FuturesOptionContractEditorViewModel(app, reference);
        var marketVm = new MarketDataViewModel(reference);
        using var editor = new FuturesOptionContractEditorControl(vm, marketVm);
        using var host = new DarkTradingForm { ClientSize = new Size(1100, 500), ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = Point.Empty, Opacity = 0 };
        editor.Dock = DockStyle.Fill;
        host.Controls.Add(editor);
        host.Show();
        var loaded = false;
        ((IControlCommand)editor).Load(app, success => loaded = success);
        await WaitUntilAsync(() => loaded);
        var list = (ListView)editor.Controls.Find("lstFuturesOptionContractIds", true).Single();
        Assert.True(list.VirtualMode);
        Assert.Equal(201, list.VirtualListSize);
        Assert.Equal(Color.Black.ToArgb(), list.BackColor.ToArgb());
        Assert.Equal(200, vm.FuturesOptionContracts.Count);
        Assert.Equal(contracts[0].ContractId, list.Items[0].Text);
        Assert.True(editor.CanChangeRemove);
        await api.Received(1).GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>());

        list.SelectedIndices.Clear();
        list.SelectedIndices.Add(190);
        list.EnsureVisible(199);
        await WaitUntilAsync(() => vm.LoadMoreOperation.IsRunning);
        Assert.True(list.Enabled);
        Assert.False(marketVm.IsEditorBusy);
        list.SelectedIndices.Clear();
        list.SelectedIndices.Add(200); // The loading row is not an editable contract.
        Assert.False(editor.CanChangeRemove);
        list.SelectedIndices.Clear();
        list.SelectedIndices.Add(190);
        Assert.True(editor.CanChangeRemove);
        Assert.Equal(200, vm.FuturesOptionContracts.Count);
        nextPage.SetResult(new ServiceOk<FuturesOptionContractPageReadModel>(new(contracts[200..], null)));
        await WaitUntilAsync(() => list.VirtualListSize == 205);
        Assert.Equal(190, list.SelectedIndices[0]);
        Assert.Equal(contracts[204].ContractId, list.Items[204].Text);
        Assert.False(vm.HasMoreContracts);
        await api.Received(2).GetFuturesOptionContractsPageAsync(Arg.Any<GetFuturesOptionContractsPageParameter>(), Arg.Any<CancellationToken>());
        await api.DidNotReceive().GetFuturesOptionContractsAsync(Arg.Any<string>());
        using var bitmap = new Bitmap(host.Width, host.Height);
        host.DrawToBitmap(bitmap, new Rectangle(Point.Empty, host.Size));
        var directory = Environment.GetEnvironmentVariable("IFM_OPTION_PAGING_RENDER_DIR");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            bitmap.Save(Path.Combine(directory, "FuturesOptionVirtualList.png"));
        }
        await ((IAsyncFormControl)editor).CloseAsync();
    }

    static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!predicate())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Virtual editor did not reach the expected state.");
            await Task.Delay(15);
        }
    }
}
