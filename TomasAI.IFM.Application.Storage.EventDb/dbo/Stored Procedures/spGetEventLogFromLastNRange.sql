-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetEventLogFromLastNRange] 
	-- Add the parameters for the stored procedure here
	@entityId bigint,
	@entityTypeId bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select 
		el.EventId,
		el.EventSourceId,
        el.EventSourceVersion,
        et.EventTypeName,
        el.EventData,
        el.EventDate
    from dbo.event_log el join dbo.event_type et on el.EventTypeId = et.EventTypeId
	where el.EventSourceId in (select es.EventSourceId from dbo.event_source es where es.EntityId = @entityId and es.EntityTypeId = @entityTypeId)
    order by el.EventId desc;

END
