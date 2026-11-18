-- Período: últimos @Dias dias ou, com @Dias nulo, de @De (inclusive) a @Ate (exclusive; o repositório passa o dia
-- seguinte à data final). audit.Log só tem o índice clusterizado em LogID, então os filtros por AppID e por LogUID
-- varriam a tabela inteira. Como o LogID cresce com a data, uma busca binária no LogID (umas 25 leituras pontuais
-- no índice) converte cada ponta do período numa faixa de LogID, e a consulta lê só essa faixa. As datas também
-- entram no WHERE, para o período ser exato. O OPTION (RECOMPILE) usa os valores de @Corte e @Fim no plano.
DECLARE @Desde datetime = CASE WHEN @Dias IS NULL THEN @De ELSE DATEADD(DAY, -@Dias, GETDATE()) END;
DECLARE @Min bigint, @Max bigint, @Baixo bigint, @Alto bigint, @Meio bigint, @Data datetime;
DECLARE @Corte bigint = NULL, @Fim bigint = NULL;

SELECT @Min = MIN(LogID), @Max = MAX(LogID) FROM audit.Log;

-- @Corte: menor LogID cujo primeiro log a partir dele é de @Desde em diante.
IF @Desde IS NOT NULL AND @Min IS NOT NULL
BEGIN
    SELECT @Baixo = @Min, @Alto = @Max + 1;
    WHILE @Baixo < @Alto
    BEGIN
        SET @Meio = @Baixo + (@Alto - @Baixo) / 2;
        SET @Data = NULL;
        SELECT TOP (1) @Data = Date FROM audit.Log WHERE LogID >= @Meio ORDER BY LogID;

        IF @Data IS NULL OR @Data >= @Desde
            SET @Alto = @Meio;
        ELSE
            SET @Baixo = @Meio + 1;
    END;

    SET @Corte = @Baixo;
END;

-- @Fim: menor LogID cujo primeiro log a partir dele é de @Ate em diante (a faixa vai até antes dele).
IF @Ate IS NOT NULL AND @Min IS NOT NULL
BEGIN
    SELECT @Baixo = @Min, @Alto = @Max + 1;
    WHILE @Baixo < @Alto
    BEGIN
        SET @Meio = @Baixo + (@Alto - @Baixo) / 2;
        SET @Data = NULL;
        SELECT TOP (1) @Data = Date FROM audit.Log WHERE LogID >= @Meio ORDER BY LogID;

        IF @Data IS NULL OR @Data >= @Ate
            SET @Alto = @Meio;
        ELSE
            SET @Baixo = @Meio + 1;
    END;

    SET @Fim = @Baixo;
END;

SELECT TOP (@Top)
       l.LogID,
       l.TypeID,
       ISNULL(t.Type, '(tipo não cadastrado)') AS Type,
       l.Date,
       CAST(l.Message   AS nvarchar(max)) AS Message,
       CONVERT(varchar(36), l.LogUID)     AS LogUID,
       l.AppID,
       CAST(l.Exception AS nvarchar(max)) AS Exception,
       CAST(l.LogEvent  AS nvarchar(max)) AS LogEvent
  FROM audit.Log l
  LEFT JOIN audit.Log_Types t ON t.TypeID = l.TypeID
 WHERE (@Corte IS NULL OR l.LogID >= @Corte)
   AND (@Fim IS NULL OR l.LogID < @Fim)
   AND (@Desde IS NULL OR l.Date >= @Desde)
   AND (@Ate IS NULL OR l.Date < @Ate)
   AND (@AppID IS NULL OR l.AppID = @AppID)
   AND (@SemAppID = 0 OR l.AppID IS NULL)
   AND (@LogUID IS NULL OR CONVERT(char(36), l.LogUID) LIKE @LogUID + '%')
 ORDER BY l.LogID DESC
OPTION (RECOMPILE);