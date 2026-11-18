USE CSLIB_UserManager;
GO

DECLARE @AppId INT = 93;

DELETE ugo
FROM dbo.UserGroupOrganizationUnit ugo
INNER JOIN dbo.Groups g
    ON g.Id = ugo.Groups_Id
WHERE g.AppId = @AppId
  AND g.Name = 'Consultas'
  AND ugo.OrganizationUnits_Id IS NULL;

;WITH Permissoes AS
(
    SELECT *
    FROM (VALUES
        -- Consultas
        (902089, 'Consultas', 250) -- Eliezer: STI
        , (992089, 'Consultas', 250) -- Eliezer ADMIN: STI
    ) AS V(Users_Id, GroupName, OrganizationUnits_Id)
)
INSERT INTO dbo.UserGroupOrganizationUnit
    (Users_Id, Groups_Id, OrganizationUnits_Id)
SELECT
    p.Users_Id,
    g.Id,
    p.OrganizationUnits_Id
FROM Permissoes p
INNER JOIN dbo.Groups g
    ON g.AppId = @AppId
   AND g.Name = p.GroupName
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.UserGroupOrganizationUnit ugo
    WHERE ugo.Users_Id = p.Users_Id
      AND ugo.Groups_Id = g.Id
      AND ISNULL(ugo.OrganizationUnits_Id, -1) = ISNULL(p.OrganizationUnits_Id, -1)
);
