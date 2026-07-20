-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradeTypeLimits] 
	-- Add the parameters for the stored procedure here
	@tradeId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT TradeId
      ,TradeType
      ,MaxLossLimit
      ,MinProfitLimit
   	FROM dbo.trade_type_limit 
	where Tradeid = @tradeId

END
