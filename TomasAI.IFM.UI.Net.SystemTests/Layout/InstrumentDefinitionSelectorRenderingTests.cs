using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Views.MarketData;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class InstrumentDefinitionSelectorRenderingTests
{
    [Theory]
    [InlineData(false, 1120, 660)]
    [InlineData(true, 1120, 660)]
    [InlineData(true, 850, 500)]
    public void Selector_filters_and_actions_fit_without_clipping(bool options, int width, int height)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var model = new InstrumentDefinitionSelectorViewModel(new MarketDataQueryService(
                    Substitute.For<IMarketDataQueryApi>(), Substitute.For<IMarketDataFeedQueryApi>()));
                using var form = new InstrumentDefinitionSelectorForm(model, options) { Size = new(width, height) };
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new(-32000, -32000);
                form.ShowInTaskbar = false;
                form.Show();
                System.Windows.Forms.Application.DoEvents();
                form.PerformLayout();
                var filters = form.Controls.OfType<FlowLayoutPanel>().Single();
                filters.PerformLayout();
                form.PerformLayout();
                Assert.All(filters.Controls.Cast<Control>(), control =>
                {
                    Assert.True(control.Right <= filters.ClientSize.Width, control.Text);
                    Assert.True(control.Bottom <= filters.ClientSize.Height, control.Text);
                });
                var grid = form.Controls.OfType<DataGridView>().Single();
                var review = form.Controls.OfType<PropertyGrid>().Single();
                Assert.True(grid.Width >= 450);
                Assert.True(grid.Height >= 200);
                Assert.False(grid.Bounds.IntersectsWith(review.Bounds));
                Assert.True(grid.ReadOnly);
                Assert.Equal(ComboBoxStyle.DropDownList, filters.Controls.OfType<Panel>()
                    .SelectMany(panel => panel.Controls.OfType<ComboBox>()).Single().DropDownStyle);
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                var directory = Path.Combine(AppContext.BaseDirectory, "artifacts", "stage2");
                Directory.CreateDirectory(directory);
                bitmap.Save(Path.Combine(directory, $"selector-{options}-{width}.png"), ImageFormat.Png);
                form.Dispose();
                form.Dispose();
            }
            catch (Exception e) { failure = e; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
