-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetEventSourceVersion] 
	@entityId bigint,
	@entityTypeId bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    if (exists(select * from dbo.event_source where EntityId = @entityId and EntityTypeId = @entityTypeId)) 
		select EventSourceVersion 
		from dbo.event_source
		where EntityId = @entityId 
		and EntityTypeId = @entityTypeId;
	else
		select cast(0 as bigint ) EventSourceVersion;
	
END
