USE [CSLIB_UserManager]
GO

-- Gestão › Copilot › Meu consumo (specs/005-copilot-meu-consumo, contracts/usermanager.md).
-- Recurso CopilotProprio (consumo próprio do Copilot) e grupo Copilot, com CanSelect. Idempotente: pode ser executado
-- de novo sem duplicar nada. Não inclui usuários: quem entra no grupo Copilot é decisão da coordenação.

SET XACT_ABORT ON

BEGIN TRY
	BEGIN TRANSACTION

	-- APLICAÇÃO (criada pelo script 001)
	DECLARE @AppName VARCHAR(50) = 'SIAT'
	DECLARE @SIAT_AppID INT = (SELECT [Id] FROM Apps WHERE [Name] = @AppName)

	IF @SIAT_AppID IS NULL
	BEGIN
		RAISERROR ('A aplicação SIAT não existe em Apps. Execute antes o script 001 desta pasta.', 16, 1)
	END

	-- RECURSO
	DECLARE @Recurso_CopilotProprio INT = 50 -- Consumo próprio do Copilot

	IF EXISTS (SELECT * FROM AppResources WHERE [AppId] = @SIAT_AppID AND [Id] = @Recurso_CopilotProprio AND [ResourceKey] <> 'CopilotProprio')
	BEGIN
		RAISERROR ('O Id %d de AppResources do SIAT já pertence a outro recurso. Ajuste @Recurso_CopilotProprio para um valor livre.', 16, 1, @Recurso_CopilotProprio)
	END

	IF NOT EXISTS (SELECT * FROM AppResources WHERE [AppId] = @SIAT_AppID AND [Id] = @Recurso_CopilotProprio)
	BEGIN
		INSERT INTO AppResources ([AppId], [Id], [ParentId], [Description], [ResourceKey])
		VALUES (@SIAT_AppID, @Recurso_CopilotProprio, NULL, 'Consumo próprio do Copilot', 'CopilotProprio');
	END

	-- GRUPO (o Id do grupo é sequencial na tabela inteira, como no script 001)
	DECLARE @NomeGrupoCopilot VARCHAR(50) = 'Copilot'
	DECLARE @GRP_COPILOT INT

	IF NOT EXISTS (SELECT * FROM Groups WHERE [Name] = @NomeGrupoCopilot AND [AppId] = @SIAT_AppID)
	BEGIN
		SELECT @GRP_COPILOT = ISNULL(MAX([Id]), 0) + 1
		FROM Groups

		INSERT INTO Groups ([AppId], [Id], [Name])
		VALUES (@SIAT_AppID, @GRP_COPILOT, @NomeGrupoCopilot);
	END

	SELECT @GRP_COPILOT = [Id]
	FROM Groups
	WHERE [Name] = @NomeGrupoCopilot AND [AppId] = @SIAT_AppID

	-- ACESSO DO GRUPO AO RECURSO (somente CanSelect = 1)
	DECLARE @aid INT

	-- GRUPO COPILOT: CopilotProprio
	IF NOT EXISTS (SELECT * FROM Accesses WHERE [AppId] = @SIAT_AppID AND [ResourceId] = @Recurso_CopilotProprio AND [GroupId] = @GRP_COPILOT)
	BEGIN
		SELECT @aid = ISNULL(MAX([Id]), 0) + 1
		FROM Accesses

		INSERT INTO Accesses ([Id], [AppId], [ResourceId], [GroupId], [CanSelect], [CanInsert], [CanUpdate], [CanDelete], [CanExecute], [CanPrint], [CanSpecial1], [CanSpecial2], [CanSpecial3])
		VALUES (@aid, @SIAT_AppID, @Recurso_CopilotProprio, @GRP_COPILOT, 1, 0, 0, 0, 0, 0, 0, 0, 0);
	END

	COMMIT TRANSACTION
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0
		ROLLBACK TRANSACTION;

	THROW;
END CATCH
