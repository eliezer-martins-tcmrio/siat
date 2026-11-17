SELECT TOP (@Top)
       CONVERT(varchar(36), m.MessageId)      AS MessageId,
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
 ORDER BY m.DatePosted DESC