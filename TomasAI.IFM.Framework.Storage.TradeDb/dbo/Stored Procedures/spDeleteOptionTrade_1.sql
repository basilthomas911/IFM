-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteOptionTrade]
	-- Add the parameters for the stored procedure here
	@tradeId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	delete from dbo.option_trade where TradeId = @tradeId
	delete from dbo.trade_position where TradeId = @tradeId
	delete from dbo.option_leg where TradeId = @tradeId
	delete from dbo.option_leg_data where TradeId = @tradeId
	delete from dbo.trade_type_limit where TradeId = @tradeId
	delete from dbo.trade_limit where TradeId = @tradeId
	delete from dbo.trade_fill where TradeId = @tradeId


END
