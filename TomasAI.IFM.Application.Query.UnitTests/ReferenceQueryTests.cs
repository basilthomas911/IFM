using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Queries;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Query.UnitTests
{
    public class ReferenceQueryTests
    {
        [Fact]
        public async Task LookupTypeShortCodeExistsOk()
        {
            var q = new GetLookupTypeShortCodeExistsQuery
            {
                LookupTypeName = "Currency",
                ShortCode = "USD"
            };
            var dbConn = new DbConnectionSettings()
                .Add("ReferenceDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=referenceDb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, ReferenceDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            var dbFactory = new DbContextFactory(dbResolver);
            var dbCache = new DbCache();
            diContainer.Add(typeof(IObjectRepository<ReferenceDbContext>), new ReferenceDbContext(dbConn, dbFactory));
            var referenceQuery = new ReferenceQueries(dbFactory);
            var shortCodeExists = await referenceQuery.ExecuteAsync(q);
            shortCodeExists.Should().NotBeNull();
            shortCodeExists.Value.Should().BeTrue();
        }
    }
}
