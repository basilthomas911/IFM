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
            "TomasAI.IFM.UI.Net.Views/Trade/IronCondor/IronCondorView.cs"
        ]);
    }

    [Theory]
    [InlineData(@"\.Execute\s*\(\s*async\b", 189, "Action-based async Model executions")]
    [InlineData(@"_appRoot\.Execute\s*\(\s*async\b", 2, "Action-based async application-root executions")]
    [InlineData(@"catch(?:\s*\([^)]*\))?\s*\{\s*\}", 15, "empty catch blocks")]
    [InlineData(@"Task\.Run\s*\(", 2, "Task.Run calls")]
    [InlineData(@"Process\.(?:GetCurrentProcess\(\)\.)?Kill\s*\(|GetCurrentProcess\(\)\.Kill\s*\(", 3, "forced process termination calls")]
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
    public void PresentationColors_DoNotSpreadBeyondKnownViewModels()
    {
        var files = SolutionSource.FindFilesWithMatches(
            SolutionSource.GetSourceFiles(SharedPresentationProjects),
            @"using\s+System\.Drawing\s*;|\bSystem\.Drawing\.Color\b");

        files.Should().BeEquivalentTo(
        [
            "TomasAI.IFM.UI.Net.ViewModels/MarketData/FuturesEodDataUIViewModel.cs",
            "TomasAI.IFM.UI.Net.ViewModels/MarketData/FuturesTradeSignalUIViewModel.cs",
            "TomasAI.IFM.UI.Net.ViewModels/MarketData/FuturesTradeStatusUIViewModel.cs",
            "TomasAI.IFM.UI.Net.ViewModels/MarketData/PlaceTradeUIViewModel.cs"
        ]);
    }
}
