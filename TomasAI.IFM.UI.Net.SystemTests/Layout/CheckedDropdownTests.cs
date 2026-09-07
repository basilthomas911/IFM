using System.Reflection;
using FluentAssertions;
using TomasAI.IFM.UI.Net.Views.Presentation;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class CheckedDropdownTests
{
    [Fact]
    public async Task Checks_update_readonly_summary_immediately_and_filtering_preserves_selections()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-3000, -3000) };
                using var dropdown = new CheckedDropdown { AccessibleName = "Test choices", Width = 300 };
                form.Controls.Add(dropdown); form.Show();
                dropdown.SetItems([new("RangeBound", "Range Bound"), new("Directional", "Directional"), new("Disabled", "Disabled", false)]);
                var checks = Field<CheckedListBox>(dropdown, "list");
                checks.SetItemChecked(0, true);
                dropdown.SelectedValues.Should().Equal("RangeBound");
                dropdown.DisplayText.Should().Be("Range Bound");
                Field<TextBox>(dropdown, "display").ReadOnly.Should().BeTrue();
                checks.SetItemChecked(1, true);
                dropdown.DisplayText.Should().Be("Range Bound, Directional");
                checks.SetItemChecked(2, true);
                dropdown.SelectedValues.Should().NotContain("Disabled");
                Field<TextBox>(dropdown, "search").Text = "direction";
                checks.Items.Count.Should().Be(1);
                checks.GetItemChecked(0).Should().BeTrue();
                checks.SetItemChecked(0, false);
                dropdown.SelectedValues.Should().Equal("RangeBound");
                dropdown.SetSelectedValues(["Missing"]);
                dropdown.HasUnavailableSelections.Should().BeTrue();
                dropdown.DisplayText.Should().Contain("Unavailable: Missing");
                Field<TextBox>(dropdown, "search").Clear();
                checks.SetItemChecked(checks.Items.Count - 1, false);
                dropdown.SelectedValues.Should().BeEmpty();
                Field<Button>(dropdown, "toggle").PerformClick();
                Field<ToolStripDropDown>(dropdown, "popup").Visible.Should().BeTrue();
                Field<ToolStripDropDown>(dropdown, "popup").Close();
                form.Close(); completed.SetResult();
            }
            catch (Exception ex) { completed.SetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
}
