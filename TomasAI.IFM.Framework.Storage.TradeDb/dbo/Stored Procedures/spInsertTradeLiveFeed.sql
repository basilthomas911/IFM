-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-09-19
-- Description:	insert trade live feed
-- =============================================
CREATE PROCEDURE [dbo].[spInsertTradeLiveFeed]
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@liveFeed bit
AS
BEGIN

	insert into trade_live_feed(
		OrderId,
		TradeId,
		LiveFeed)
	values(
		@orderId,
		@tradeId,
		@liveFeed)
END
