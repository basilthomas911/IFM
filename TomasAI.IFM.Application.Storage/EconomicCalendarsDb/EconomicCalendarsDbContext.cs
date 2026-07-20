using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Reference.ViewModels;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.Storage.EconomicCalendarsDb;

/// <summary>
/// Represents the database context for managing economic calendar data.
/// </summary>
/// <remarks>This class provides functionality for interacting with the Economic Calendars database,  including
/// mapping models and reading data from external sources. It extends  <see cref="ObjectDataRepository{T}"/> to provide
/// repository-like behavior and implements  <see cref="IEconomicCalendarsDbContext"/> for dependency injection and
/// abstraction.</remarks>
/// <param name="connectionSettings"></param>
/// <param name="dbFactory"></param>
/// <param name="logger"></param>
public class EconomicCalendarsDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<DbProvider> logger) 
    : ObjectDataRepository<EconomicCalendarsDbContext>(connectionSettings[EconomicCalendarsDbConnection], logger), IEconomicCalendarsDbContext
{
    public const string EconomicCalendarsDbConnection = "EconomicCalendarsDbConnection";
    readonly IDbContextFactory _dbFactory = IsArgumentNull.Set(dbFactory);

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override EconomicCalendarsDbContext Database => this;

    /// <summary>
    /// initialize economic calendar view model mappings
    /// </summary>
    /// <param name="model"></param>
    public override void OnCreateModel(DbModel<EconomicCalendarsDbContext> model)
    {
        EconomicCalendars = model.Map(e => e.EconomicCalendars)
            .Parameters(e =>
                e.Set(o => o.Event)
                 .Set(o => o.Date)
                 .Set(o => o.Country)
                 .Set(o => o.Actual)
                 .Set(o => o.Previous)
                 .Set(o => o.Change)
                 .Set(o => o.ChangePercentage)
                 .Set(o => o.Estimate)
                 .Set(o => o.Impact)
            );
    }

    public DbMap<EconomicCalendarJsonModel> EconomicCalendars { get; private set; } = null!;

    /// <summary>
    /// read economic calendars from external web site
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<EconomicCalendarReadModel>> ReadAsync()
    {
        try
        {
            var db = _dbFactory.EconomicCalendarsDb;
            var economicCalendarJson = await db.Use(connectionString => new DataReaderOptions(connectionString))
                .ReadAsync<EconomicCalendarJsonModel>();
            var economicCalendars = new List<EconomicCalendarReadModel>();
            foreach (var e in economicCalendarJson)
            {
                try
                {
                    economicCalendars.Add(e.ToViewModel());
                }
                catch { }
            }
            return economicCalendars;
        }
        catch 
        {
            return [];
        }
    }

}
