using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Views.MarketData;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class InstrumentDefinitionImportWorkflowTests
{
    static readonly DateTimeOffset At = new(2026, 9, 8, 16, 0, 0, TimeSpan.Zero);
    static readonly DateTimeOffset Expiry = new(2026, 12, 18, 21, 0, 0, TimeSpan.Zero);
    static InstrumentDefinitionSelection Definition(bool option) => new()
    {
        SnapshotId = Guid.Parse("b1eb88c6-88ed-4ec2-a977-41b53d297498"), Dataset = "GLBX.MDP3", Root = "ES",
        PublisherId = 1, InstrumentId = option ? 42u : 99u, UnderlyingInstrumentId = option ? 99u : 0,
        RawSymbol = option ? "fixture C6500.5" : "fixture ESZ6", InstrumentClass = option ? "C" : "F",
        Currency = "USD", Exchange = "CME", Multiplier = 50, TickSize = .25m,
        Strike = option ? 6500.5m : null, ExpirationUtc = Expiry, DefinitionTimestampUtc = At.AddDays(-1),
        DefinitionDigest = new('a', 64), RawDefinitionReference = "fixture/definition"
    };

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Given_provider_definition_Add_or_Change_previews_exact_reviewed_reference(bool option, bool change)
        => Sta(() =>
        {
            var source = Definition(option);
            var future = InstrumentDefinitionImport.Future(Definition(false), "America/New_York", At);
            var priorOption = option ? InstrumentDefinitionImport.Option(source, future, "America/New_York", At) : null;
            var api = Substitute.For<IMarketDataQueryApi>();
            api.GetInstrumentDefinitionsAsync(Arg.Any<InstrumentDefinitionPageRequest>(), Arg.Any<CancellationToken>())
                .Returns(new ServiceOk<InstrumentDefinitionPage>(new(source.SnapshotId, At.UtcDateTime, [source], null)));
            api.GetFuturesContractAsync(future.ContractId).Returns(new ServiceOk<FuturesContractV3ReadModel>(future));
            using var model = new InstrumentDefinitionSelectorViewModel(new MarketDataQueryService(api, Substitute.For<IMarketDataFeedQueryApi>()));
            string? preview = null;
            using var form = new InstrumentDefinitionSelectorForm(model, option, change && !option ? future : null,
                change ? priorOption : null, new Clock(), (owner, text) =>
                {
                    preview = text;
                    Save(owner, $"import-{option}-{change}");
                    return true;
                });
            ShowOffscreen(form);
            var review = (ReferenceConventionEditor)form.Controls.OfType<PropertyGrid>().Single().SelectedObject!;
            review.ExchangeTimeZoneId = "America/New_York"; review.CalendarVersion = "fixture/calendar";
            review.ReviewState = ReferenceReviewState.Reviewed; review.MappingVersion = change ? "fixture/v2" : "fixture/v1";
            review.EvidenceId = "fixture/review"; review.EffectiveFromUtc = At.AddDays(-1); review.EffectiveUntilUtc = Expiry;
            review.LastTradingUtc = Expiry; review.UnderlyingContractId = future.ContractId;
            review.SettlementStyle = option ? ReferenceSettlementStyle.DeliveryOfFuture : ReferenceSettlementStyle.Cash;
            review.ExerciseStyle = ReferenceExerciseStyle.American; review.PremiumStyle = ReferencePremiumStyle.PremiumPaid;
            review.PremiumTickRule = ReferencePremiumTickRule.Fixed; review.TickRuleVersion = "fixture/ticks";
            review.DayCount = ReferenceDayCount.Actual365Fixed;
            form.Controls.Find("ProviderRoot", true).Single().Text = "ES";
            ((Button)form.Controls.Find("SearchDefinitions", true).Single()).PerformClick();
            Pump(() => form.Controls.OfType<DataGridView>().Single().Rows.Count == 1);
            ((Button)form.Controls.Find("UseDefinition", true).Single()).PerformClick();
            Pump(() => preview is not null);
            Assert.Equal(DialogResult.OK, form.DialogResult);
            if (option)
            {
                Assert.Equal("ES20261218C6500.5", form.Option!.ContractId);
                Assert.Equal(6500.5m, form.Option.StrikePriceDecimal);
                Assert.Equal(99u, form.Option.UnderlyingInstrumentId);
                Assert.Equal(ReferenceExerciseStyle.American, form.Option.ExerciseStyle);
                Assert.Empty(FuturesReferenceQualification.Errors(form.Option));
            }
            else
            {
                Assert.Equal("ES20261218", form.Future!.ContractId);
                Assert.Equal(99u, form.Future.InstrumentId);
                Assert.Empty(FuturesReferenceQualification.Errors(form.Future));
            }
        });

    [Fact]
    public void Given_missing_review_evidence_Use_does_not_confirm_or_close()
        => Sta(() =>
        {
            var source = Definition(false);
            var api = Substitute.For<IMarketDataQueryApi>();
            api.GetInstrumentDefinitionsAsync(Arg.Any<InstrumentDefinitionPageRequest>(), Arg.Any<CancellationToken>())
                .Returns(new ServiceOk<InstrumentDefinitionPage>(new(source.SnapshotId, At.UtcDateTime, [source], null)));
            using var model = new InstrumentDefinitionSelectorViewModel(new MarketDataQueryService(api, Substitute.For<IMarketDataFeedQueryApi>()));
            var confirmed = false;
            using var form = new InstrumentDefinitionSelectorForm(model, false, time: new Clock(), confirm: (_, _) => confirmed = true);
            ShowOffscreen(form);
            var review = (ReferenceConventionEditor)form.Controls.OfType<PropertyGrid>().Single().SelectedObject!;
            review.ExchangeTimeZoneId = "America/New_York"; review.ReviewState = ReferenceReviewState.Reviewed;
            form.Controls.Find("ProviderRoot", true).Single().Text = "ES";
            ((Button)form.Controls.Find("SearchDefinitions", true).Single()).PerformClick();
            Pump(() => form.Controls.OfType<DataGridView>().Single().Rows.Count == 1);
            ((Button)form.Controls.Find("UseDefinition", true).Single()).PerformClick();
            Pump(() => form.Controls.Find("DefinitionStatus", true).Single().Text.Contains("Missing/invalid"));
            Assert.False(confirmed); Assert.Equal(DialogResult.None, form.DialogResult); Assert.Null(form.Future);
            Save(form, "import-validation-failure");
        });

    static void ShowOffscreen(Form form)
    {
        form.StartPosition = FormStartPosition.Manual; form.Location = new(-32000, -32000);
        form.ShowInTaskbar = false; form.Show(); System.Windows.Forms.Application.DoEvents();
    }
    static void Save(Form form, string name)
    {
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        var folder = Path.Combine(AppContext.BaseDirectory, "artifacts", "stage2");
        Directory.CreateDirectory(folder);
        bitmap.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
    }
    static void Pump(Func<bool> completed)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!completed() && DateTime.UtcNow < deadline)
        { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(5); }
        Assert.True(completed(), "UI operation did not complete.");
    }
    static void Sta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => At; }
}
