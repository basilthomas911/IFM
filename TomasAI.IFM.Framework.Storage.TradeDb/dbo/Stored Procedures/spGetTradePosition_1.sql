-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradePosition] 
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@tradeType varchar(32),
	@valueDate datetime,
	@daysToExpiry int,
	@tradeStatus varchar(32)
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
	where OrderId = @orderId 
	and TradeId = @tradeId
	and TradeType = @tradeType
	and ValueDate = @valueDate
	and DaysToExpiry = @daysToExpiry
	and TradeStatus = @tradeStatus

END
