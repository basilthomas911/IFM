-- =============================================
-- Author:		Basil Thomas
-- Create date: Dec 26, 2019
-- Description:	return market volatility strike price offsets
-- =============================================
CREATE PROCEDURE [dbo].[spGetMarketVolatilityStrikePriceOffsets]
	-- Add the parameters for the stored procedure here
	@symbol varchar(10)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

   
	SELECT 
	    [Symbol]
      ,[MarketTrend]
      ,[MarketVolatility]
      ,[StrikePriceOffset]
	FROM [dbo].[market_volatility_strike_price_offset]
	WHERE Symbol = @symbol

END
