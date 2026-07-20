-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetIronCondorTradePrice] 
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	declare @NetPrice money
	declare @PutForwardPrice money
	declare @CallForwardPrice money

	select @NetPrice = sum(NetSpread) from trade_position where TradeId = @tradeId and TradeStatus = 'IntraDay' and ValueDate = @valueDate
	select @PutForwardPrice = ForwardPrice from trade_position where TradeId = @tradeId and TradeStatus = 'IntraDay' and TradeType = 'PutCreditSpread' and ValueDate = @valueDate
	select @CallForwardPrice = ForwardPrice from trade_position where TradeId = @tradeId and TradeStatus = 'IntraDay' and TradeType = 'CallCreditSpread' and ValueDate = @valueDate
	select  
		@tradeId as TradeId,
		@valueDate as ValueDate,
		@NetPrice as NetPrice, 
		ABS(@PutForwardPrice-@CallForwardPrice) as NetForwardPrice 
END
