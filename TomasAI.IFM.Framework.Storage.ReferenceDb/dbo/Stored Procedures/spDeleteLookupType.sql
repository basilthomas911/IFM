-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spDeleteLookupType 
	-- Add the parameters for the stored procedure here
	@lookupTypeName varchar(64)
AS
BEGIN
	
	delete from dbo.lookup_type
	where LookupTypeName = @lookupTypeName

END
