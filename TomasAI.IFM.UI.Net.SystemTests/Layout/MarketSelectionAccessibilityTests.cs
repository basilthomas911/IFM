using System.Windows.Forms;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;
using DataGridView = System.Windows.Forms.DataGridView;
namespace TomasAI.IFM.UI.Net.SystemTests.Layout;
public sealed class MarketSelectionAccessibilityTests {
    [Fact] public async Task Grid_cell_invoke_raises_click() {
        var ready = new TaskCompletionSource<Form>(TaskCreationOptions.RunContinuationsAsynchronously);
        var clicked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(()=> {
            using var form = new Form { Width = 900,Height = 500 };
            var grid = new TomasAI.IFM.UI.Net.Views.Trade.MarketSelectionDataGridView { Name = "marketSelectionGrid",Dock = DockStyle.Fill,AllowUserToAddRows = false,ReadOnly = true };
            grid.Columns.Add("Contract","Contract"); grid.Rows.Add("ES");
            grid.CellClick += (_,_)=>clicked.TrySetResult();
            form.Controls.Add(grid); form.Shown += (_,_)=>ready.SetResult(form);
            System.Windows.Forms.Application.Run(form);
        }) { IsBackground = true }; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        var form = await ready.Task;
        try {
            using var automation = new UIA3Automation();
            var cell = automation.FromHandle(form.Handle).FindFirstDescendant(cf=>cf.ByAutomationId("marketSelectionGrid")).AsDataGridView().Rows[0].Cells[0];
            Assert.True(cell.Patterns.Invoke.IsSupported);
            cell.Patterns.Invoke.Pattern.Invoke();
            await clicked.Task.WaitAsync(TimeSpan.FromSeconds(3));
        } finally { form.BeginInvoke((Action)(()=>form.Close())); thread.Join(TimeSpan.FromSeconds(5)); }
    }
}
