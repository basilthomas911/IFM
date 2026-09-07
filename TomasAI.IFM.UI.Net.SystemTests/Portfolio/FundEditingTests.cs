using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Views.Presentation;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class FundEditingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Fund_buttons_open_and_submit_without_deployments_and_preserve_save_errors(bool change, bool saveFails)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new ApplicationContext();
            using var dispatcher = new Control(); _ = dispatcher.Handle;
            dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var portfolio = new PortfolioReadModel { PortfolioId = 7001, Name = "Core", PortfolioVersion = 1,
                        OperatingState = PortfolioOperatingState.Active, BaseCurrency = "USD", EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "test" };
                    var fund = new FundMandateReadModel { PortfolioId = 7001, FundId = 8001, FundCode = "DAILY", Name = "Daily",
                        SchemaVersion = 3, FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Draft,
                        DecisionHorizon = "Daily", Objective = "Directional", UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"],
                        EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "test" };
                    var queries = Substitute.For<IPortfolioQueryApi>();
                    queries.GetPortfoliosAsync(Arg.Any<PortfolioOperatingState>(), 100, null, Arg.Any<CancellationToken>()).Returns(
                        new ServiceOk<PortfolioPage<PortfolioReadModel>>(new() { Items = [portfolio] }));
                    queries.GetFundsAsync(7001, null, 100, null, Arg.Any<CancellationToken>()).Returns(
                        new ServiceOk<PortfolioPage<FundMandateReadModel>>(new() { Items = change ? [fund] : [] }));
                    queries.GetPortfolioRevisionAsync(7001, Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioAggregateRevision>(new() { Revision = 3 }));
                    queries.GetFundRevisionAsync(7001, 8001, Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioAggregateRevision>(new() { Revision = 2 }));
                    queries.GetFundAllocationAsync(7001, 8001, Arg.Any<CancellationToken>()).Returns(new ServiceFailed<FundAllocationReadModel>(34001, "none"));
                    queries.GetFundRiskEnvelopeAsync(7001, 8001, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(new ServiceFailed<FundRiskEnvelopeReadModel>(34001, "none"));
                    queries.GetAssignmentsAsync(7001, 8001, 1, Arg.Any<CancellationToken>()).Returns(new ServiceOk<FundTradeTemplateAssignmentReadModel[]>([]));
                    var identities = Substitute.For<IPortfolioIdentityApi>();
                    identities.AllocateFundIdAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<PortfolioBusinessIdAllocation>(new() { Kind = PortfolioBusinessIdentityKind.Fund, Value = 8001 }));
                    var references = Substitute.For<IReferenceQueryApi>();
                    references.GetStrategyDeploymentChoicesAsync(Arg.Any<CancellationToken>()).Returns(new ServiceOk<StrategyDeploymentChoice[]>([]));
                    references.GetLookupDefinitionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(c =>
                    {
                        var group = c.Arg<string>();
                        var value = group == LookupDefinitionGroups.AssetTypes ? "Futures" : group == LookupDefinitionGroups.Directions ? "Bullish" : "Directional";
                        return new ServiceOk<LookupDefinitionReadModel[]>([new(1, group, value, value, "", 10, true, now, now)]);
                    });
                    references.GetTradeStrategySymbolsAsync(Arg.Any<TradeStrategyFamilyType>(), Arg.Any<CancellationToken>()).Returns(
                        new ServiceOk<TradeStrategySymbolReadModel[]>([new() { Id = 1, Symbol = "ES", Exchange = "XCME", Currency = "USD", Description = "ES" }]));
                    var commands = Substitute.For<IPortfolioCommandApi>();
                    commands.AddFundAsync(Arg.Any<PortfolioFundId>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
                    var fundCommands = Substitute.For<IPortfolioFundCommandApi>();
                    FundMandateReadModel? submitted = null;
                    ServiceResult<Guid> Capture(FundMandateReadModel mandate)
                    {
                        submitted = mandate;
                        return saveFails ? new ServiceFailed<Guid>(34001, "Save rejected for verification") : new ServiceOk<Guid>(Guid.NewGuid());
                    }
                    fundCommands.CreateFundMandateAsync(Arg.Any<FundMandateReadModel>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(c => Capture(c.Arg<FundMandateReadModel>()));
                    fundCommands.AddFundMandateVersionAsync(Arg.Any<FundMandateReadModel>(), Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns(c => Capture(c.Arg<FundMandateReadModel>()));
                    using var form = new PortfolioAdministrationForm { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-3000, -3000) };
                    form.Show();
                    await form.LoadViewModelAsync(queries, commands, fundCommands, identities, referenceQueries: references);
                    queries.ClearReceivedCalls();
                    var button = Field<Button>(form, change ? "_newFundVersion" : "_createFund");
                    button.Enabled.Should().BeTrue();
                    if (change) button.Text.Should().Be("Change Fund...");
                    Exception? modalFailure = null; bool opened = false;
                    using var timer = new System.Windows.Forms.Timer { Interval = 30 };
                    timer.Tick += (_, _) =>
                    {
                        var editor = System.Windows.Forms.Application.OpenForms.OfType<FundMandateEditorForm>().FirstOrDefault();
                        if (editor is null) return;
                        timer.Stop(); opened = true;
                        try
                        {
                            editor.Text.Should().Be(change ? "Change Fund" : "Create Fund");
                            Field<CheckedListBox>(editor, "_families").Items.Count.Should().Be(0);
                            var layout = editor.Controls.OfType<TableLayoutPanel>().Single();
                            layout.Controls.OfType<Label>().Should().NotContain(x => x.Text == "Code" || x.Text == "Fund Code");
                            layout.Controls.OfType<TextBox>().Should().NotContain(x => x.AccessibleName == "Fund code");
                            for (var row = 0; row < 10; row++)
                            {
                                var label = (Label)layout.GetControlFromPosition(0, row)!;
                                var input = layout.GetControlFromPosition(1, row)!;
                                Math.Abs((label.Top + label.Height / 2d) - (input.Top + input.Height / 2d)).Should().BeLessThanOrEqualTo(1);
                                label.Padding.Should().Be(Padding.Empty);
                            }
                            ((Label)layout.GetControlFromPosition(0, 10)!).TextAlign.Should().Be(ContentAlignment.TopRight);
                            Field<TextBox>(editor, "_name").Text = "Edited Fund";
                            Field<TextBox>(editor, "_objective").Text = "Directional";
                            Field<CheckedDropdown>(editor, "_underlyings").SetSelectedValues(["ES"]);
                            Field<CheckedDropdown>(editor, "_assets").SetSelectedValues(["Futures"]);
                            Field<CheckedDropdown>(editor, "_directions").SetSelectedValues(["Bullish"]);
                            Field<CheckedDropdown>(editor, "_conditions").SetSelectedValues(["Directional"]);
                            var renderDirectory = Environment.GetEnvironmentVariable("IFM_REFERENCE_UI_RENDER_DIR");
                            if (!saveFails && !string.IsNullOrWhiteSpace(renderDirectory))
                            {
                                Directory.CreateDirectory(renderDirectory);
                                using var bitmap = new Bitmap(editor.Width, editor.Height);
                                editor.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                                bitmap.Save(Path.Combine(renderDirectory, change ? "change-fund-lookups.png" : "create-fund-lookups.png"));
                                var dropdown = Field<CheckedDropdown>(editor, "_conditions");
                                Field<Button>(dropdown, "toggle").PerformClick();
                                var popup = Field<ToolStripDropDown>(dropdown, "popup");
                                using var popupBitmap = new Bitmap(popup.Width, popup.Height);
                                popup.DrawToBitmap(popupBitmap, new Rectangle(Point.Empty, popupBitmap.Size));
                                popupBitmap.Save(Path.Combine(renderDirectory, "checked-dropdown.png"));
                                popup.Close();
                            }
                            editor.AcceptButton!.PerformClick();
                            editor.Value.Should().NotBeNull(Field<Label>(editor, "_error").Text);
                        }
                        catch (Exception exception) { modalFailure = exception; editor.Close(); }
                    };
                    timer.Start(); button.PerformClick();
                    if (modalFailure is not null) throw modalFailure;
                    opened.Should().BeTrue("an empty deployment catalog must not block opening the editor");
                    submitted.Should().NotBeNull();
                    submitted!.Name.Should().Be("Edited Fund");
                    submitted.FundMandateVersion.Should().Be(change ? 2 : 1);
                    submitted.FundCode.Should().Be(change ? "DAILY" : "FUND-8001");
                    submitted.OperatingState.Should().Be(FundOperatingState.Draft);
                    submitted.PermittedTradeStrategyFamilies.Should().BeEmpty();
                    submitted.PermittedDirections.Should().Equal("Bullish");
                    submitted.PermittedConditions.Should().Equal("Directional");
                    submitted.Validate().Should().BeEmpty();
                    if (saveFails)
                    {
                        Field<Label>(form, "_status").Text.Should().Contain("Save rejected for verification");
                        await queries.DidNotReceive().GetFundsAsync(Arg.Any<int>(), Arg.Any<FundOperatingState?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
                    }
                    form.Close(); completion.SetResult();
                }
                catch (Exception exception) { completion.SetException(exception); }
                finally { context.ExitThread(); }
            });
            System.Windows.Forms.Application.Run(context);
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(25));
    }

    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
}
