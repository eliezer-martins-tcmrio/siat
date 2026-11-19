-- Usuário do UserManager com o login de rede (sem o domínio). Login é único entre os habilitados (índice filtrado
-- IX_Users_FilterEnabled_UniqueLogin), mas desabilitados podem repetir o login de outro usuário: o habilitado vem
-- primeiro, e um desabilitado só aparece quando não há habilitado.
SELECT TOP (1)
    u.Id AS Matricula,
    u.Name AS Nome,
    u.Email,
    u.Enabled AS Habilitado
FROM dbo.Users AS u
WHERE u.Login = @Login
ORDER BY u.Enabled DESC
