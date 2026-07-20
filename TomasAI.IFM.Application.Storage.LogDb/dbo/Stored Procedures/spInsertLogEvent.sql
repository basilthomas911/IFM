-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spInsertLogEvent 
	-- Add the parameters for the stored procedure here
	@timestamp datetime,
	@logLevel varchar(16),
	@message text,
	@serviceId varchar(64)
AS
BEGIN

	INSERT INTO [dbo].[telemetry_log]
           ([Timestamp]
           ,[LogLevel]
           ,[Message]
           ,[ServiceId])
     VALUES
           (@timestamp
           ,@logLevel
           ,@message
           ,@serviceId)

END
