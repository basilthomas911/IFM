-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spgetLogEventsByDateRange
	-- Add the parameters for the stored procedure here
	@startDate datetime,
	@endDate datetime
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	SELECT 
	  [Timestamp]
      ,[LogLevel]
      ,[Message]
      ,[ServiceId]
	FROM [dbo].[telemetry_log]
	where Timestamp between @startDate and @endDate

END
