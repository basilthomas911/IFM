using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Reference.Repository;
namespace TomasAI.IFM.Application.Api.Server.Core.DependencyInjection.Domain;
/// <summary>Registers Reference domain coordination over typed storage contracts.</summary>
internal static class RegisterReferenceServices
{
    internal static void Add(IServiceCollection services)
    {
        services.AddSingleton<TradeStrategyFamilyBootstrapper>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Contracts.ITradeStrategySymbolStore, TradeStrategySymbolStore>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Contracts.IInstrumentDefinitionStore>(provider =>
        new InstrumentDefinitionStore(provider.GetRequiredService<IDbContextFactory>().ReferenceDb,provider.GetRequiredService<TomasAI.IFM.Application.MarketData.Contracts.ITradeStrategySymbolStore>()));
        services.AddSingleton<ITradeStrategyFamilyCatalogStore, TradeStrategyFamilyCatalogStore>();
        services.AddSingleton<TomasAI.IFM.Domain.Reference.TradeStrategyFamilies.TradeStrategyFamilyCreationService>();
        services.AddSingleton<TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogService>();
    }
}
