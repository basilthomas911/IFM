using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.Views.App;
using TomasAI.IFM.UI.Net.Views.Fund;
using TomasAI.IFM.UI.Net.Views.MarketData;
using TomasAI.IFM.UI.Net.Views.Portfolio;
using TomasAI.IFM.UI.Net.Views.Presentation;
using TomasAI.IFM.UI.Net.Views.Reference;
using TomasAI.IFM.UI.Net.Views.SystemAdmin;
using TomasAI.IFM.UI.Net.Views.Trade;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class DarkTradingThemeTests
{
    [Fact]
    public void Every_application_form_and_view_inherits_the_default_theme()
    {
        var types = typeof(DarkTradingTheme).Assembly.GetTypes();
        types.Where(type => type.IsSubclassOf(typeof(Form)))
            .Should().OnlyContain(type => typeof(DarkTradingForm).IsAssignableFrom(type));
        types.Where(type => type.IsSubclassOf(typeof(UserControl)))
            .Should().OnlyContain(type => typeof(DarkTradingView).IsAssignableFrom(type));
    }

    [Fact]
    public Task New_views_and_late_controls_are_themed_before_display_without_changing_behavior()
        => OnSta(() =>
        {
            using var form = new DarkTradingForm();
            using var view = new DarkTradingView();
            form.Controls.Add(view);
            var panel = new Panel();
            view.Controls.Add(panel);
            var input = new TextBox { Text = "Retained input", ReadOnly = true, Font = new Font("Segoe UI", 18) };
            panel.Controls.Add(input);
            input.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            input.Font.Name.Should().Be(DarkTradingTheme.FontFamily);
            input.Font.Size.Should().Be(10);
            input.Font.Bold.Should().BeTrue();
            input.ReadOnly.Should().BeTrue();
            input.Text.Should().Be("Retained input");
            input.BackColor.Should().Be(Color.Black);
            input.ForeColor.Should().Be(Color.White);
            var warning = new Label { BackColor = Color.Yellow, ForeColor = Color.Black, Text = "Warning" };
            panel.Controls.Add(warning);
            warning.BackColor.Should().Be(Color.Yellow);
            warning.ForeColor.Should().Be(Color.Black);
            var button = new Button { Text = "Save" };
            var clicks = 0;
            button.Click += (_, _) => clicks++;
            panel.Controls.Add(button);
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = Point.Empty;
            form.Opacity = 0;
            form.Show();
            button.PerformClick();
            clicks.Should().Be(1);
            button.FlatStyle.Should().Be(FlatStyle.Flat);
            button.BackColor.Should().Be(DarkTradingTheme.CommandSurface);
            using var menu = new ContextMenuStrip();
            panel.ContextMenuStrip = menu;
            var menuItem = menu.Items.Add("Added later");
            menuItem.Font.Size.Should().Be(10);
            menuItem.BackColor.Should().Be(Color.Black);
            var grid = new DataGridView();
            panel.Controls.Add(grid);
            grid.Columns.Add("value", "Value");
            grid.Columns[0].DefaultCellStyle.SelectionBackColor.Should().Be(SystemColors.Highlight);
        });

    [Fact]
    public Task Combo_selection_is_blue_and_disabled_selection_remains_readable()
        => OnSta(() =>
        {
            using var view = new DarkTradingView();
            var combo = new DrawProbeCombo { DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.Add("Selected");
            view.Controls.Add(combo);
            using var bitmap = new Bitmap(200, 30);
            using var graphics = Graphics.FromImage(bitmap);
            combo.Draw(graphics, DrawItemState.Selected);
            bitmap.GetPixel(190, 15).ToArgb().Should().Be(SystemColors.Highlight.ToArgb());
            combo.Enabled = false;
            combo.Draw(graphics, DrawItemState.Selected);
            bitmap.GetPixel(190, 15).ToArgb().Should().Be(Color.Black.ToArgb());
        });

    [Fact]
    public Task Button_caption_colors_follow_state_parent_state_and_late_overrides()
        => OnSta(() =>
        {
            using var form = new DarkTradingForm();
            var panel = new Panel { Dock = DockStyle.Fill };
            form.Controls.Add(panel);
            var button = new Button { Text = "Save", Size = new Size(140, 32), Enabled = false };
            panel.Controls.Add(button);
            button.ForeColor.Should().Be(Color.Gray);
            button.Enabled = true;
            button.ForeColor.Should().Be(Color.White);
            button.ForeColor = Color.Black;
            button.ForeColor.Should().Be(Color.White);
            panel.Enabled = false;
            button.ForeColor.Should().Be(Color.Gray);
            panel.Enabled = true;
            button.ForeColor.Should().Be(Color.White);

            var toolbar = new ToolStrip { Dock = DockStyle.Bottom };
            panel.Controls.Add(toolbar);
            var command = new ToolStripButton("Command") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            toolbar.Items.Add(command);
            command.ForeColor.Should().Be(Color.White);
            command.Enabled = false;
            command.ForeColor.Should().Be(Color.Gray);
            command.ForeColor = Color.Red;
            command.ForeColor.Should().Be(Color.Gray);
            command.Enabled = true;
            command.ForeColor.Should().Be(Color.White);
            toolbar.Enabled = false;
            command.ForeColor.Should().Be(Color.Gray);
            toolbar.Enabled = true;
            command.ForeColor.Should().Be(Color.White);

            form.ShowInTaskbar = false;
            form.Opacity = 0;
            form.Show();
            foreach (var enabled in new[] { true, false, true })
            {
                button.Enabled = enabled;
                using var bitmap = new Bitmap(button.Width, button.Height);
                button.DrawToBitmap(bitmap, button.ClientRectangle);
                var captionPixels = Enumerable.Range(20, button.Width - 40).SelectMany(x =>
                    Enumerable.Range(6, button.Height - 12).Select(y => bitmap.GetPixel(x, y).ToArgb()));
                captionPixels.Should().Contain(DarkTradingTheme.ButtonTextColor(enabled).ToArgb(),
                    "the rendered caption must match its enabled state, not only its ForeColor property");
                command.Enabled = enabled;
                using var toolbarBitmap = new Bitmap(toolbar.Width, toolbar.Height);
                toolbar.DrawToBitmap(toolbarBitmap, toolbar.ClientRectangle);
                var commandBounds = Rectangle.Inflate(command.Bounds, -5, -4);
                var commandPixels = Enumerable.Range(commandBounds.Left, commandBounds.Width).SelectMany(x =>
                    Enumerable.Range(commandBounds.Top, commandBounds.Height).Select(y => toolbarBitmap.GetPixel(x, y).ToArgb()));
                commandPixels.Should().Contain(DarkTradingTheme.ButtonTextColor(enabled).ToArgb(),
                    "toolbar rendering must use the same enabled and disabled caption colors");
            }
        });

    [Theory]
    [InlineData("Reference")][InlineData("MarketData")][InlineData("Portfolio")]
    [InlineData("Fund")][InlineData("Administration")][InlineData("CreateOrder")]
    [InlineData("Operations")][InlineData("Outlook")]
    public Task Representative_screens_render_with_shared_fonts_inputs_and_buttons(string screen)
        => OnSta(() =>
        {
            void Trace(string stage)
            {
                var dir = Environment.GetEnvironmentVariable("IFM_DARK_THEME_RENDER_DIR");
                if (string.IsNullOrEmpty(dir)) return;
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "render.log"), $"{screen}: {stage}{Environment.NewLine}");
            }
            Trace("constructing");
            var app = Substitute.For<IAppRoot>();
            var reference = Substitute.For<IReferenceDataService>();
            using Control view = screen switch
            {
                "Reference" => new ReferenceForm(app, reference),
                "MarketData" => new MarketDataForm(app, Substitute.For<IStatusConsoleEventProducer>(), reference),
                "Portfolio" => new PortfolioAdministrationForm(),
                "Fund" => new FundTransactionEditor(Substitute.For<IViewNavigator>()),
                "Administration" => new SystemAdminForm(Substitute.For<IDatabaseBackupService>()),
                "CreateOrder" => new CreateFundOrderForm(),
                "Operations" => new OperationsView(),
                _ => new MarketOutlookView()
            };
            Trace("constructed");
            using var host = view is Form ? null : new DarkTradingForm { ClientSize = view.Size };
            var form = view as Form ?? host!;
            if (host is not null) { view.Dock = DockStyle.Fill; host.Controls.Add(view); }
            // Suppress backend startup handlers only; retain the real base lifecycle/layout/paint.
            var events = (System.ComponentModel.EventHandlerList)typeof(Control)
                .GetProperty("Events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(form)!;
            foreach (var key in typeof(Form).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                         .Where(field => field.Name.Contains("Load", StringComparison.OrdinalIgnoreCase)))
            {
                var value = key.GetValue(null);
                if (value is not null && events[value] is { } handler) events.RemoveHandler(value, handler);
            }
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = Point.Empty;
            form.Opacity = 0;
            form.Show();
            Trace("shown");
            System.Windows.Forms.Application.DoEvents();
            using var bitmap = new Bitmap(view.Width, view.Height);
            view.DrawToBitmap(bitmap, new Rectangle(Point.Empty, view.Size));
            Trace("painted");
            var controls = Descendants(view).ToArray();
            controls.Should().OnlyContain(control => control.Font.Name == DarkTradingTheme.FontFamily
                && Math.Abs(control.Font.Size - DarkTradingTheme.FontSize) < 0.01F);
            controls.OfType<Button>().Should().OnlyContain(button => button.FlatStyle == FlatStyle.Flat);
            controls.OfType<Button>().Should().OnlyContain(button =>
                button.ForeColor.ToArgb() == DarkTradingTheme.ButtonTextColor(button.Enabled).ToArgb());
            controls.OfType<ComboBox>().Should().OnlyContain(combo => combo.BackColor.ToArgb() == Color.Black.ToArgb()
                && combo.DrawMode == DrawMode.OwnerDrawFixed);
            controls.OfType<DateTimePicker>().Should().OnlyContain(picker => picker is DarkDateTimePicker);
            foreach (var grid in controls.OfType<DataGridView>())
            {
                grid.EnableHeadersVisualStyles.Should().BeFalse();
                grid.ColumnHeadersDefaultCellStyle.BackColor.Should().Be(DarkTradingTheme.CommandSurface);
            }
            var directory = Environment.GetEnvironmentVariable("IFM_DARK_THEME_RENDER_DIR");
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                bitmap.Save(Path.Combine(directory, screen + ".png"));
            }
        });

    static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        foreach (var descendant in Descendants(child)) yield return descendant;
    }

    static async Task OnSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, true);
            using var context = new ApplicationContext();
            using var dispatcher = new Control();
            _ = dispatcher.Handle;
            dispatcher.BeginInvoke(() =>
            {
                try { action(); completion.SetResult(); }
                catch (Exception error) { completion.SetException(error); }
                finally { context.ExitThread(); }
            });
            System.Windows.Forms.Application.Run(context);
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    sealed class DrawProbeCombo : ComboBox
    {
        public void Draw(Graphics graphics, DrawItemState state)
            => OnDrawItem(new DrawItemEventArgs(graphics, Font, new Rectangle(0, 0, 200, 30), 0, state));
    }
}
