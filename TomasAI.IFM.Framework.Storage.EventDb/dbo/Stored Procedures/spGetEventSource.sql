-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetEventSource] 
	-- Add the parameters for the stored procedure here
	@entityId bigint,
	@entityTypeId bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
 
	SELECT es.[EventSourceId]
      ,es.[EntityId]
	  ,et.[EntityTypeId]
      ,es.[EventSourceVersion]
      ,es.[EventSourceDate]
	FROM event_source es join entity_type et on es.EntityTypeId = et.EntityTypeId
	where es.EntityId = @entityId
	and et.EntityTypeId = @entityTypeId

END
