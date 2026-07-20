-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetEventLogFromSnapshot] 
	-- Add the parameters for the stored procedure here
	@entityId bigint,
	@entityTypeId bigint,
    @snapshotEventTypeId bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	declare @eventId bigint
	declare @eventSourceId bigint
	declare @eventSourceVersion bigint

	select @eventSourceId = es.EventSourceId from dbo.event_source es where es.EntityId = @entityId and es.EntityTypeId = @entityTypeId
	select @eventSourceVersion = max(el.EventSourceVersion) from dbo.event_log el where el.EventTypeId = @snapshotEventTypeId and el.EventSourceId = @eventSourceId
	select @eventId = max(EventId) from dbo.event_log where EventSourceId = @eventSourceId and EventSourceVersion = @eventSourceVersion and EventTypeId = @snapshotEventTypeId 

	if (@eventId is not null)
		select 
			el.EventId,
			el.EventSourceId,
			el.EventSourceVersion,
			et.EventTypeName,
			el.EventData,
			el.EventDate
		from dbo.event_log el join event_type et on el.EventTypeId = et.EventTypeId
		where el.EventSourceId = @eventSourceId and el.EventId >= @eventId
		order by el.EventId;
	else
		select 
			el.EventId,
			el.EventSourceId,
			el.EventSourceVersion,
			et.EventTypeName,
			el.EventData,
			el.EventDate
		from dbo.event_log el join event_type et on el.EventTypeId = et.EventTypeId
		where el.EventSourceId = @eventSourceId
		order by el.EventId;


END
