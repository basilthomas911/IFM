-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-03-07
-- Description:	delete scheduled job
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteScheduledJob] 
	-- Add the parameters for the stored procedure here
	@jobId int
AS
BEGIN
	delete from scheduled_job where JobId = @jobId
	delete from scheduled_job_days where JobId = @jobId
END
