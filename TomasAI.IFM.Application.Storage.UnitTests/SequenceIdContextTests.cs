using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using SharedSequenceService = TomasAI.IFM.Shared.Identity.ISequenceIdDbContext;

namespace TomasAI.IFM.Application.Storage.UnitTests;

public sealed class SequenceIdContextTests
{
    [Fact]
    public void Capabilities_and_factory_keep_one_repository_and_the_shared_allocator_contract()
    {
        SequenceIdDbContext context = null!;
        var factory = new DbContextFactory(new DbContextResolver(_ => context));
        var settings = new DbConnectionSettings().Add(SequenceIdDbContext.SequenceIdDbConnection,
            "Host=127.0.0.1;Port=1;Database=ifm_unit_sequence", "System.Data.Postgres");
        context = new(settings, factory, NullLogger<DbProvider>.Instance);
        Assert.Same(context, factory.SequenceIdDb);
        Assert.Same(context, context.Database);
        Assert.Same(context, context.DbReader);
        Assert.Same(context, context.DbWriter);
        Assert.IsAssignableFrom<SharedSequenceService>(context);
    }

    [Fact]
    public void Read_capability_cannot_allocate_identifiers_and_write_capability_only_allocates()
    {
        Assert.Equal(new[] { "GetCurrentSequenceIdAsync", "GetSequenceAllocationSizeAsync" },
            typeof(ISequenceIdDbReadContext).GetMethods().Select(method => method.Name).Order(StringComparer.Ordinal));
        Assert.Equal("GetNextSequenceIdAsync", Assert.Single(typeof(ISequenceIdDbWriteContext).GetMethods()).Name);
        Assert.True(typeof(SharedSequenceService).IsAssignableFrom(typeof(ISequenceIdDbContext)));
        Assert.Equal(typeof(ISequenceIdDbContext), typeof(IDbContextFactory).GetProperty(nameof(IDbContextFactory.SequenceIdDb))!.PropertyType);
    }
}
