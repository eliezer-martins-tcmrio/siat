USE [CSLIB_UserManager]
GO

SET XACT_ABORT ON

BEGIN TRY
	BEGIN TRANSACTION

	-- APLICAÇÃO (CONFIRMAR O VALOR ANTES DE EXECUTAR, VER O AVISO NO CABEÇALHO)
	DECLARE @AppId INT = 93
	DECLARE @AppName VARCHAR(50) = 'SIAT'

	IF EXISTS (SELECT * FROM Apps WHERE [Id] = @AppId AND [Name] <> @AppName)
	BEGIN
		RAISERROR ('O Id %d da tabela Apps já pertence a outra aplicação. Ajuste @AppId para um valor livre.', 16, 1, @AppId)
	END

	IF EXISTS (SELECT * FROM Apps WHERE [Name] = @AppName AND [Id] <> @AppId)
	BEGIN
		RAISERROR ('A aplicação SIAT já existe em Apps com outro Id. Ajuste @AppId para o Id existente.', 16, 1)
	END

	IF NOT EXISTS (SELECT * FROM Apps WHERE [Name] = @AppName)
	BEGIN
		INSERT INTO Apps ([Id], [Name])
		VALUES (@AppId, @AppName)
	END

	DECLARE @SIAT_AppID INT = (SELECT [Id] FROM Apps WHERE [Name] = @AppName)

	-- RECURSOS
	-- Um recurso por módulo libera os três ambientes (DSV, HML e PRD).
	DECLARE @Recurso_MailQueue INT = 10 -- Fila de e-mails
	DECLARE @Recurso_AuditLog INT = 20 -- Log de auditoria
	DECLARE @Recurso_Catalogo INT = 30 -- Catálogo de sistemas
	DECLARE @Recurso_Versoes INT = 40 -- Versões de sistemas

	-- INSERINDO RECURSOS
	IF NOT EXISTS (SELECT * FROM AppResources WHERE [AppId] = @SIAT_AppID AND [Id] = @Recurso_MailQueue)
	BEGIN
		INSERT INTO AppResources ([AppId], [Id], [ParentId], [Description], [ResourceKey])
		VALUES (@SIAT_AppID, @Recurso_MailQueue, NULL, 'Consulta à fila de e-mails', 'MailQueue');
	END

	IF NOT EXISTS (SELECT * FROM AppResources WHERE [AppId] = @SIAT_AppID AND [Id] = @Recurso_AuditLog)
	BEGIN
		INSERT INTO AppResources ([AppId], [Id], [ParentId], [Description], [ResourceKey])
		VALUES (@SIAT_AppID, @Recurso_AuditLog, NULL, 'Consulta ao log de auditoria', 'AuditLog');
	END

	IF NOT EXISTS (SELECT * FROM AppResources WHERE [AppId] = @SIAT_AppID AND [Id] = @Recurso_Catalogo)
	BEGIN
		INSERT INTO AppResources ([AppId], [Id], [ParentId], [Description], [ResourceKey])
		VALUES (@SIAT_AppID, @Recurso_Catalogo, NULL, 'Catálogo de sistemas', 'Catalogo_Sistema');
	END

	IF NOT EXISTS (SELECT * FROM AppResources WHERE [AppId] = @SIAT_AppID AND [Id] = @Recurso_Versoes)
	BEGIN
		INSERT INTO AppResources ([AppId], [Id], [ParentId], [Description], [ResourceKey])
		VALUES (@SIAT_AppID, @Recurso_Versoes, NULL, 'Versões de sistemas', 'Versoes_Sistema');
	END

	-- GRUPOS
	DECLARE @NomeGrupoConsultas VARCHAR(50) = 'Consultas'
	DECLARE @GRP_CONSULTAS INT
	DECLARE @NomeGrupoSistemas VARCHAR(50) = 'Sistemas'
	DECLARE @GRP_SISTEMAS INT

	-- INSERINDO GRUPOS (o Id do grupo é sequencial na tabela inteira, como no SiCATI)
	-- Grupo Consultas
	IF NOT EXISTS (SELECT * FROM Groups WHERE [Name] = @NomeGrupoConsultas AND [AppId] = @SIAT_AppID)
	BEGIN
		SELECT @GRP_CONSULTAS = ISNULL(MAX([Id]), 0) + 1
		FROM Groups

		INSERT INTO Groups ([AppId], [Id], [Name])
		VALUES (@SIAT_AppID, @GRP_CONSULTAS, @NomeGrupoConsultas);
	END

	SELECT @GRP_CONSULTAS = [Id]
	FROM Groups
	WHERE [Name] = @NomeGrupoConsultas AND [AppId] = @SIAT_AppID

	-- Grupo Sistemas
	IF NOT EXISTS (SELECT * FROM Groups WHERE [Name] = @NomeGrupoSistemas AND [AppId] = @SIAT_AppID)
	BEGIN
		SELECT @GRP_SISTEMAS = ISNULL(MAX([Id]), 0) + 1
		FROM Groups

		INSERT INTO Groups ([AppId], [Id], [Name])
		VALUES (@SIAT_AppID, @GRP_SISTEMAS, @NomeGrupoSistemas);
	END

	SELECT @GRP_SISTEMAS = [Id]
	FROM Groups
	WHERE [Name] = @NomeGrupoSistemas AND [AppId] = @SIAT_AppID

	-- INSERINDO OS ACESSOS AOS RECURSOS DOS GRUPOS (somente CanSelect = 1)
	DECLARE @aid INT

	-- GRUPO CONSULTAS: MailQueue
	IF NOT EXISTS (SELECT * FROM Accesses WHERE [AppId] = @SIAT_AppID AND [ResourceId] = @Recurso_MailQueue AND [GroupId] = @GRP_CONSULTAS)
	BEGIN
		SELECT @aid = ISNULL(MAX([Id]), 0) + 1
		FROM Accesses

		INSERT INTO Accesses ([Id], [AppId], [ResourceId], [GroupId], [CanSelect], [CanInsert], [CanUpdate], [CanDelete], [CanExecute], [CanPrint], [CanSpecial1], [CanSpecial2], [CanSpecial3])
		VALUES (@aid, @SIAT_AppID, @Recurso_MailQueue, @GRP_CONSULTAS, 1, 0, 0, 0, 0, 0, 0, 0, 0);
	END

	-- GRUPO CONSULTAS: AuditLog
	IF NOT EXISTS (SELECT * FROM Accesses WHERE [AppId] = @SIAT_AppID AND [ResourceId] = @Recurso_AuditLog AND [GroupId] = @GRP_CONSULTAS)
	BEGIN
		SELECT @aid = ISNULL(MAX([Id]), 0) + 1
		FROM Accesses

		INSERT INTO Accesses ([Id], [AppId], [ResourceId], [GroupId], [CanSelect], [CanInsert], [CanUpdate], [CanDelete], [CanExecute], [CanPrint], [CanSpecial1], [CanSpecial2], [CanSpecial3])
		VALUES (@aid, @SIAT_AppID, @Recurso_AuditLog, @GRP_CONSULTAS, 1, 0, 0, 0, 0, 0, 0, 0, 0);
	END

	-- GRUPO SISTEMAS: Catalogo
	IF NOT EXISTS (SELECT * FROM Accesses WHERE [AppId] = @SIAT_AppID AND [ResourceId] = @Recurso_Catalogo AND [GroupId] = @GRP_SISTEMAS)
	BEGIN
		SELECT @aid = ISNULL(MAX([Id]), 0) + 1
		FROM Accesses

		INSERT INTO Accesses ([Id], [AppId], [ResourceId], [GroupId], [CanSelect], [CanInsert], [CanUpdate], [CanDelete], [CanExecute], [CanPrint], [CanSpecial1], [CanSpecial2], [CanSpecial3])
		VALUES (@aid, @SIAT_AppID, @Recurso_Catalogo, @GRP_SISTEMAS, 1, 0, 0, 0, 0, 0, 0, 0, 0);
	END

	-- GRUPO SISTEMAS: Versoes
	IF NOT EXISTS (SELECT * FROM Accesses WHERE [AppId] = @SIAT_AppID AND [ResourceId] = @Recurso_Versoes AND [GroupId] = @GRP_SISTEMAS)
	BEGIN
		SELECT @aid = ISNULL(MAX([Id]), 0) + 1
		FROM Accesses

		INSERT INTO Accesses ([Id], [AppId], [ResourceId], [GroupId], [CanSelect], [CanInsert], [CanUpdate], [CanDelete], [CanExecute], [CanPrint], [CanSpecial1], [CanSpecial2], [CanSpecial3])
		VALUES (@aid, @SIAT_AppID, @Recurso_Versoes, @GRP_SISTEMAS, 1, 0, 0, 0, 0, 0, 0, 0, 0);
	END

	COMMIT TRANSACTION
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0
		ROLLBACK TRANSACTION;

	THROW;
END CATCH
