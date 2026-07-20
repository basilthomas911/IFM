-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertStrikePriceVolatility] 
	-- Add the parameters for the stored procedure here
	@symbol varchar(10),
	@tradeType varchar(32),
	@marketTrend varchar(32),
	@marketVolatility varchar(32),
	@minDelta int,
	@maxDelta int,
	@strikePriceOffset int

AS
BEGIN
	if exists(select * from strike_price_volatility
		where Symbol = @symbol and TradeType = @tradeType and MarketTrend = @marketTrend and MarketVolatility = @marketVolatility)
		update strike_price_volatility_map
		set MinDelta = @minDelta,
			MaxDelta = @maxDelta,
			StrikePriceOffset = @strikePriceOffset
		where Symbol = @symbol 
		and TradeType = @tradeType 
		and MarketTrend = @marketTrend 
		and MarketVolatility = @marketVolatility
	else
		insert into [dbo].[strike_price_volatility]
			   ([Symbol]
			   ,[TradeType]
			   ,[MarketTrend]
			   ,[MarketVolatility]
			   ,[MinDelta]
			   ,[MaxDelta]
			   ,[StrikePriceOffset])
		 values
			   (@symbol
			   ,@tradeType
			   ,@marketTrend
			   ,@marketVolatility
			   ,@minDelta
			   ,@maxDelta
			   ,@strikePriceOffset)

END
