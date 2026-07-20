

-- =============================================
-- Author:		Basil Thomas
-- Create date: Feb 16, 2018
-- Description:	insert data into spread distribution table
-- =============================================
CREATE PROCEDURE [dbo].[spInsertSpreadDistribution] 
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@tradeType varchar(32),
	@tradeStatus varchar(64),
	@valueDate datetime,
	@daysToExpiry int,
	@forwardPrice real,
	@lossProbability real,
	@shortVolatility real,
	@longVolatility real,
	@lossThreshold money,
	@lossThresholdCount int
AS
BEGIN
	
	insert into dbo.spread_distribution (
		TradeId, TradeType, TradeStatus, ValueDate, DaysToExpiry, ShortVolatility, LongVolatility, ForwardPrice, LossProbability, LossThreshold, LossThresholdCount, CreatedOn
	) values (
		@tradeId, @tradeType, @tradeStatus, @valueDate, @daysToExpiry,  @shortVolatility, @longVolatility,@forwardPrice, @lossProbability, @lossThreshold, @lossThresholdCount, GetDate()
	)
	
END
