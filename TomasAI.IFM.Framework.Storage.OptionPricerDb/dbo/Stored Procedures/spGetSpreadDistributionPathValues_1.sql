-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spGetSpreadDistributionPathValues 
	-- Add the parameters for the stored procedure here
	@spreadDistributionId bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here

	SELECT [Id]
		  ,[SpreadDistributionId]
		  ,[DaysToMaturity]
		  ,[SpreadValue]
	FROM [dbo].[spread_distribution_path_value]
	where SpreadDistributionId = @spreadDistributionId
END
