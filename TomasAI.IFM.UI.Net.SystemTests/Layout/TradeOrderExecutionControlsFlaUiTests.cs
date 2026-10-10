using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using NSubstitute;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Views.Trade;
using System.Windows.Forms;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class TradeOrderExecutionControlsFlaUiTests
{
    [Fact]
    public async Task Editor_has_no_manual_close_or_state_override_controls()
    {
        var ready = new TaskCompletionSource<Form>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                var root = Substitute.For<IAppRoot>();
                root.Services.Returns(Substitute.For<IUiServiceCatalog>());
                using var editor = new TradeOrderEditorForm(root, Substitute.For<IReferenceDataService>());
                editor.LoadViewModel(new TomasAI.IFM.UI.Net.ViewModels.Trade.TradeOrderEditorViewModel(root,
                    DateOnly.FromDateTime(DateTime.UtcNow), [], Substitute.For<IReferenceDataService>()));
                // Host the actual designer controls without starting editor backend subscriptions.
                using var form = new Form { Text = "Execution-driven Trade Order controls", ClientSize = editor.ClientSize };
                form.Controls.AddRange(editor.Controls.Cast<Control>().ToArray());
                form.Shown += (_, _) => ready.TrySetResult(form);
                System.Windows.Forms.Application.Run(form);
                finished.SetResult();
            }
            catch (Exception error) { ready.TrySetException(error); finished.TrySetException(error); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        var editor = await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            using var automation = new UIA3Automation();
            var window = automation.FromHandle(editor.Handle).AsWindow();
            Assert.Null(window.FindFirstDescendant(cf => cf.ByAutomationId("btnCompleteOrder")));
            Assert.Null(window.FindFirstDescendant(cf => cf.ByAutomationId("btnChangeTradeState")));
            Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("btnLoadOrder")));
            Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("btnAddTrade")));
        }
        finally { editor.BeginInvoke((Action)editor.Close); await finished.Task.WaitAsync(TimeSpan.FromSeconds(15)); }
    }

}
