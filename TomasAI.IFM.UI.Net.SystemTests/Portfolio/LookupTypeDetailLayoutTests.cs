using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.Views.Reference;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class LookupTypeDetailLayoutTests
{
    [Fact]
    public async Task Details_fit_the_reference_pane_when_browsing_editing_and_resizing()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stage = "starting";
        var thread = new Thread(() =>
        {
            try
            {
                var service = Substitute.For<IReferenceDataService>();
                service.GetReferenceDataDefinitionTypesAsync(Arg.Any<CancellationToken>()).Returns(
                    UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success([
                        new("Reference", "LookupTypes", 1, "lookup type", DateTime.UtcNow, "test")]));
                service.GetLookupTypesAsync(Arg.Any<CancellationToken>()).Returns(
                    UiOperationResult<IReadOnlyList<LookupTypeUiModel>>.Success([]));
                service.GetLookupTypeNamesAsync(Arg.Any<CancellationToken>()).Returns(
                    UiOperationResult<IReadOnlyList<string>>.Success([]));
                service.GetLookupTypeShortCodesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
                    UiOperationResult<IReadOnlyList<LookupTypeShortCodeUiModel>>.Success([]));
                using var form = new ReferenceForm(Substitute.For<IAppRoot>(), service);
                System.Windows.Forms.Application.ThreadException += (_, e) =>
                {
                    done.TrySetException(e.Exception);
                    form.Dispose();
                };
                form.LoadViewModel(new ReferenceViewModel(service));
                form.Shown += async (_, _) =>
                {
                    try
                    {
                        // Finish the form's Shown/activation sequence before exercising it.
                        await Task.Yield();
                        var host = Field<Panel>(form, "pnlMarketData");
                        for (var i = 0; i < 100 && host.Controls.Count == 0; i++) await Task.Delay(20);
                        var view = host.Controls.OfType<LookupTypeEditorView>().Single();
                        var originalSize = form.Size;
                        foreach (var size in new[] { originalSize, new Size(originalSize.Width - 150, originalSize.Height), new Size(originalSize.Width + 150, originalSize.Height + 80) })
                        {
                            form.Size = size;
                            AssertLayout(host, view);
                            view.Add(_ => { });
                            AssertLayout(host, view);
                            view.Close(_ => { }).Should().BeFalse();
                        }
                        form.Size = originalSize;
                        view.Add(_ => { });
                        Field<TextBox>(view, "txtLookupTypeName").Text = "ReferenceDataDefinitionTypes";
                        Field<TextBox>(view, "txtShortCode").Text = "LookupTypes";
                        Field<TextBox>(view, "txtDescription").Text = "Lookup type definitions\r\nMultiline description";
                        form.Refresh();
                        if (Environment.GetEnvironmentVariable("IFM_REFERENCE_UI_RENDER_DIR") is { Length: > 0 } directory)
                        {
                            Directory.CreateDirectory(directory);
                            using var bitmap = new Bitmap(form.Width, form.Height);
                            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                            bitmap.Save(Path.Combine(directory, "lookup-type-details.png"));
                        }
                        stage = "closing";
                        form.Close();
                        stage = $"close returned; complete={Field<bool>(form, "_closeComplete")}";
                    }
                    catch (Exception ex) { done.TrySetException(ex); form.Close(); }
                };
                System.Windows.Forms.Application.Run(form);
                done.TrySetResult();
            }
            catch (Exception ex) { done.TrySetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await done.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        catch (TimeoutException) { throw new TimeoutException(stage); }
        thread.Join(TimeSpan.FromSeconds(3)).Should().BeTrue();
    }

    static void AssertLayout(Panel host, LookupTypeEditorView view)
    {
        view.Bounds.Should().Be(host.ClientRectangle);
        var pane = Field<SplitContainer>(view, "splitContainer1").Panel2;
        var fields = new[] { "LookupTypeName", "ShortCode", "OrderId", "Description" };
        var previousBottom = 0;
        int? left = null;
        int? right = null;
        foreach (var name in fields)
        {
            var label = Field<Label>(view, "lbl" + name);
            var input = Field<TextBox>(view, "txt" + name);
            var labelBounds = pane.RectangleToClient(label.RectangleToScreen(label.ClientRectangle));
            var inputBounds = pane.RectangleToClient(input.Parent!.RectangleToScreen(input.Bounds));
            pane.ClientRectangle.Contains(labelBounds).Should().BeTrue(name + " label must fit");
            pane.ClientRectangle.Contains(inputBounds).Should().BeTrue(name + " input must fit");
            labelBounds.Right.Should().BeLessThan(inputBounds.Left);
            inputBounds.Top.Should().BeGreaterThan(previousBottom);
            inputBounds.Width.Should().BeGreaterThan(120);
            left ??= inputBounds.Left;
            right ??= inputBounds.Right;
            inputBounds.Left.Should().Be(left.Value);
            inputBounds.Right.Should().Be(right.Value);
            if (name != "Description")
                Math.Abs((labelBounds.Top + labelBounds.Bottom) - (inputBounds.Top + inputBounds.Bottom)).Should().BeLessThanOrEqualTo(2);
            previousBottom = inputBounds.Bottom;
        }
        Field<TextBox>(view, "txtDescription").Multiline.Should().BeTrue();
    }

    static T Field<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
}
