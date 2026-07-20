-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-03-07
-- Description:	insert scheduled job
-- =============================================
CREATE PROCEDURE [dbo].[spInsertScheduledJob]
	-- Add the parameters for the stored procedure here
	@jobId int,
	@jobName varchar(255),
	@jobSchedule varchar(64),
	@jobScheduleDate datetime,
	@jobScheduleInterval real,
	@taskName varchar(64),
	@taskEnabled bit,
	@createdOn datetime,
	@createdBy varchar(64)
AS
BEGIN
	
	if exists(select * from scheduled_job where JobName = @jobName)
		update scheduled_job
		set JobSchedule = @jobSchedule,
			JobScheduleDate = @jobScheduleDate,
			JobScheduleInterval = @jobScheduleInterval,
			TaskName = @taskName,
			TaskEnabled = @taskEnabled,
			UpdatedOn = @createdOn,
			UpdatedBy = @createdBy
		where JobName = @jobName
	else
		insert into [dbo].[scheduled_job]
			([JobId]
			,[JobName]
			,[JobSchedule]
			,[JobScheduleDate]
			,[JobScheduleInterval]
			,[TaskName]
			,[TaskEnabled]
			,[CreatedOn]
			,[CreatedBy]
			,[UpdatedOn]
			,[UpdatedBy])
		values
			(@jobId
			,@jobName
			,@jobSchedule
			,@jobScheduleDate
			,@jobScheduleInterval
			,@taskName
			,@taskEnabled
			,@createdOn
			,@createdBy
			,@createdOn
			,@createdBy)

END
