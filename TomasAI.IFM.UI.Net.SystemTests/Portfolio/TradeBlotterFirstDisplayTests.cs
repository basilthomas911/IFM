using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;
using TomasAI.IFM.UI.Net.Views.Trade;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class TradeBlotterFirstDisplayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Detail_tab_covers_pending_load_and_does_not_reveal_after_close_or_detach(bool detach)
        => OnUiThread(async () =>
        {
            var root = Substitute.For<IAppRoot>();
            var events = new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
                Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>());
            events.SetSiteId(Guid.NewGuid());
            root.Services.CommandResponses.Returns(events);
            var api = Substitute.For<TomasAI.IFM.Domain.Trade.Shared.ServiceApi.ITradeQueryApi>();
            var pending = new TaskCompletionSource<TomasAI.IFM.Shared.EventSourcing.ServiceResult<OptionTradeReadModel>>();
            api.GetOptionTradeAsync(101, 7).Returns(pending.Task);
            root.Services.TradeQueries.Returns(new TomasAI.IFM.UI.Net.Services.Trade.TradeQueryService(api));
            var date = new DateOnly(2026, 8, 11);
            var maturity = new DateOnly(2026, 9, 18);
            var fund = new FundReadModel(17, "Test", "Test", 100000m, false, DateTime.UtcNow, "test");
            var order = new FundOrderReadModel(17, 101, DateTime.UtcNow,
                TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open, "ES", date, maturity,
                "Test", DateTime.UtcNow, "test", null, "test");
            var trade = new FundOrderTradeReadModel(17, 101, 7, TradeType.ShortIronCondor,
                date, maturity, TradeState.OrderFilled, TradeAction.Sell, "Test", true,
                "ES", DateTime.UtcNow, "test", null, "test");
            var model = new IronCondorViewModel(root, fund, order, trade, date, [], historicalReadOnly: true);
            using var form = new Form { Size = new Size(1500, 1000), ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };
            using var tab = new TabPage("Legacy trade");
            using var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(tab); form.Controls.Add(tabs);
            using var viewer = new IronCondorView(tab, model);
            tab.Controls.Add(viewer);
            form.Show();
            var cover = Field<Label>(viewer, "_initialLoading");
            cover.Visible.Should().BeTrue();
            cover.Bounds.Should().Be(viewer.ClientRectangle);
            viewer.Controls.GetChildIndex(cover).Should().Be(0);
            var load = Field<Task>(viewer, "_initialLoad");
            load.IsCompleted.Should().BeFalse();
            if (detach) tab.Controls.Remove(viewer);
            else await ((IAsyncFormControl)viewer).CloseAsync();
            pending.SetResult(new TomasAI.IFM.Shared.EventSourcing.ServiceOk<OptionTradeReadModel>(
                new OptionTradeReadModel { OrderId = 101, TradeId = 7, TradeType = TradeType.ShortIronCondor }));
            await load;
            Field<bool>(viewer, "_preparingInitialContent").Should().BeTrue();
            cover.Text.Should().Be("Loading trade details...");
            await ((IAsyncFormControl)viewer).CloseAsync();
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Pending_load_shows_only_loading_and_cannot_reveal_after_close_or_detach(bool detach)
        => OnUiThread(async () =>
        {
            var service = Substitute.For<IReferenceDataService>();
            var pending = new TaskCompletionSource<UiOperationResult<DefaultFuturesContractDefinitionsUiModel>>();
            service.GetDefaultFuturesContractDefinitionsAsync(Arg.Any<CancellationToken>()).Returns(
                _ => new ValueTask<UiOperationResult<DefaultFuturesContractDefinitionsUiModel>>(pending.Task));
            using var parent = new TradeOrderEditorForm(Substitute.For<IAppRoot>(), service);
            var model = Model(service);
            using var editor = new IronCondorTradeOrderView(parent, model) { Dock = DockStyle.Fill };
            ShowHost(parent);
            var host = Field<Panel>(parent, "pnlTradeControl");
            host.Controls.Add(editor);
            var loading = Field<Label>(editor, "_initialLoading");
            var content = Field<Panel>(editor, "_initialContent");
            loading.Visible.Should().BeTrue();
            content.Visible.Should().BeFalse();
            Field<Task>(editor, "_initialLoad").IsCompleted.Should().BeFalse();
            if (detach) host.Controls.Remove(editor);
            else await ((IAsyncFormControl)editor).CloseAsync();
            pending.SetResult(UiOperationResult<DefaultFuturesContractDefinitionsUiModel>.Failure(503, "late failure"));
            await Field<Task>(editor, "_initialLoad");
            content.Visible.Should().BeFalse();
            loading.Text.Should().Be("Loading trade blotter...");
            await ((IAsyncFormControl)editor).CloseAsync();
        });

    [Fact]
    public Task Historical_blotter_reveals_populated_controls_together_and_loads_only_once()
        => OnUiThread(async () =>
        {
            var service = Substitute.For<IReferenceDataService>();
            using var parent = new TradeOrderEditorForm(Substitute.For<IAppRoot>(), service);
            var seed = Model(service);
            var trade = (OptionTradeReadModel)typeof(IronCondorTradeOrderViewModel)
                .GetMethod("CreateIronCondorTrade", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(seed, [TradeStatus.Open])!;
            var model = Model(service, trade);
            using var editor = new IronCondorTradeOrderView(parent, model) { Dock = DockStyle.Fill };
            Descendants(editor).Should().OnlyContain(control =>
                control.Font.Name == "Microsoft Sans Serif" && Math.Abs(control.Font.Size - 10F) < 0.01F,
                "the final font must be applied before the blotter is measured or hosted");
            var inputs = Descendants(editor).Where(control => control is TextBox or ComboBox or DateTimePicker or NumericUpDown).ToArray();
            Dictionary<Control, int>? revealedHeights = null;
            var fontChangesAfterReveal = new List<string>();
            foreach (var control in Descendants(editor))
                control.FontChanged += (_, _) => { if (revealedHeights is not null) fontChangesAfterReveal.Add(control.Name); };
            var content = Field<Panel>(editor, "_initialContent");
            content.VisibleChanged += (_, _) =>
            {
                if (content.Visible && revealedHeights is null)
                    revealedHeights = inputs.ToDictionary(control => control, control => control.Height);
            };
            ShowHost(parent);
            var host = Field<Panel>(parent, "pnlTradeControl");
            host.Controls.Add(editor);
            var initialLoad = Field<Task>(editor, "_initialLoad");
            await initialLoad;
            await Task.Delay(50); // Let the real host's ControlAdded/layout/paint queue finish.
            model.IsLoaded.Should().BeTrue();
            Field<Label>(editor, "_initialLoading").Text.Should().Be("Loading trade blotter...");
            Field<Panel>(editor, "_initialContent").Visible.Should().BeTrue();
            Field<Label>(editor, "_initialLoading").Visible.Should().BeFalse();
            Field<TextBox>(editor, "txtFundBalance").Text.Should().NotBeNullOrWhiteSpace();
            Field<ComboBox>(editor, "ddlOrderType").Items.Count.Should().BeGreaterThan(0);
            revealedHeights.Should().NotBeNull();
            fontChangesAfterReveal.Should().BeEmpty("hosting must not change fonts after the reveal");
            foreach (var (control, height) in revealedHeights!)
                control.Height.Should().Be(height, $"{control.Name} must not shrink after the reveal");
            editor.Width.Should().Be(host.ClientSize.Width);
            editor.Hide(); editor.Show();
            Field<Task>(editor, "_initialLoad").Should().BeSameAs(initialLoad);
            using var bitmap = new Bitmap(parent.Width, parent.Height);
            parent.DrawToBitmap(bitmap, new Rectangle(Point.Empty, parent.Size));
            Descendants(parent).OfType<ComboBox>().Should().OnlyContain(combo =>
                combo.BackColor.ToArgb() == Color.Black.ToArgb()
                && combo.DrawMode == DrawMode.OwnerDrawFixed);
            foreach (var picker in Descendants(parent).OfType<DateTimePicker>())
            {
                picker.GetType().Name.Should().Be("DarkDateTimePicker");
                using var rendered = new Bitmap(picker.Width, picker.Height);
                picker.DrawToBitmap(rendered, picker.ClientRectangle);
                var pixels = Enumerable.Range(0, rendered.Width).SelectMany(x =>
                    Enumerable.Range(0, rendered.Height).Select(y => rendered.GetPixel(x, y)));
                pixels.Count(color => color.ToArgb() == Color.Black.ToArgb())
                    .Should().BeGreaterThan(rendered.Width * rendered.Height / 2,
                        $"{picker.Name} must paint its date field black");
            }
            var directory = Environment.GetEnvironmentVariable("IFM_BLOTTER_RENDER_DIR");
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                bitmap.Save(Path.Combine(directory, "ready.png"));
            }
            await ((IAsyncFormControl)editor).CloseAsync();
            await seed.DisposeAsync();
        });

    static void ShowHost(TradeOrderEditorForm host)
    {
        // Use the real host and its typography/layout hooks; exclude unrelated account loading.
        host.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), host,
            typeof(TradeOrderEditorForm).GetMethod("TradeOrderForm_Load", BindingFlags.Instance | BindingFlags.NonPublic)!);
        host.ShowInTaskbar = false;
        host.StartPosition = FormStartPosition.Manual;
        host.Location = new Point(-3000, -3000);
        host.Show();
    }

    static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        foreach (var descendant in Descendants(child)) yield return descendant;
    }

    static IronCondorTradeOrderViewModel Model(IReferenceDataService service, OptionTradeReadModel? history = null)
    {
        var date = new DateOnly(2026, 8, 11);
        var maturity = new DateOnly(2026, 9, 18);
        return new IronCondorTradeOrderViewModel(Substitute.For<IAppRoot>(), date, 17,
            new FuturesContractV3ReadModel("ESZ26", "ESZ26", "ES", "ESZ26", "FUT", "USD", "CME", "50", maturity, true),
            new FundOrderReadModel(17, 101, DateTime.UtcNow, TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open,
                "ESZ26", date, maturity, "Test", DateTime.UtcNow, "test", null, "test"),
            new FundOrderTradeReadModel(17, 101, 7, TradeType.ShortIronCondor, date, maturity,
                TradeState.NewTrade, TradeAction.Sell, "Test", true, "ES", DateTime.UtcNow, "test", null, "test"),
            OrderActionType.Open, service, historicalReadOnly: history is not null,
            historicalTrade: history, historicalFundBalance: 250000m);
    }

    static async Task OnUiThread(Func<Task> verify)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new ApplicationContext();
            using var dispatcher = new Control(); _ = dispatcher.Handle;
            dispatcher.BeginInvoke(async () =>
            {
                try { await verify(); completion.SetResult(); }
                catch (Exception exception) { completion.SetException(exception); }
                finally { context.ExitThread(); }
            });
            System.Windows.Forms.Application.Run(context);
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    static T Field<T>(object owner, string name) => (T)owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
}
