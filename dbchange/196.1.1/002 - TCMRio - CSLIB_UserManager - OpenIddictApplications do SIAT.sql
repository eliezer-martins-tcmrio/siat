USE [CSLIB_UserManager]
GO

/*
	Clientes OIDC do SIAT no SIA (OpenIddict).
	Contrato: specs/001-consultas-visor/contracts/usermanager.md

	Script idempotente: um ClientId que já existe não é alterado.

	*****************************************************************************
	* SEGREDOS: este arquivo NÃO contém segredo em claro, nem hash real.        *
	* Antes de executar, substituir os placeholders @HashSecretDev e            *
	* @HashSecretPrd pelo HASH do ClientSecret de cada cliente.                 *
	*                                                                           *
	* Como gerar o hash (mesmo formato dos clientes do SiCATI, "AQAAAAEAACcQ…"):*
	*  - cadastrando o cliente pelo IOpenIddictApplicationManager (o OpenIddict *
	*    calcula o hash do ClientSecret em claro ao criar a aplicação), ou      *
	*  - com Microsoft.AspNetCore.Identity.PasswordHasher<object>               *
	*    .HashPassword(null, "<segredo>"), que produz o mesmo formato.          *
	*                                                                           *
	* O segredo em claro NUNCA entra no repositório: user-secrets em            *
	* desenvolvimento (TcmrjOidc:ClientSecret) e variável de ambiente           *
	* TcmrjOidc__ClientSecret no IIS.                                           *
	*****************************************************************************
*/

SET XACT_ABORT ON

-- Hash do ClientSecret de SIAT-dev. SUBSTITUIR antes de executar.
DECLARE @HashSecretDev NVARCHAR(MAX) = N'<HASH_DO_CLIENTSECRET_SIAT_DEV>'
-- Hash do ClientSecret de SIAT (PRD). SUBSTITUIR antes de executar.
DECLARE @HashSecretPrd NVARCHAR(MAX) = N'<HASH_DO_CLIENTSECRET_SIAT_PRD>'

DECLARE @Permissions NVARCHAR(MAX) = N'["ept:authorization","ept:logout","ept:token","gt:authorization_code","rst:code","scp:profile","scp:openid"]'
DECLARE @Requirements NVARCHAR(MAX) = N'["ft:pkce"]'

-- TODO: confirmar o nome DNS de PRD (questão em aberto 5 do PRD).
-- Se o SIAT for publicado num subdiretório do IIS, o subdiretório entra antes de /signin-oidc e /signout-callback-oidc.
DECLARE @UrlBasePrd NVARCHAR(200) = N'https://siat.tcmrj.tcmnet.tcm'

-- Cliente de desenvolvimento (porta 7271 de src/Tcmrj.Siat.Web/Properties/launchSettings.json)
IF NOT EXISTS (
		SELECT *
		FROM [OpenIddict].[OpenIddictApplications]
		WHERE [ClientId] = 'SIAT-dev'
		)
BEGIN
	IF @HashSecretDev LIKE N'<%'
	BEGIN
		-- Sem RETURN: o cliente de PRD continua independente do de desenvolvimento.
		RAISERROR ('Substitua @HashSecretDev pelo hash do ClientSecret de SIAT-dev antes de executar. SIAT-dev não foi inserido.', 16, 1)
	END
	ELSE
	INSERT [OpenIddict].[OpenIddictApplications] (
		[Id]
		,[ClientId]
		,[ClientSecret]
		,[ConcurrencyToken]
		,[ConsentType]
		,[DisplayName]
		,[DisplayNames]
		,[Permissions]
		,[PostLogoutRedirectUris]
		,[Properties]
		,[RedirectUris]
		,[Requirements]
		,[Type]
		)
	VALUES (
		CONVERT(NVARCHAR(100), NEWID())
		,N'SIAT-dev'
		,@HashSecretDev
		,CONVERT(NVARCHAR(100), NEWID())
		,NULL
		,N'[DEV] SIAT'
		,NULL
		,@Permissions
		,N'["https://localhost:7271/signout-callback-oidc"]'
		,NULL
		,N'["https://localhost:7271/signin-oidc"]'
		,@Requirements
		,N'confidential'
		)
END

-- Cliente de produção
IF NOT EXISTS (
		SELECT *
		FROM [OpenIddict].[OpenIddictApplications]
		WHERE [ClientId] = 'SIAT'
		)
BEGIN
	IF @HashSecretPrd LIKE N'<%'
	BEGIN
		RAISERROR ('Substitua @HashSecretPrd pelo hash do ClientSecret de SIAT antes de executar. SIAT não foi inserido.', 16, 1)
	END
	ELSE
	INSERT [OpenIddict].[OpenIddictApplications] (
		[Id]
		,[ClientId]
		,[ClientSecret]
		,[ConcurrencyToken]
		,[ConsentType]
		,[DisplayName]
		,[DisplayNames]
		,[Permissions]
		,[PostLogoutRedirectUris]
		,[Properties]
		,[RedirectUris]
		,[Requirements]
		,[Type]
		)
	VALUES (
		CONVERT(NVARCHAR(100), NEWID())
		,N'SIAT'
		,@HashSecretPrd
		,CONVERT(NVARCHAR(100), NEWID())
		,NULL
		,N'SIAT'
		,NULL
		,@Permissions
		,N'["' + @UrlBasePrd + N'/signout-callback-oidc"]'
		,NULL
		,N'["' + @UrlBasePrd + N'/signin-oidc"]'
		,@Requirements
		,N'confidential'
		)
END
