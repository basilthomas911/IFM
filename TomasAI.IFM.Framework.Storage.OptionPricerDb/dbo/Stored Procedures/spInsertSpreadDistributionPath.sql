-- =============================================
-- Author:		Basil Thomas
-- Create date: Feb 16, 2018
-- Description:	insert data into spread path table
-- =============================================
CREATE PROCEDURE [dbo].[spInsertSpreadDistributionPath] 
	-- Add the parameters for the stored procedure here
	@spreadDistributionId bigint,
	@daysToMaturity int,
	@averagePrice real
AS
BEGIN

	if (exists(select * from dbo.spread_distribution_path where
		SpreadDistributionId = @spreadDistributionId and DaysToMaturity = @daysToMaturity))
		update dbo.spread_distribution_path
		set AveragePrice = @averagePrice
		where SpreadDistributionId = @spreadDistributionId and DaysToMaturity = @daysToMaturity
	else
		insert into dbo.spread_distribution_path (
			SpreadDistributionId, DaysToMaturity, AveragePrice
		) values (
			@spreadDistributionId, @daysToMaturity, @averagePrice
		)

END
