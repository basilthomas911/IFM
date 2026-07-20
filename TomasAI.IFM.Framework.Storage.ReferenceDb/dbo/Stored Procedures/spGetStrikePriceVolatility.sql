-- =============================================
-- Author:		Basil Thomas
-- Create date: 2020-04-06
-- Description:	return strike price volatility map
-- =============================================
CREATE PROCEDURE spGetStrikePriceVolatility 
	-- Add the parameters for the stored procedure here
	@symbol varchar(10),
	@tradeType varchar(32)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT [Symbol]
      ,[TradeType]
      ,[MarketTrend]
      ,[MarketVolatility]
      ,[MarketVolatilityTrend]
      ,[Delta]
      ,[StrikePriceOffset]
	FROM [dbo].[strike_price_volatility]
	where Symbol = @symbol
	and TradeType = @tradeType

END
