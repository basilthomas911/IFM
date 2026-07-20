-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetLookupTypeShortCodes] 
	-- Add the parameters for the stored procedure here
	@lookupTypeName varchar(64)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select
		ShortCode,
		OrderId
	from dbo.lookup_type
	where LookupTypeName = @lookupTypeName
	order by OrderId

END
