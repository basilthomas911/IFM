-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spDeleteTradeTypeLimit 
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@tradeType varchar(32)
AS
BEGIN

	delete from dbo.trade_type_limit
	where TradeId = @tradeId 
	and TradeType = @tradeType

END
