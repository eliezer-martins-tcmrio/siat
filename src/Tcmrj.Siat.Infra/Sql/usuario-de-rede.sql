-- Vínculo do usuário de rede com a matrícula: linha ativa da view do RH e o usuário de mesmo Id no UserManager.
-- TOP (2): basta saber se o vínculo é único. LEFT JOIN: a falta de usuário (ou usuário desabilitado) tem mensagem própria.
SELECT TOP (2)
    f.Matricula,
    u.Name AS Nome,
    u.Email,
    CAST(ISNULL(u.Enabled, 0) AS bit) AS Habilitado
FROM dbo.vwSIRHU_Integracao AS f
LEFT JOIN dbo.Users AS u ON u.Id = f.Matricula
WHERE f.LoginRede = @LoginRede
    AND f.Ativo = 1
