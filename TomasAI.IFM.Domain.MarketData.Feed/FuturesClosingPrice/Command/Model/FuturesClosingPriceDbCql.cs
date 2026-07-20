namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesClosingPrice.Command.Model;

internal static class FuturesClosingPriceDbCql
{
	public const string InsertFuturesClosingPrice = """
		INSERT INTO futures_closing_price (
			contractId,
			valueDate,
			closingPrice,
			createdOn,
			createdBy
		) VALUES (
			:contractId,
			:valueDate,
			:closingPrice,
			:createdOn,
			:createdBy
		);
		""";
}
