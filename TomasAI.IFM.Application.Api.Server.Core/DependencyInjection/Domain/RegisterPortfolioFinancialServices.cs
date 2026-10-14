using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.DependencyInjection.Domain;

/// <summary>Registers Portfolio domain financial repositories and projection coordination.</summary>
internal static class RegisterPortfolioFinancialServices
{
    internal static void Register(IServiceCollection services)
    {
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.OrderComposition.Repository.PortfolioOrderCompositionStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.OrderComposition.Repository.PortfolioCloseOrderCompositionStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.PortfolioFinancialSchema>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.LedgerFinancialProjection>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.LedgerConfigurationProjection>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.CapacityFinancialProjection>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialAuthorityProjection>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.PortfolioOrderCompositionProjection>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialReadProjectionRebuilder>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialEventLogReader>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialProjectionBaseline>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.BrokerAccountingIntentStore>();
        services.AddSingleton(provider => new TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialDevelopmentPolicy(
        provider.GetRequiredService<IHostEnvironment>().IsDevelopment()));
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.IGeneralLedgerStore,
        TomasAI.IFM.Domain.Portfolio.Financial.Repository.GeneralLedgerStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.ICapacityReservationStore,
        TomasAI.IFM.Domain.Portfolio.Financial.Repository.CapacityReservationStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model.FinancialIdentityAllocator>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialAuthorityPreparationStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.IFinancialHistoryProjection,
        TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialHistoryProjection>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.IFinancialQueryStore,
        TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialQueryStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi>(provider =>
        new TomasAI.IFM.Application.Api.Nats.Client.PortfolioFinancialApi(
        provider.GetRequiredService<IActorProducer>()));
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.IPortfolioOrderCompositionApi>(provider =>
        (TomasAI.IFM.Application.Api.Nats.Client.PortfolioFinancialApi)provider.GetRequiredService<
        TomasAI.IFM.Domain.Portfolio.Shared.Financial.IPortfolioFinancialApi>());
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.AccountingExportStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.EmulatorExecutionStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.IPortfolioAuthorityFence,
        TomasAI.IFM.Domain.Portfolio.Financial.Repository.PortfolioAuthorityFence>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialHistoryJournal>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.ILedgerConfigurationStore,
        TomasAI.IFM.Domain.Portfolio.Financial.Repository.LedgerConfigurationStore>();
        // Financial history recovery, workflow recovery, and capacity expiry polling are
        // temporarily disabled pending review of retry behavior and persisted-data compatibility.
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialWorkflowRecoveryJournal>();
        services.AddSingleton<TomasAI.IFM.Domain.Portfolio.Financial.Repository.CapacityExpiryDispatchStore>();
    }
}
