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
 WHERE (@AppID IS NULL OR l.AppID = @AppID)
   AND (@LogID IS NULL OR CAST(l.LogID AS varchar(20)) LIKE @LogID + '%')
 ORDER BY l.LogID DESC
OPTION (RECOMPILE)