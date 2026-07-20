-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetEntityId] 
	-- Add the parameters for the stored procedure here
	@entityIdValue varchar(900)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	if (not exists(select * from dbo.event_entity_id where Value = @entityIdValue)) 
		insert into dbo.event_entity_id(Value) values(@entityIdValue);
	
    select EntityId 
    from  dbo.event_entity_id 
    where Value = @entityIdValue;
END

