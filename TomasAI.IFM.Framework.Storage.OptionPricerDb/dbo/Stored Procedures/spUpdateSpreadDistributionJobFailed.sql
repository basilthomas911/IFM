-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateSpreadDistributionJobFailed]
	-- Add the parameters for the stored procedure here
	@jobId int,
	@jobFailed datetime,
	@jobStatus varchar(4000)
AS
BEGIN

	update dbo.spread_distribution_job
	set JobFailed = @jobFailed,
		JobStatus = @jobStatus,
		InProgress = 0
	where JobId = @jobId

END
