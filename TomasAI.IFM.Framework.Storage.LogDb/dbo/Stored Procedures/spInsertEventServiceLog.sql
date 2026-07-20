-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertEventServiceLog] 
	@commandId varchar(64),
	@eventDate datetime,
    @eventName varchar(4000),
	@eventData text,
	@userName varchar(255),
	@errorMessage varchar(255),
	@errorCode int,
	@errorType varchar(64),
	@errorData text
AS
BEGIN

	insert into dbo.event_service_log (
		CommandId, 
		EventDate,
        EventName, 
		EventData,
		UserName,
		ErrorMessage,
		ErrorCode,
		ErrorType,
		ErrorData)
    values (
		@commandId, 
        @eventDate, 
        @eventName,
		@eventData,
		@userName,
		@errorMessage,
		@errorCode,
		@errorType,
		@errorData);

END
