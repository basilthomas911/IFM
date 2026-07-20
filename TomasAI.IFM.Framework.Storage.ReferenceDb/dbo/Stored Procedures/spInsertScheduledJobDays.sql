-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertScheduledJobDays] 
	-- Add the parameters for the stored procedure here
	@jobId int,
	@monday bit,
	@tuesday bit,
	@wednesday bit,
	@thursday bit,
	@friday bit,
	@saturday bit,
	@sunday bit
AS
BEGIN

	if not exists(select * from [scheduled_job_days] where JobId = @jobId)
		insert into [dbo].[scheduled_job_days]
			([JobId]
			,[Monday]
			,[Tuesday]
			,[Wednesday]
			,[Thursday]
			,[Friday]
			,[Saturday]
			,[Sunday])
		values
			 (@jobId
			 ,@monday
			 ,@tuesday
			 ,@wednesday
			 ,@thursday
			 ,@friday
			 ,@saturday
			 ,@sunday)
	else
		update scheduled_job_days
			set Monday = @monday,
				Tuesday = @tuesday,
				Wednesday = @wednesday,
				Thursday = @thursday,
				Friday = @friday,
				Saturday = @saturday,
				Sunday = @sunday
		where JobId = @jobId
END
