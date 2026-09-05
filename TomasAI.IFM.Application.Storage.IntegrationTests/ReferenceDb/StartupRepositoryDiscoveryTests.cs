using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Subscriptions;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

public sealed class StartupRepositoryDiscoveryTests
{
    [Fact]
    public void Discovery_excludes_the_actual_stage4_private_repository_and_keeps_public_contexts()
    {
        var assembly = typeof(PostgresDurableSubscriptionIntentStore).Assembly;
        var owned = typeof(PostgresDurableSubscriptionIntentStore).GetNestedType("Repository", BindingFlags.NonPublic)!;
        owned.IsNestedPrivate.Should().BeTrue();
        // Characterize the exact old scan that caused the production startup exception.
        assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IObjectRepository<>))).Should().Contain(owned);

        var discovered = ObjectRepositoryDiscovery.Discover([assembly, assembly]);
        discovered.Should().NotContain(owned).And.Contain(typeof(ReferenceDbContext)).And.OnlyHaveUniqueItems();
        discovered.Should().OnlyContain(t => t.IsVisible && !t.IsAbstract && !t.ContainsGenericParameters);
    }

    [Fact]
    public void Discovery_ignores_dynamic_assemblies_and_abstract_repository_base()
    {
        var dynamicAssembly = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("RepositoryDiscoveryTest"), System.Reflection.Emit.AssemblyBuilderAccess.Run);
        ObjectRepositoryDiscovery.Discover([dynamicAssembly, typeof(ObjectDataRepository<>).Assembly])
            .Should().NotContain(typeof(ObjectDataRepository<>));
    }
}
