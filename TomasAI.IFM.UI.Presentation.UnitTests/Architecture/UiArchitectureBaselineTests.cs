using FluentAssertions;

namespace TomasAI.IFM.UI.Presentation.UnitTests.Architecture;

public class UiArchitectureBaselineTests
{
    static readonly string[] UiProjects =
    [
        "TomasAI.IFM.UI.Net",
        "TomasAI.IFM.UI.Net.Views",
        "TomasAI.IFM.UI.Net.ViewModels",
        "TomasAI.IFM.UI.Net.Models",
        "TomasAI.IFM.UI.EventConsumer"
    ];

    static readonly string[] SharedPresentationProjects =
    [
        "TomasAI.IFM.UI.Net.ViewModels",
        "TomasAI.IFM.UI.Net.Models",
        "TomasAI.IFM.UI.EventConsumer"
    ];

    [Fact]
    public void UiProjects_DoNotUseSyncOverAsync()
    {
        const string pattern = @"\.Wait\s*\(|\.Result\b|GetAwaiter\s*\(\s*\)\.GetResult\s*\(";
        var matches = SolutionSource.FindFilesWithMatches(
            SolutionSource.GetSourceFiles(UiProjects),
            pattern);

        matches.Should().BeEmpty(
            "UI work must remain naturally asynchronous and must not block the UI thread");
    }

    [Fact]
    public void SharedPresentationProjects_DoNotReferenceWinFormsOrWpf()
    {
        const string pattern =
            @"System\.Windows\.Forms|System\.Windows\.Threading|System\.Windows\.Controls|System\.Windows\.Media|System\.Windows\.Input";
        var matches = SolutionSource.FindFilesWithMatches(
            SolutionSource.GetSourceFiles(SharedPresentationProjects),
            pattern);

        matches.Should().BeEmpty(
            "shared Models, ViewModels, and event consumers must remain hostable by either WinForms or WPF");
    }

    [Fact]
    public void SharedPresentationProjects_DoNotUseFrameworkPresentationTypes()
    {
        const string pattern = @"\bMessageBox\b|\bUserControl\b|\bSystem\.Drawing\b";
        var matches = SolutionSource.FindFilesWithMatches(
            SolutionSource.GetSourceFiles(SharedPresentationProjects),
            pattern);

        matches.Should().BeEmpty(
            "shared presentation state and behavior must use semantic contracts instead of WinForms or drawing types");
    }

    [Fact]
    public void SharedPresentationProjects_RemainFrameworkNeutralTargets()
    {
        foreach (var projectName in SharedPresentationProjects)
        {
            var projectPath = Path.Combine(
                SolutionSource.RootPath,
                projectName,
                $"{projectName}.csproj");
            var project = File.ReadAllText(projectPath);

            project.Should().Contain("<TargetFramework>net10.0</TargetFramework>");
            project.Should().NotContain("<UseWindowsForms>true</UseWindowsForms>");
            project.Should().NotContain("<UseWPF>true</UseWPF>");
        }
    }

    [Fact]
    public void AsyncVoid_IsRestrictedToKnownWinFormsEventAdapters()
    {
        var matches = SolutionSource.FindFilesWithMatches(
            SolutionSource.GetSourceFiles(UiProjects),
            @"async\s+void\b");

        matches.Should().BeEquivalentTo(
        [
            "TomasAI.IFM.UI.Net.Views/App/IFMAppView.cs",
            "TomasAI.IFM.UI.Net.Views/App/MarketEconomicCalendarView.cs",
            "TomasAI.IFM.UI.Net.Views/App/StatusConsoleView.cs",
            "TomasAI.IFM.UI.Net.Views/Fund/AdjustFundTransactionEditor.cs",
            "TomasAI.IFM.UI.Net.Views/Fund/FundTransactionEditor.cs",
            "TomasAI.IFM.UI.Net.Views/MarketData/MarketDataForm.cs",
            "TomasAI.IFM.UI.Net.Views/MarketData/YieldCurveRateEditForm.cs",
            "TomasAI.IFM.UI.Net.Views/Reference/ReferenceForm.cs",
            "TomasAI.IFM.UI.Net.Views/SystemAdmin/SystemAdminForm.cs",
            "TomasAI.IFM.UI.Net.Views/SystemInfo/SystemWaitView.cs",
            "TomasAI.IFM.UI.Net.Views/Trade/CreateFundOrderTradeForm.cs",
            "TomasAI.IFM.UI.Net.Views/Trade/CreateFundOrderForm.cs",
            "TomasAI.IFM.UI.Net.Views/Trade/CreateFundForm.cs",
            "TomasAI.IFM.UI.Net.Views/Trade/IronCondor/IronCondorView.cs",
            "TomasAI.IFM.UI.Net.Views/Trade/TradeEndOfDayForm.cs",
            "TomasAI.IFM.UI.Net.Views/Trade/TradeOrderEditorForm.cs"
        ]);
    }

    [Theory]
    [InlineData(@"\.Execute\s*\(\s*async\b", 0, "Action-based async Model executions")]
    [InlineData(@"_appRoot\.Execute\s*\(\s*async\b", 0, "Action-based async application-root executions")]
    [InlineData(@"GetForm\s*<", 0, "application-root form service-locator calls")]
    [InlineData(@"catch(?:\s*\([^)]*\))?\s*\{\s*\}", 15, "empty catch blocks")]
    [InlineData(@"Task\.Run\s*\(", 0, "Task.Run calls")]
    [InlineData(@"System\.Threading\.Timer|new\s+System\.Timers\.Timer", 0, "unowned background timers")]
    [InlineData(@"Process\.(?:GetCurrentProcess\(\)\.)?Kill\s*\(|GetCurrentProcess\(\)\.Kill\s*\(", 0, "forced process termination calls")]
    [InlineData(@"\.(?:Post|BeginInvoke)\s*\(", 144, "fire-and-forget UI dispatch calls")]
    public void KnownTechnicalDebt_DoesNotExceedRecordedBaseline(
        string pattern,
        int maximumCount,
        string finding)
    {
        var count = SolutionSource.CountMatches(
            SolutionSource.GetSourceFiles(UiProjects),
            pattern);

        count.Should().BeLessThanOrEqualTo(
            maximumCount,
            $"new {finding} must not be introduced while Stage 1 removes the existing baseline");
    }

    [Fact]
    public void SharedPresentationProjects_DoNotExposeDrawingColors()
    {
        var files = SolutionSource.FindFilesWithMatches(
            SolutionSource.GetSourceFiles(SharedPresentationProjects),
            @"using\s+System\.Drawing\s*;|\bSystem\.Drawing\.Color\b");

        files.Should().BeEmpty(
            "shared ViewModels expose semantic presentation roles that each UI framework maps to its own palette");
    }

    [Fact]
    public void FrameworkAdapters_AreIsolatedToWinFormsViews()
    {
        var files = SolutionSource.FindFilesWithMatches(
            SolutionSource.GetSourceFiles(UiProjects),
            @"class\s+\w+[^\r\n]*:\s*(?:IUiDispatcher|IUserInteraction|IViewNavigator)");

        files.Should().BeEquivalentTo(
        [
            "TomasAI.IFM.UI.Net.Views/Presentation/WinFormsUiDispatcher.cs",
            "TomasAI.IFM.UI.Net.Views/Presentation/WinFormsUserInteraction.cs",
            "TomasAI.IFM.UI.Net.Views/Presentation/WinFormsViewNavigator.cs"
        ]);
    }

    [Fact]
    public void FundUiConsumer_RoutesEveryAdjustmentCompletionAndFailureEvent()
    {
        var sourcePath = Path.Combine(
            SolutionSource.RootPath,
            "TomasAI.IFM.UI.EventConsumer",
            "FundUIEventConsumer.cs");
        var source = File.ReadAllText(sourcePath);
        var eventTypes = new[]
        {
            "OpeningTradeFundTransactionAdjustmentCreatedCompleteEvent",
            "OpeningTradeFundTransactionAdjustmentCreatedFailEvent",
            "RealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent",
            "RealizedTradePnlFundTransactionAdjustmentCreatedFailEvent",
            "TradeCommissionFundTransactionAdjustmentCreatedCompleteEvent",
            "TradeCommissionFundTransactionAdjustmentCreatedFailEvent",
            "UnrealizedTradePnlFundTransactionAdjustmentCreatedCompleteEvent",
            "UnrealizedTradePnlFundTransactionAdjustmentCreatedFailEvent"
        };

        foreach (var eventType in eventTypes)
            source.Should().Contain($"AsEvent<{eventType}>");
    }
}
