using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

internal static class MarketDataDbContextTestFactory
{
    public static MarketDataDbContext Create(IDbConnectionSetting connectionSetting)
    {
        var settings = new DbConnectionSettings().Add(
            MarketDataDbContext.MarketDataDbConnection,
            connectionSetting.ConnectionString,
            connectionSetting.ProviderName);
        var repositories = new Dictionary<Type, IObjectRepository>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        var context = new MarketDataDbContext(
            settings,
            factory,
            Substitute.For<IBlackboardService>(),
            Substitute.For<ISequenceIdGenerator>(),
            Substitute.For<ILogger<DbProvider>>());
        repositories.Add(typeof(IObjectRepository<MarketDataDbContext>), context);
        return context;
    }
}
