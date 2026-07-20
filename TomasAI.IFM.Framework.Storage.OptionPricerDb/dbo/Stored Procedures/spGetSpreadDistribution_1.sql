-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetSpreadDistribution] 
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@tradeType varchar(32),
	@tradeStatus varchar(32),
	@valueDate date,
	@daysToExpiry int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT [Id]
		  ,[TradeId]
		  ,[TradeType]
		  ,[TradeStatus]
		  ,[ValueDate]
		  ,[DaysToExpiry]
		  ,[ShortVolatility]
		  ,[LongVolatility]
		  ,[ForwardPrice]
		  ,[LossProbability]
		  ,[LossThreshold]
		  ,[LossThresholdCount]
		  ,[CreatedOn]
	  FROM [dbo].[spread_distribution]
	  where Id in (select max(Id) from dbo.spread_distribution where
		TradeId = @tradeId 
		and TradeType = @tradeType
		and TradeStatus = @tradeStatus 
		and ValueDate = @valueDate 
		and DaysToExpiry = @daysToExpiry)

END
