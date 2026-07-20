-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteStrikePriceVolatility] 
	-- Add the parameters for the stored procedure here
	@symbol varchar(10),
	@tradeType varchar(32),
	@marketTrend varchar(32),
	@marketVolatility varchar(32)
AS
BEGIN

	delete from [dbo].[strike_price_volatility]
    where Symbol = @symbol 
	and TradeType = @tradeType 
	and MarketTrend = @marketTrend 
	and MarketVolatility = @marketVolatility

END
