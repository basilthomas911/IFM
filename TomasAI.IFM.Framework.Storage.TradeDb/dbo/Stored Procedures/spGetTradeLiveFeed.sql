-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-09-19
-- Description:	get trade live feed
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradeLiveFeed]
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT 
		OrderId,
		TradeId,
		LiveFeed
	from dbo.trade_live_feed
	where OrderId = @orderId
	and TradeId = @tradeId

END
