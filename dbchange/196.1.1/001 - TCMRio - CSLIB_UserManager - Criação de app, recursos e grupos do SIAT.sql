USE [CSLIB_UserManager]
GO

/*
	Cadastro do SIAT no UserManager: aplicação, recursos, grupos e acessos.
	Contrato: specs/001-consultas-visor/contracts/usermanager.md

	Script idempotente: pode ser executado mais de uma vez sem duplicar registros.
	Executar em DSV e em PRD (o mesmo valor de @AppId nos dois ambientes).

	*****************************************************************************
	* ATENÇÃO: o valor de @AppId abaixo (178) é provisório.                     *
	* ANTES de executar, CONFIRMAR o maior Id da tabela Apps em DSV e em PRD    *
	*     SELECT MAX([Id]) FROM Apps                                            *
	* (em DSV o maior Id era 177 quando o script foi escrito) e usar um valor   *
	* livre nos dois ambientes. O mesmo valor deve ser informado em             *
	* UserManagerService:ApplicationId nos appsettings do SIAT.                 *
	*****************************************************************************

	Os recursos dos módulos futuros (Sistemas, Versoes, Documentacao, Copilot,
	CopilotProprio, Builds) entram nos scripts das suas próprias features.
*/

SET XACT_ABORT ON

BEGIN TRY
	BEGIN TRANSACTION

	-- APLICAÇÃO (CONFIRMAR O VALOR ANTES DE EXECUTAR, VER O AVISO NO CABEÇALHO)
	DECLARE @AppId INT = 178
	DECLARE @AppName VARCHAR(50) = 'SIAT'

	IF EXISTS (
			SELECT *
			FROM Apps
			WHERE [Id] = @AppId
				AND [Name] <> @AppName
			)
	BEGIN
		RAISERROR ('O Id %d da tabela Apps já pertence a outra aplicação. Ajuste @AppId para um valor livre.', 16, 1, @AppId)
	END

	IF EXISTS (
			SELECT *
			FROM Apps
			WHERE [Name] = @AppName
				AND [Id] <> @AppId
			)
	BEGIN
		RAISERROR ('A aplicação SIAT já existe em Apps com outro Id. Ajuste @AppId para o Id existente.', 16, 1)
	END

	IF NOT EXISTS (
			SELECT *
			FROM Apps
			WHERE [Name] = @AppName
			)
	BEGIN
		INSERT INTO Apps ([Id], [Name])
		VALUES (@AppId, @AppName)
	END

	DECLARE @SIAT_AppID INT = (
			SELECT [Id]
			FROM Apps
			WHERE [Name] = @AppName
			)

	-- RECURSOS
	DECLARE @Recurso_MailQueue INT = 10 -- Fila de e-mails (DSV/HML)
	DECLARE @Recurso_MailQueuePRD INT = 11 -- Fila de e-mails (PRD)
	DECLARE @Recurso_AuditLog INT = 20 -- Log de auditoria (DSV/HML)
	DECLARE @Recurso_AuditLogPRD INT = 21 -- Log de auditoria (PRD)

	-- INSERINDO RECURSOS
	IF NOT EXISTS (
			SELECT *
			FROM AppResources
			WHERE [AppId] = @SIAT_AppID
				AND [Id] = @Recurso_MailQueue
			)
	BEGIN
		INSERT INTO AppResources ([AppId], [Id], [ParentId], [Description], [ResourceKey])
		VALUES (@SIAT_AppID, @Recurso_MailQueue, NULL, 'Consulta à fila de e-mails (DSV/HML)', 'MailQueue');
	END

	IF NOT EXISTS (
			SELECT *
			FROM AppResources
			WHERE [AppId] = @SIAT_AppID
				AND [Id] = @Recurso_MailQueuePRD
			)
	BEGIN
		INSERT INTO AppResources ([AppId], [Id], [ParentId], [Description], [ResourceKey])
		VALUES (@SIAT_AppID, @Recurso_MailQueuePRD, NULL, 'Consulta à fila de e-mails (PRD)', 'MailQueuePRD');
	END

	IF NOT EXISTS (
			SELECT *
			FROM AppResources
			WHERE [AppId] = @SIAT_AppID
				AND [Id] = @Recurso_AuditLog
			)
	BEGIN
		INSERT INTO AppResources ([AppId], [Id], [ParentId], [Description], [ResourceKey])
		VALUES (@SIAT_AppID, @Recurso_AuditLog, NULL, 'Consulta ao log de auditoria (DSV/HML)', 'AuditLog');
	END

	IF NOT EXISTS (
			SELECT *
			FROM AppResources
			WHERE [AppId] = @SIAT_AppID
				AND [Id] = @Recurso_AuditLogPRD
			)
	BEGIN
		INSERT INTO AppResources ([AppId], [Id], [ParentId], [Description], [ResourceKey])
		VALUES (@SIAT_AppID, @Recurso_AuditLogPRD, NULL, 'Consulta ao log de auditoria (PRD)', 'AuditLogPRD');
	END

	-- GRUPOS
	DECLARE @NomeGrupoConsultas VARCHAR(50) = 'Consultas'
	DECLARE @NomeGrupoConsultasPRD VARCHAR(50) = 'Consultas PRD'
	DECLARE @GRP_CONSULTAS INT
	DECLARE @GRP_CONSULTAS_PRD INT

	-- INSERINDO GRUPOS (o Id do grupo é sequencial na tabela inteira, como no SiCATI)
	IF NOT EXISTS (
			SELECT *
			FROM Groups
			WHERE [Name] = @NomeGrupoConsultas
				AND [AppId] = @SIAT_AppID
			)
	BEGIN
		SELECT @GRP_CONSULTAS = ISNULL(MAX([Id]), 0) + 1
		FROM Groups

		INSERT INTO Groups ([AppId], [Id], [Name])
		VALUES (@SIAT_AppID, @GRP_CONSULTAS, @NomeGrupoConsultas);
	END

	SELECT @GRP_CONSULTAS = [Id]
	FROM Groups
	WHERE [Name] = @NomeGrupoConsultas
		AND [AppId] = @SIAT_AppID

	IF NOT EXISTS (
			SELECT *
			FROM Groups
			WHERE [Name] = @NomeGrupoConsultasPRD
				AND [AppId] = @SIAT_AppID
			)
	BEGIN
		SELECT @GRP_CONSULTAS_PRD = ISNULL(MAX([Id]), 0) + 1
		FROM Groups

		INSERT INTO Groups ([AppId], [Id], [Name])
		VALUES (@SIAT_AppID, @GRP_CONSULTAS_PRD, @NomeGrupoConsultasPRD);
	END

	SELECT @GRP_CONSULTAS_PRD = [Id]
	FROM Groups
	WHERE [Name] = @NomeGrupoConsultasPRD
		AND [AppId] = @SIAT_AppID

	-- INSERINDO OS ACESSOS AOS RECURSOS DOS GRUPOS (somente CanSelect = 1)
	DECLARE @aid INT

	-- GRUPO CONSULTAS: MailQueue
	IF NOT EXISTS (
			SELECT *
			FROM Accesses
			WHERE [AppId] = @SIAT_AppID
				AND [ResourceId] = @Recurso_MailQueue
				AND [GroupId] = @GRP_CONSULTAS
			)
	BEGIN
		SELECT @aid = ISNULL(MAX([Id]), 0) + 1
		FROM Accesses

		INSERT INTO Accesses ([Id], [AppId], [ResourceId], [GroupId], [CanSelect], [CanInsert], [CanUpdate], [CanDelete], [CanExecute], [CanPrint], [CanSpecial1], [CanSpecial2], [CanSpecial3])
		VALUES (@aid, @SIAT_AppID, @Recurso_MailQueue, @GRP_CONSULTAS, 1, 0, 0, 0, 0, 0, 0, 0, 0);
	END

	-- GRUPO CONSULTAS: AuditLog
	IF NOT EXISTS (
			SELECT *
			FROM Accesses
			WHERE [AppId] = @SIAT_AppID
				AND [ResourceId] = @Recurso_AuditLog
				AND [GroupId] = @GRP_CONSULTAS
			)
	BEGIN
		SELECT @aid = ISNULL(MAX([Id]), 0) + 1
		FROM Accesses

		INSERT INTO Accesses ([Id], [AppId], [ResourceId], [GroupId], [CanSelect], [CanInsert], [CanUpdate], [CanDelete], [CanExecute], [CanPrint], [CanSpecial1], [CanSpecial2], [CanSpecial3])
		VALUES (@aid, @SIAT_AppID, @Recurso_AuditLog, @GRP_CONSULTAS, 1, 0, 0, 0, 0, 0, 0, 0, 0);
	END

	-- GRUPO CONSULTAS PRD: MailQueuePRD
	IF NOT EXISTS (
			SELECT *
			FROM Accesses
			WHERE [AppId] = @SIAT_AppID
				AND [ResourceId] = @Recurso_MailQueuePRD
				AND [GroupId] = @GRP_CONSULTAS_PRD
			)
	BEGIN
		SELECT @aid = ISNULL(MAX([Id]), 0) + 1
		FROM Accesses

		INSERT INTO Accesses ([Id], [AppId], [ResourceId], [GroupId], [CanSelect], [CanInsert], [CanUpdate], [CanDelete], [CanExecute], [CanPrint], [CanSpecial1], [CanSpecial2], [CanSpecial3])
		VALUES (@aid, @SIAT_AppID, @Recurso_MailQueuePRD, @GRP_CONSULTAS_PRD, 1, 0, 0, 0, 0, 0, 0, 0, 0);
	END

	-- GRUPO CONSULTAS PRD: AuditLogPRD
	IF NOT EXISTS (
			SELECT *
			FROM Accesses
			WHERE [AppId] = @SIAT_AppID
				AND [ResourceId] = @Recurso_AuditLogPRD
				AND [GroupId] = @GRP_CONSULTAS_PRD
			)
	BEGIN
		SELECT @aid = ISNULL(MAX([Id]), 0) + 1
		FROM Accesses

		INSERT INTO Accesses ([Id], [AppId], [ResourceId], [GroupId], [CanSelect], [CanInsert], [CanUpdate], [CanDelete], [CanExecute], [CanPrint], [CanSpecial1], [CanSpecial2], [CanSpecial3])
		VALUES (@aid, @SIAT_AppID, @Recurso_AuditLogPRD, @GRP_CONSULTAS_PRD, 1, 0, 0, 0, 0, 0, 0, 0, 0);
	END

	COMMIT TRANSACTION
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0
		ROLLBACK TRANSACTION;

	THROW;
END CATCH
