-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertEventLog] 
	@eventSourceId bigint,
    @eventSourceVersion bigint,
    @eventTypeId bigint,
    @eventDate datetime,
	@commandId varchar(64)
AS
BEGIN
	insert into dbo.event_log (
		EventSourceId, 
        EventSourceVersion, 
        EventTypeId, 
        EventDate,
		CommandId)
    values (
		@eventSourceId, 
        @eventSourceVersion, 
        @eventTypeId, 
        @eventDate,
		@commandId);

		return CAST(@@IDENTITY as bigint)

END
