using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Model;

internal static class FuturesClosingPriceDbContext
{
	/// <summary>
	/// Inserts a futures closing price record into the database.
	/// </summary>
	/// <param name="dbFactory">The database context factory.</param>
	/// <param name="e">The closing price read model to persist.</param>
	internal static async ValueTask InsertFuturesClosingPriceAsync(
		this IDbContextFactory dbFactory, FuturesClosingPriceReadModel e)
		=> await dbFactory.MarketDataDb
			.Use(FuturesClosingPriceDbCql.InsertFuturesClosingPrice)
			.SetParameters(new InsertFuturesClosingPrice(
				contractId: e.ContractId,
				valueDate: e.ValueDate,
				closingPrice: e.ClosingPrice,
				createdOn: e.CreatedOn,
				createdBy: e.CreatedBy))
			.ExecuteCommandAsync();

	internal readonly record struct InsertFuturesClosingPrice(
		string contractId,
		DateOnly valueDate,
		decimal closingPrice,
		DateTime createdOn,
		string createdBy) : IBindValue
	{
		public object Bind() => new { contractId, valueDate, closingPrice, createdOn, createdBy };
	}
}
