USE CSLIB_UserManager;
GO

DECLARE @AppId INT = 93;
DECLARE @UserId INT = 902089;
DECLARE @OrganizationUnitId INT = 250;

-- Concede ao usuário todos os grupos do app SIAT
INSERT INTO dbo.UserGroupOrganizationUnit
    (Users_Id, Groups_Id, OrganizationUnits_Id)
SELECT
    @UserId,
    g.Id,
    @OrganizationUnitId
FROM dbo.Groups g
WHERE g.AppId = @AppId
  AND NOT EXISTS
(
    SELECT 1
    FROM dbo.UserGroupOrganizationUnit ugo
    WHERE ugo.Users_Id = @UserId
      AND ugo.Groups_Id = g.Id
      AND ISNULL(ugo.OrganizationUnits_Id, -1) = ISNULL(@OrganizationUnitId, -1)
);
