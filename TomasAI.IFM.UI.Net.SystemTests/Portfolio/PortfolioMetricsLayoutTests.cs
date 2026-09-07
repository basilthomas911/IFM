using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.Fund;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioMetricsLayoutTests
{
    [Fact]
    public async Task Three_equal_sections_and_two_metric_rows_fit_at_default_and_minimum_sizes()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new PortfolioAdministrationForm();
                form.Show();
                var portfolio = new PortfolioReadModel { PortfolioId = 1101, Name = "Primary Portfolio", OperatingState = PortfolioOperatingState.Active, PortfolioVersion = 1 };
                var fund = new FundMandateReadModel { PortfolioId = 1101, FundId = 5001, Name = "Core Futures", FundCode = "CORE", FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Active, EffectiveFromUtc = new DateTime(2026, 1, 1), Objective = "Core futures strategy", UnderlyingUniverse = ["ES", "VX"] };
                var model = new PortfolioAdministrationViewModel(Substitute.For<IPortfolioQueryApi>(), Substitute.For<IPortfolioCommandApi>(), Substitute.For<IPortfolioFundCommandApi>(), Substitute.For<IPortfolioIdentityApi>(), false);
                typeof(PortfolioAdministrationViewModel).GetProperty(nameof(model.SelectedPortfolio))!.SetValue(model, portfolio);
                typeof(PortfolioAdministrationViewModel).GetProperty(nameof(model.SelectedFund))!.SetValue(model, fund);
                Set(form, "_viewModel", model); Set(form, "_bindingSelection", true);
                Field<DataGridView>(form, "_portfolios").DataSource = new[] { portfolio };
                Field<DataGridView>(form, "_funds").DataSource = new[] { fund };
                Invoke(form, "BindConfiguration");
                Field<DateTimePicker>(form, "_metricsTo").Value = new DateTime(2026, 9, 8);
                var api = Substitute.For<IFundQueryApi>();
                api.GetFundPnlReportAsync(Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>()).Returns(new ServiceOk<FundPnlReportReadModel>(new(.6, 250, .4, -100, 3.75, 1, 1.2, 1500, .15, 25) { HasHistory = true, MaximumDrawdownAmount = 500, MaximumDrawdownPercent = .05 }));
                using var metrics = new FundMetricsViewModel(new FundQueryService(api));
                Set(form, "_metrics", metrics);
                // The in-memory API completes synchronously; no server or mutation is involved.
                metrics.LoadAsync(5001, new(2026, 1, 1), new(2026, 9, 8)).GetAwaiter().GetResult();
                Invoke(form, "RenderMetrics");
                foreach (var size in new[] { new Size(1450, 900), form.MinimumSize })
                {
                    form.Size = size; form.PerformLayout(); form.Refresh();
                    var sections = Field<TableLayoutPanel>(form, "_sections");
                    var widths = sections.Controls.Cast<Control>().Select(x => x.Width).ToArray();
                    (widths.Max() - widths.Min()).Should().BeLessThanOrEqualTo(2);
                    foreach (Control section in sections.Controls)
                    {
                        sections.ClientRectangle.Contains(section.Bounds).Should().BeTrue();
                        foreach (var button in Descendants(section).OfType<Button>().Where(x => x.Visible))
                            button.Parent!.ClientRectangle.Contains(button.Bounds).Should().BeTrue(button.Text + " must fit its wrapped toolbar");
                    }
                    var tabs = Descendants(sections).OfType<TabControl>().Single();
                    for (var index = 0; index < tabs.TabPages.Count; index++)
                    {
                        tabs.ClientRectangle.Contains(tabs.GetTabRect(index)).Should().BeTrue("every Fund detail tab must be visible");
                        tabs.GetTabRect(index).Width.Should().BeGreaterThan(TextRenderer.MeasureText(tabs.TabPages[index].Text, tabs.Font).Width);
                    }
                    var strip = Field<TableLayoutPanel>(form, "_metricStrip");
                    strip.RowCount.Should().Be(2);
                    strip.ColumnCount.Should().Be(10);
                    foreach (Control input in strip.Controls) strip.ClientRectangle.Contains(input.Bounds).Should().BeTrue(input.AccessibleName);
                    var values = Field<TextBox[]>(form, "_metricValues");
                    values.Should().OnlyContain(x => x.ReadOnly && x.BackColor == Color.Black && x.ForeColor == Color.White);
                    values[9].Text.Should().Be(.05.ToString("P2"));
                    if (Environment.GetEnvironmentVariable("IFM_REFERENCE_UI_RENDER_DIR") is { Length: > 0 } path)
                    {
                        Directory.CreateDirectory(path);
                        using var bitmap = new Bitmap(form.Width, form.Height);
                        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                        bitmap.Save(Path.Combine(path, $"portfolio-metrics-{size.Width}.png"));
                    }
                }
                metrics.Clear(); Invoke(form, "RenderMetrics");
                Field<TextBox[]>(form, "_metricValues").Should().OnlyContain(x => x.Text == "N/A");
                form.Close(); done.TrySetResult();
            }
            catch (Exception ex) { done.TrySetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(25));
    }

    static IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);
}
