-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-04-25
-- Description:	return all trade orders by date range
-- =============================================
CREATE PROCEDURE spGetTradeOrders 
	-- Add the parameters for the stored procedure here
	@startDate date,
	@endDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select distinct OrderId, TradeId, TradeType, TradeDate, MaturityDate
	from option_trade
	where TradeDate between @startDate and @endDate
	order by TradeDate
END
