-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-09-19
-- Description:	delete trade live feed
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteTradeLiveFeed]
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int
AS
BEGIN

	delete from trade_live_feed
	where OrderId = @orderId and TradeId = @tradeId
END
