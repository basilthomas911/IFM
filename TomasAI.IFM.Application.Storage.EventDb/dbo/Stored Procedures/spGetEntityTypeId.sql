-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-03-23
-- Description:	create unique id for each unique entity type
-- =============================================
CREATE PROCEDURE [dbo].[spGetEntityTypeId]
	-- Add the parameters for the stored procedure here
	@entityTypeName varchar(255)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	if not exists(select * from entity_type where EntityTypeName = @entityTypeName)
		insert into entity_type(EntityTypeName) values (@entityTypeName)
	select EntityTypeId from entity_type where EntityTypeName = @entityTypeName
END
