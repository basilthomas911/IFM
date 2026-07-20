
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradePositions] 
	-- Add the parameters for the stored procedure here
	@orderid int,
	@tradeId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here

	SELECT 
	   [OrderId]
	  ,[TradeId]
      ,[TradeType]
      ,[ValueDate]
      ,[DaysToExpiry]
      ,[TradeStatus]
      ,[Commission]
      ,[DeltaHedge]
      ,[NetSpread]
      ,[TradeValue]
      ,[TradePnl]
      ,[AssetPrice]
      ,[OTMProbability]
      ,[ForwardPrice]
	  ,[ForwardLossRatio]
      ,[LossProbability]
      ,[RiskFreeRate]
      ,[CreatedOn]
      ,[CreatedBy]
      ,[UpdatedOn]
      ,[UpdatedBy]
	FROM [dbo].[trade_position]
	where Orderid = @orderId 
	and TradeId = @tradeId

END
