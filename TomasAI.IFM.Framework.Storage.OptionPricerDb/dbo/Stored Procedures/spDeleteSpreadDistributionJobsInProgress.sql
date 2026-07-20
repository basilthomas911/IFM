-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteSpreadDistributionJobsInProgress] 
	-- Add the parameters for the stored procedure here
AS
BEGIN

	delete from spread_distribution_job
	where InProgress = 1

END
