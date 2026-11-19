-- Usuário do UserManager com o login de rede (sem o domínio), só se for interno e habilitado: desabilitado e usuário
-- externo não entram. Internos (servidores do Tribunal) têm Id abaixo de 1.000.000; os externos começam em 1.000.001.
-- Login é único entre os habilitados (índice filtrado IX_Users_FilterEnabled_UniqueLogin): no máximo uma linha.
SELECT
    u.Id AS Matricula,
    u.Name AS Nome,
    u.Email
FROM dbo.Users AS u
WHERE u.Login = @Login
    AND u.Enabled = 1
    AND u.Id < 1000000
