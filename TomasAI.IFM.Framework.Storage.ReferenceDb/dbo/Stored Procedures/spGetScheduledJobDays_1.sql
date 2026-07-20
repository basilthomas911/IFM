-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spGetScheduledJobDays 
	-- Add the parameters for the stored procedure here
	@jobId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    select [JobId]
      ,[Monday]
      ,[Tuesday]
      ,[Wednesday]
      ,[Thursday]
      ,[Friday]
      ,[Saturday]
      ,[Sunday]
	from [dbo].[scheduled_job_days]
	where JobId = @jobId

END
