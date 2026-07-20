-- =============================================
-- Author:		Basil Thomas	
-- Create date: 2019-03-07
-- Description:	return scheduled jobs
-- =============================================
CREATE PROCEDURE [dbo].[spGetScheduledJobs]
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT [JobId]
      ,[JobName]
      ,[JobSchedule]
      ,[JobScheduleDate]
      ,[JobScheduleInterval]
      ,[TaskName]
	  ,[TaskEnabled]
      ,[CreatedOn]
      ,[CreatedBy]
      ,[UpdatedOn]
      ,[UpdatedBy]
	FROM [dbo].[scheduled_job]

END
