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

            static EconomicCalendarReadModel MapToEconomicCalendar(IObjectMapReader<EconomicCalendarReadModel> o)
                => new(
                    o.Get(e => e.EventDate),
                    o.Get(e => e.CountryCode),
                    o.Get(e => e.EventName),
                    o.Get(e => e.Actual),
                    o.Get(e => e.Forecast),
                    o.Get(e => e.Prior),
                    o.Get(e => e.CreatedOn),
                    o.Get(e => e.CreatedBy)
                );
        }
    }
}
