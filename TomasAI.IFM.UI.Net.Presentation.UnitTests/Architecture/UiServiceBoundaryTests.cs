using FluentAssertions;
using System.Xml.Linq;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Architecture;

/// <summary>Protects the completed Services-to-Models presentation boundary.</summary>
public sealed class UiServiceBoundaryTests
{
    static readonly string[] ModelsReferenceAllowList =
    [
        "TomasAI.IFM.Domain.Portfolio.Shared",
        "TomasAI.IFM.Domain.MarketData.Analytics.Shared",
        "TomasAI.IFM.Domain.MarketData.Shared",
        "TomasAI.IFM.Domain.SystemAdmin.Shared",
        "TomasAI.IFM.Domain.Trade.Shared"
    ];

    static readonly string[] DocumentedBoundaryDtoFiles =
    [
        "App/IFMAppViewModel.cs", "App/MarketEconomicCalendarViewModel.cs",
        "App/StatusConsoleViewModel.cs", "Contracts/ITradeOrderConfirmationService.cs",
        "Extensions/LookupTypeListExtension.cs", "Fund/AdjustFundTransactionViewModel.cs",
        "Fund/CreateFundViewModel.cs", "Fund/FundCashTransactionViewModel.cs",
        "Fund/FundTransactionEditorViewModel.cs", "Fund/FundTransactionUIViewModel.cs",
        "MarketData/FuturesContractEditorViewModel.cs", "MarketData/FuturesEodDataUIViewModel.cs",
        "MarketData/FuturesOptionContractEditorViewModel.cs",
        "MarketData/FuturesTradeSignalUIViewModel.cs", "MarketData/FuturesTradeStatusUIViewModel.cs",
        "MarketData/YieldCurveRateEditorViewModel.cs",
        "Operations/FuturesItiSignalEventRow.cs", "Trade/EndOfDayProcessViewModel.cs",
        "Portfolio/PortfolioAdministrationViewModel.cs",
        "Trade/FundOrderEditorViewModel.cs", "Trade/IronCondor/IronCondorTradeInfoViewModel.cs",
        "Trade/IronCondor/IronCondorTradeOrderViewModel.cs",
        "Trade/IronCondor/IronCondorViewModel.cs", "Trade/TradeOrderConfirmationViewModel.cs",
        "Trade/TradeOrderEditorViewModel.cs"
    ];

    /// <summary>Ensures Models retains only shared value-contract references required by UI records and policies.</summary>
    [Fact]
    public void ModelsProjectReferences_AreLimitedToUiValueContracts()
        => ReadProjectReferences("TomasAI.IFM.UI.Net.Models")
            .Should().BeEquivalentTo(ModelsReferenceAllowList);

    /// <summary>Ensures every UI model source remains free of transport APIs, consumers, and service execution.</summary>
    [Fact]
    public void AllModels_AreFreeOfTransportAndExecutionConcerns()
        => SolutionSource.FindFilesWithMatches(
                SolutionSource.GetSourceFiles(["TomasAI.IFM.UI.Net.Models"]),
                @"ServiceApi|UI\.EventConsumer|Framework\.Messaging|Application\.Api|IUiService|UiServiceBase")
            .Should().BeEmpty();

    /// <summary>Ensures the generic legacy model resolver and its execution contracts cannot return.</summary>
    [Fact]
    public void ProductionSources_DoNotUseLegacyModelResolution()
    {
        var projects = new[]
        {
            "TomasAI.IFM.UI.Net", "TomasAI.IFM.UI.Net.Views", "TomasAI.IFM.UI.Net.ViewModels",
            "TomasAI.IFM.UI.Net.Services", "TomasAI.IFM.UI.Net.Models"
        };
        SolutionSource.FindFilesWithMatches(
                SolutionSource.GetSourceFiles(projects),
                @"GetModel\s*<|\bIModel\s*<|\bBaseModel\s*<|\bIEventModel\b")
            .Should().BeEmpty();
    }

    /// <summary>Ensures ViewModels use the UI pricing boundary rather than the pricing framework directly.</summary>
    [Fact]
    public void ViewModels_DoNotReferenceOptionPricerFramework()
    {
        SolutionSource.FindFilesWithMatches(
                SolutionSource.GetSourceFiles(["TomasAI.IFM.UI.Net.ViewModels"]),
                @"Framework\.OptionPricer")
            .Should().BeEmpty();
        ReadProject("TomasAI.IFM.UI.Net.ViewModels")
            .Should().NotContain("TomasAI.IFM.Framework.OptionPricer");
    }

    /// <summary>Ensures every remaining backend read DTO usage is explicitly documented as a workflow boundary.</summary>
    [Fact]
    public void BackendReadDtoUsage_IsLimitedToDocumentedWorkflowBoundaries()
    {
        var root = Path.Combine(SolutionSource.RootPath, "TomasAI.IFM.UI.Net.ViewModels");
        var actual = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("ReadModel", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        actual.Should().BeEquivalentTo(DocumentedBoundaryDtoFiles);
    }

    /// <summary>Ensures the intended one-way dependency graph is wired.</summary>
    [Fact]
    public void ViewModelsReferenceServices_AndServicesReferenceModels()
    {
        ReadProject("TomasAI.IFM.UI.Net.ViewModels").Should().Contain("TomasAI.IFM.UI.Net.Services");
        ReadProject("TomasAI.IFM.UI.Net.Services").Should().Contain("TomasAI.IFM.UI.Net.Models");
        ReadProject("TomasAI.IFM.UI.Net.Models").Should().NotContain("TomasAI.IFM.UI.Net.Services");
    }

    /// <summary>Ensures the typed catalog exposes named services and no generic resolution method.</summary>
    [Fact]
    public void ServiceCatalog_IsTypedAndContainsNoGenericResolver()
    {
        var catalog = File.ReadAllText(Path.Combine(
            SolutionSource.RootPath, "TomasAI.IFM.UI.Net.Services", "IUiServiceCatalog.cs"));
        catalog.Should().Contain("FundCommandService FundCommands");
        catalog.Should().Contain("IOptionPricingService OptionPricing");
        catalog.Should().NotMatchRegex(@"\b(Get|Resolve)\s*<");
    }

    /// <summary>Ensures all superseded execution adapters are absent from Models.</summary>
    [Fact]
    public void LegacyAdapterSources_AreRemovedFromModels()
    {
        var legacyFiles = new[]
        {
            "ApplicationEventModel.cs", "BaseModel.cs", "DatabaseBackupModel.cs",
            "EconomicCalendarEventModel.cs", "EndOfDayProcessEventModel.cs", "EventModel.cs",
            "FundCommandModel.cs", "FundEventModel.cs", "FundOrderEventModel.cs", "FundQueryModel.cs",
            "LookupTypeEventModel.cs", "MarketDataAnalyticsCommandModel.cs",
            "MarketDataAnalyticsEventModel.cs", "MarketDataAnalyticsQueryModel.cs",
            "MarketDataCommandModel.cs", "MarketDataEventModel.cs", "MarketDataFeedCommandModel.cs",
            "MarketDataFeedQueryModel.cs", "MarketDataQueryModel.cs",
            "OptionTradeSpreadBarDataEventModel.cs", "ReferenceCommandModel.cs",
            "ReferenceQueryModel.cs", "SpreadDistributionJobModel.cs", "StatusConsoleModel.cs",
            "TradeCommandModel.cs", "TradePlacementCommandModel.cs", "TradePlacementEventModel.cs",
            "TradePlanActionEventModel.cs", "TradePlanEventModel.cs", "TradePlanQueryModel.cs",
            "TradePositionFeedEventModel.cs", "TradeQueryModel.cs"
        };
        legacyFiles.Should().OnlyContain(file => !File.Exists(Path.Combine(
            SolutionSource.RootPath, "TomasAI.IFM.UI.Net.Models", file)));
        Directory.Exists(Path.Combine(SolutionSource.RootPath, "TomasAI.IFM.UI.Net.Models", "Contracts"))
            .Should().BeFalse();
    }

    static string[] ReadProjectReferences(string projectName)
    {
        var project = XDocument.Load(Path.Combine(
            SolutionSource.RootPath, projectName, $"{projectName}.csproj"));
        return project.Descendants("ProjectReference")
            .Select(element => Path.GetFileNameWithoutExtension(
                element.Attribute("Include")?.Value.Replace('\\', Path.DirectorySeparatorChar)))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray();
    }

    static string ReadProject(string projectName)
        => File.ReadAllText(Path.Combine(
            SolutionSource.RootPath, projectName, $"{projectName}.csproj"));
}
