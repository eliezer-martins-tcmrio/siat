-- Primeiro escolhe só as chaves das @Top mensagens mais recentes (linhas estreitas, sem corpo nem destinatários);
-- depois busca as colunas largas só dessas. OPTION (RECOMPILE) faz o plano ser montado para os valores do filtro:
-- sem ele, o plano em cache precisa servir para qualquer @SmtpId/@Texto e, sem filtros, percorre todos os
-- destinatários, agrupa e ordena a tabela inteira (tempo esgotado no banco de produção com todos os SMTPs).
-- MessageId desempata mensagens do mesmo minuto (DatePosted é smalldatetime), para o resultado ser estável.
WITH Selecionadas AS (
    SELECT TOP (@Top)
           m.MessageId,
           m.DatePosted
      FROM dbo.Messages m
     WHERE (@SmtpId IS NULL OR m.SmtpId = @SmtpId)
       AND (@Texto IS NULL
            OR m.Subject LIKE @Texto
            OR CAST(m.Message AS nvarchar(max)) LIKE @Texto
            OR CONVERT(varchar(36), m.MessageId) LIKE @Texto
            OR EXISTS (SELECT 1
                         FROM dbo.Recipients r
                        WHERE r.MessageId = m.MessageId
                          AND CAST(r.Recipient AS nvarchar(max)) LIKE @Texto))
     ORDER BY m.DatePosted DESC, m.MessageId
)
SELECT CONVERT(varchar(36), m.MessageId)      AS MessageId,
       m.SmtpId                               AS SmtpId,
       m.Subject                              AS Assunto,
       m.DatePosted                           AS DataPostagem,
       m.SendAfter                            AS EnviarApos,
       m.DateSent                             AS DataEnvio,
       ISNULL(m.Sent, 0)                      AS Enviado,
       m.IsHTML                               AS EhHtml,
       CAST(m.Message AS nvarchar(max))       AS Corpo,
       ISNULL((SELECT STRING_AGG(CAST(r.Recipient AS nvarchar(max)), '; ')
                      WITHIN GROUP (ORDER BY r.Sequencial)
                 FROM dbo.Recipients r
                WHERE r.MessageId = m.MessageId), '(sem destinatários)') AS Destinatarios
  FROM Selecionadas s
  JOIN dbo.Messages m ON m.MessageId = s.MessageId
 ORDER BY s.DatePosted DESC, s.MessageId
OPTION (RECOMPILE)