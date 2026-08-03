using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;
using Xunit;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

namespace TomasAI.IFM.Application.Storage.LoadTests.SqlServer
{
    public class ReferenceDbLoadTests : IClassFixture<ReferenceDatabaseFixture>
    {
        readonly ReferenceDatabaseFixture _testFixture;

        public ReferenceDbLoadTests(ReferenceDatabaseFixture testFixture)
        {
            _testFixture = testFixture;
        }

        [Fact]
        public async Task GetEconomicCalendarsFromCsvFileOk()
        {
            var db = _testFixture.DbFactory.ReferenceDb;
            var resultSet = await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\economic-calendars.csv"))
               .ReadAsync<EconomicCalendarReadModel>(MapToEconomicCalendar);
            resultSet.Should().NotBeNull();
            resultSet.Count.Should().BeGreaterThan(0);
            await db.Use($"truncate economic_calendar").ExecuteCommandAsync();
            var dbRef = db as IReferenceDbContext;
            await dbRef.InsertEconomicCalendarsAsync(resultSet);
            return;

            static EconomicCalendarReadModel MapToEconomicCalendar(IObjectDataRecord o)
                => new(
                    o.GetDateTime(0),
                    o.GetString(1),
                    o.GetString(2),
                    o.GetString(3),
                    o.GetString(4),
                    o.GetString(5),
                    o.GetDateTime(6),
                    o.GetString(7)
                );
        }
    }
}
