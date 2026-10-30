using static TomasAI.IFM.Shared.Storage.BindValues;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal sealed record OptionExpiryCalendarState(
    Guid Generation,
    DateOnly CoverageFrom,
    DateOnly CoverageThrough,
    DateTime RefreshedAtUtc);
