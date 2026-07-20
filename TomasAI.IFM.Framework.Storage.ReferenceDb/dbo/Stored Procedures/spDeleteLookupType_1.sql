-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteLookupType] 
	-- Add the parameters for the stored procedure here
	@lookupTypeName varchar(64),
	@shortCode varchar(64)
AS
BEGIN
	
	delete from dbo.lookup_type
	where LookupTypeName = @lookupTypeName
	and ShortCode = @shortCode

END
