-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-03-24
-- Description:	insert event source
-- =============================================
CREATE PROCEDURE [dbo].[spInsertEventSource] 
	@entityId bigint,
    @entityTypeId bigint,
    @eventSourceVersion bigint,
    @eventSourceDate datetime
AS
BEGIN

	if (exists(select * from dbo.event_source where EntityId = @entityId and EntityTypeId = @entityTypeId)) 
		update dbo.event_source
        set EventSourceVersion = @eventSourceVersion,
			EventSourceDate = @eventSourceDate
		where EntityId = @entityId 
		and EntityTypeId = @entityTypeId;
	else
		insert into dbo.event_source (
			EntityId, 
			EntityTypeId, 
			EventSourceVersion, 
			EventSourceDate)
        values (
			@entityId, 
			@entityTypeId, 
			@eventSourceVersion, 
			@eventSourceDate);

END
