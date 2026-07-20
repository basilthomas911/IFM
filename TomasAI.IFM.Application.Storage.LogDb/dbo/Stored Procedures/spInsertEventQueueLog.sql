
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertEventQueueLog] 
	-- Add the parameters for the stored procedure here
	@eventId bigint,
	@eventTypeName varchar(256),
	@eventQueueStatus varchar(32),
	@eventQueueDate datetime,
	@eventFailedMessage varchar(4000)
AS
BEGIN
	
 
	INSERT INTO [dbo].[event_queue_log]
           ([EventId]
           ,[EventTypeName]
           ,[EventQueueStatus]
           ,[EventQueueDate]
           ,[EventFailedMessage])
     VALUES
           (@eventId
           ,@eventTypeName
           ,@eventQueueStatus
           ,@eventQueueDate
           ,@eventFailedMessage)

END
