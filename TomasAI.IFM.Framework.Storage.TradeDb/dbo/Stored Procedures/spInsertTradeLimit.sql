-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertTradeLimit]
	-- Add the parameters for the stored procedure here
	@tradeid int,
	@tradeType varchar(32),
	@riskMargin money,
	@maxProfit money,
	@maxLoss money,
	@maxReturn real,
	@maxLossLimit real,
	@minProfitLimit real,
	@minProfitTarget money,
	@dailyProfitTarget money,
	@createdOn datetime,
	@createdBy varchar(64),
	@updatedOn datetime,
	@updatedBy varchar(64)
AS
BEGIN
	
	if not exists(select * from dbo.trade_limit where TradeId = @tradeId)
		INSERT INTO [dbo].[trade_limit]
				   ([TradeId]
				   ,[TradeType]
				   ,[RiskMargin]
				   ,[MaxProfit]
				   ,[MaxLoss]
				   ,[MaxReturn]
				   ,[MaxLossLimit]
				   ,[MinProfitLimit]
				   ,[MinProfitTarget]
				   ,[DailyProfitTarget]
				   ,[CreatedOn]
				   ,[CreatedBy]
				   ,[UpdatedOn]
				   ,[UpdatedBy])
			 VALUES
				   (@tradeid
				   ,@tradeType
				   ,@riskMargin
				   ,@maxProfit
				   ,@maxLoss
				   ,@maxReturn
				   ,@maxLossLimit
				   ,@minProfitLimit
				   ,@minProfitTarget
				   ,@dailyProfitTarget
				   ,@createdOn
				   ,@createdBy
				   ,@updatedOn
				   ,@updatedBy)
	else
		update dbo.trade_limit
		set TradeType = @tradeType,
			RiskMargin = @riskMargin,
			MaxProfit = @maxProfit,
			MaxLoss = @maxLoss,
			MaxReturn = @maxReturn,
			MaxLossLimit = @maxLossLimit,
			MinProfitLimit = @minProfitLimit,
			MinProfitTarget = @minProfitTarget,
			DailyProfitTarget = @dailyProfitTarget,
			UpdatedOn = @updatedOn,
			UpdatedBy = @updatedBy
		where TradeId = @tradeId

END