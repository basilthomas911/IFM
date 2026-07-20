-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetMarketExchanges]
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    select 
		Symbol,
		Exchange, 
		[DayOfWeek],
		MarketOpen, 
		MarketClose
    from dbo.market_exchanges;

END
