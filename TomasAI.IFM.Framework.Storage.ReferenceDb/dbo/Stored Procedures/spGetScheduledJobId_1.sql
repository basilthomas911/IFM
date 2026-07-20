-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-03-07
-- Description:	return scheduled job id
-- =============================================
CREATE PROCEDURE spGetScheduledJobId
	-- Add the parameters for the stored procedure here
	@jobName varchar(255)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT JobId from scheduled_job where JobName = @jobName
END
