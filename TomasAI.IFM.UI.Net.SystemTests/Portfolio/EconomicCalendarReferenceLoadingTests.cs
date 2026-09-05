using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.Views.Reference;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class EconomicCalendarReferenceLoadingTests
{
    [Theory]
    [InlineData(1F)]
    [InlineData(1.5F)]
    [InlineData(2F)]
    public async Task Economic_editor_renders_on_first_show_and_after_switching_editors(float scale)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, true);
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.SystemAware);
            using var context = new ApplicationContext(); using var dispatcher = new Control(); _ = dispatcher.Handle;
            dispatcher.BeginInvoke(async () =>
            {
                try { await VerifyAsync(scale); completion.SetResult(); }
                catch (Exception ex) { completion.SetException(ex); }
                finally { context.ExitThread(); }
            });
            System.Windows.Forms.Application.Run(context);
        }) { IsBackground = true, Name = "Economic calendar editor loading" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    static async Task VerifyAsync(float scale)
    {
        var references = Substitute.For<IReferenceDataService>();
        references.GetReferenceDataDefinitionTypesAsync(Arg.Any<CancellationToken>()).Returns(
            UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success([
                new("ReferenceDataDefinitionType", "EconomicCalendar", 0, "economic calendar definitions was changed", DateTime.UtcNow, "test")]));
        var calendar = Substitute.For<IEconomicCalendarService>(); var subscription = Substitute.For<IUiEventSubscription>();
        calendar.CreateSubscription(Arg.Any<Action<TerminalNotificationUiModel>>()).Returns(subscription);
        calendar.GetCountryCodesAsync(Arg.Any<CancellationToken>()).Returns(
            UiOperationResult<IReadOnlyList<EconomicCalendarCountryCodeUiModel>>.Success([new("US")]));
        calendar.GetCalendarsAsync(Arg.Any<DateOnly>(), "US", Arg.Any<CancellationToken>()).Returns(
            UiOperationResult<IReadOnlyList<EconomicCalendarUiModel>>.Success([
                new(new DateTime(2026, 9, 4, 12, 30, 0, DateTimeKind.Utc), "US", "Employment report", "1", "2", "3", DateTime.UtcNow, "test")]));
        var queries = Substitute.For<IReferenceQueryApi>();
        queries.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<TradeStrategyFamilyReadModel[]>([]));
        var app = Substitute.For<IAppRoot>(); app.Services.ReferenceQueries.Returns(queries);
        var model = new ReferenceViewModel(references); await model.LoadReferenceDataDefinitionTypesOperation.ExecuteAsync();
        using var form = new ReferenceForm(app, references, calendar);
        form.LoadViewModel(model);
        var selector = Field<ComboBox>(form, "ddlReferenceDataSelector");
        _ = form.Handle;
        Invoke(form, "BindReferenceDataDefinitionTypes");
        form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form,
            typeof(ReferenceForm).GetMethod("ReferenceForm_Load", BindingFlags.Instance | BindingFlags.NonPublic)!);
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-2000, -2000); form.Show();
        form.Scale(new SizeF(scale, scale));
        await Task.Delay(50);
        var host = Field<Panel>(form, "pnlMarketData");
        var editor = host.Controls.OfType<EconomicCalendarEditorView>().Single();
        editor.Visible.Should().BeTrue();
        editor.Width.Should().BeGreaterThan(100); editor.Height.Should().BeGreaterThan(100);
        editor.Bounds.Should().Be(host.ClientRectangle);
        form.Update();
        var list = Field<ListBox>(editor, "lstCalendarEvents");
        var until = DateTime.UtcNow.AddSeconds(2);
        while (list.Items.Count == 0 && DateTime.UtcNow < until) await Task.Delay(10);
        Field<ComboBox>(editor, "ddlCountryCodes").SelectedItem.Should().Be("US");
        list.Items.Cast<string>().Should().Equal("US:Employment report");
        Field<TextBox>(editor, "txtEventName").Text.Should().Be("Employment report");
        editor.Visible.Should().BeTrue();
        editor.Width.Should().BeGreaterThan(100); editor.Height.Should().BeGreaterThan(100);
        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        var directory = Environment.GetEnvironmentVariable("IFM_REFERENCE_UI_RENDER_DIR");
        if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); bitmap.Save(Path.Combine(directory, $"economic-calendar-{scale}.png")); }
        selector.SelectedIndex = 1;
        await Task.Delay(50);
        selector.SelectedIndex = 0;
        await Task.Delay(50);
        host.Controls.OfType<EconomicCalendarEditorView>().Single().Visible.Should().BeTrue();
        await ((IAsyncFormControl)host.Controls[0]).CloseAsync();
    }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    static void Invoke(object owner, string name) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, null);
}
