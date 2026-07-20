-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertSpreadDistributionJob] 
	-- Add the parameters for the stored procedure here
	@jobId int,
	@orderId int,
	@tradeId int,
	@tradeType varchar(32),
	@tradeStatus varchar(32),
	@valueDate date,
	@daysToExpiry int,
	@optionStyle varchar(32),
	@optionType varchar(32),
	@jobSubmitted datetime,
	@jobStatus varchar(4000),
	@lossProbabilityFactor real
AS
BEGIN

	if exists(select * from spread_distribution_job where JobId = @jobId)
		update spread_distribution_job
			set Orderid = @orderId,
				TradeId = @tradeId,
				TradeType = @tradeType,
				ValueDate = @valueDate,
				DaysToExpiry = @daysToExpiry,
				OptionStyle = @optionStyle,
				OptionType = @optionType,
				JobSubmitted = @jobSubmitted,
				JobStatus = @jobStatus,
				InProgress = 1,
				LossProbabilityFactor = @lossProbabilityFactor 
		where JobId = @jobId
	else
		INSERT INTO [dbo].[spread_distribution_job]
			   ([JobId]
			   ,[OrderId]
			   ,[TradeId]
			   ,[TradeType]
			   ,[TradeStatus]
			   ,[ValueDate]
			   ,[DaysToExpiry]
			   ,[OptionStyle]
			   ,[OptionType]
			   ,[JobSubmitted]
			   ,[JobStatus]
			   ,[InProgress]
			   ,[LossProbabilityFactor])
		 VALUES
			   (@jobId
			   ,@orderId
			   ,@tradeId
			   ,@tradeType
			   ,@tradeStatus
			   ,@valueDate
			   ,@daysToExpiry
			   ,@optionStyle
			   ,@optionType
			   ,@jobSubmitted
			   ,@jobStatus
			   ,1
			   ,@lossProbabilityFactor)

END
