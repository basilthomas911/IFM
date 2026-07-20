-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradeHistory]
	-- Add the parameters for the stored procedure here
	@orderid int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT t.OrderId,
	t.TradeId,
	t.TradeType,
	td.ValueDate,
	td.DaysToExpiry,
	td.TradeStatus,
	sum(td.Commission) as Commission,
	sum(td.NetSpread) as NetSpread,
	sum(td.TradePnl) as TradePnl
	from option_trade t join trade_position td on t.TradeId = td.TradeId
	where t.OrderId = @orderId 
	group by t.OrderId,	t.TradeId, t.TradeType, td.ValueDate, td.DaysToExpiry, td.TradeStatus
	order by td.ValueDate

END
