using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
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

    static EconomicCalendarJsonModel MapEconomicCalendar(IObjectDataRecord row)
        => new(
            row.GetString(0),
            row.GetDateTime(1),
            row.GetString(2),
            row.GetString(3),
            row.GetString(4),
            row.GetString(5),
            row.GetString(6),
            row.GetString(7),
            row.GetString(8));

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
                .ReadAsync(MapEconomicCalendar);
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

    public async Task<ICollection<EconomicCalendarReadModel>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var db = _dbFactory.EconomicCalendarsDb;
            var economicCalendarJson = await db.Use(connectionString => new DataReaderOptions(connectionString))
                .ReadAsync(MapEconomicCalendar, cancellationToken);
            var economicCalendars = new List<EconomicCalendarReadModel>();
            foreach (var e in economicCalendarJson)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    economicCalendars.Add(e.ToViewModel());
                }
                catch { }
            }
            return economicCalendars;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }

}
