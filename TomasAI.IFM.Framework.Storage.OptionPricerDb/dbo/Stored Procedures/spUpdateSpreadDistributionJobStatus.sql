-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateSpreadDistributionJobStatus]
	-- Add the parameters for the stored procedure here
	@jobId int,
	@jobCompleted datetime,
	@jobStatus varchar(4000)
AS
BEGIN

	update dbo.spread_distribution_job
	set JobCompleted = @jobCompleted,
		JobStatus = @jobStatus
	where JobId = @jobId

END
