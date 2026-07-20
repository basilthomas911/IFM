
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetCurrentSeedId]
	-- Add the parameters for the stored procedure here
	@seedType varchar(32)
AS
BEGIN
	
	select NextSeedId 
	from dbo.seed_id
	where SeedType = @seedType
END
