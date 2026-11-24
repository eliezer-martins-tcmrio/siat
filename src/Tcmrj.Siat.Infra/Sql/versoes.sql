-- Endereços de versão ativos (specs/003-versoes-publicadas/contracts/origem-enderecos.md).
-- Executado no banco Servidor.BancoUserManager do servidor escolhido. A ordenação (pt-BR) é feita no SIAT.
SELECT Id, Sistema,
       VersaoDSV, VersaoHML, VersaoPRD,
       SistemaRaizIniDSV, SistemaRaizIniHML, SistemaRaizIniPRD,
       VersaoDSV2, VersaoHML2, VersaoPRD2,
       SistemaRaizIniDSV2, SistemaRaizIniHML2, SistemaRaizIniPRD2
  FROM dbo.Apps_VersionAddresses
 WHERE Ativo = 1
