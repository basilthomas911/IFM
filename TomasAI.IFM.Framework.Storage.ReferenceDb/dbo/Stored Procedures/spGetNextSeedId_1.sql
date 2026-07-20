
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetNextSeedId]
	-- Add the parameters for the stored procedure here
	@seedType varchar(32)
AS
BEGIN
	
	if (exists(select * from dbo.seed_id where SeedType = @seedType))
		update dbo.seed_id
		set NextSeedId = NextSeedId+1
		where SeedType = @seedType
	else
		insert into dbo.seed_id (
			NextSeedId,
			SeedType
		) values (
			1001,
			@seedType
		)
	select NextSeedId 
	from dbo.seed_id
	where SeedType = @seedType
END
