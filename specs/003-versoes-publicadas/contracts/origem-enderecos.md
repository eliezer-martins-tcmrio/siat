# Contrato: origem dos endereços e leitura dos arquivos de versão

## Tabela `CSLIB_UserManager.dbo.Apps_VersionAddresses`

Estrutura conferida em `TCMBD3\DEV` em 2026-11-24 (mantida fora do SIAT):

| Coluna | Tipo | Nulo | Uso |
|---|---|---|---|
| `Id` | `int` | não | Identidade da linha |
| `Sistema` | `varchar(100)` | não | Nome exibido |
| `VersaoDSV`, `VersaoHML`, `VersaoPRD` | `varchar(500)` | sim | Endereço da máquina 1 |
| `VersaoDSV2`, `VersaoHML2`, `VersaoPRD2` | `varchar(500)` | sim | Endereço da máquina 2 |
| `SistemaRaizIniDSV`, `SistemaRaizIniHML`, `SistemaRaizIniPRD` | `varchar(500)` | sim | `.INI` do sistema raiz da máquina 1 |
| `SistemaRaizIniDSV2`, `SistemaRaizIniHML2`, `SistemaRaizIniPRD2` | `varchar(500)` | sim | `.INI` do sistema raiz da máquina 2 |
| `Ativo` | `bit` | não | Só `1` é lido |

`Sql/versoes.sql` (recurso embutido, sem concatenação de texto):

```sql
SELECT Id, Sistema,
       VersaoDSV, VersaoHML, VersaoPRD,
       SistemaRaizIniDSV, SistemaRaizIniHML, SistemaRaizIniPRD,
       VersaoDSV2, VersaoHML2, VersaoPRD2,
       SistemaRaizIniDSV2, SistemaRaizIniHML2, SistemaRaizIniPRD2
  FROM dbo.Apps_VersionAddresses
 WHERE Ativo = 1
```

Executado no banco `Servidor.BancoUserManager` do servidor escolhido, pela `FabricaDeConexao`. A ordenação é feita no SIAT (pt-BR), não no SQL.

| Erro SQL | Resultado |
|---|---|
| Conexão recusada, servidor fora do ar | `Erros.Versoes.TabelaIndisponivel` ("Não foi possível conectar a {servidor}") |
| `-2` (tempo esgotado) | `Erros.Versoes.TempoEsgotado` |
| 208 (objeto inexistente), 229/230/916/927 (permissão) | `Erros.Versoes.TabelaIndisponivel` com "tabela inexistente ou sem permissão para a conta do SIAT" |
| Nenhuma linha | `VersoesLidas` com lista vazia (estado "nenhum sistema cadastrado") |

## Formas de endereço

| Forma | Exemplo (DSV, 2026-11-24) | Aceita |
|---|---|---|
| UNC | `\\tcm2016web3\TcmRoot\DSV\Sites\sgq_dsv\VERSAO.TXT` | sim |
| UNC com `{0}` | `\\unity\producao\prod\producao\VBSCP\v.{0}\Assinador\VERSAO.TXT` | sim, com sistema raiz |
| `.INI` | `\\unity\producao\prod\producao\SIRHU\SIRHU.INI` | sim (`VERSAO=`) |
| HTTPS | `https://etcm.tcm.rj.gov.br/versao.txt` | sim |
| Letra de unidade, relativo, `file:`, `\\?\`, outro esquema | — | não: erro "Endereço não suportado" ([research.md R3](../research.md#r3-endereços-aceitos-e-segurança-da-leitura)) |

## Leitura de um endereço (`ILeitorDeArquivoDeVersao`)

Entrada: endereço aceito e `CancellationToken` (com o tempo limite já aplicado). Saída: texto lido (até 64 KB, decodificado com detecção de BOM e UTF-8 como padrão; o `Get-Content` do script lê ANSI sem BOM, mas os valores lidos são só dígitos, pontos e `VERSAO=`) ou erro de célula:

| Situação | Mensagem da célula |
|---|---|
| `FileNotFoundException` | "Arquivo não encontrado." |
| `DirectoryNotFoundException`, rede/compartilhamento inexistente | "Caminho não encontrado." |
| `UnauthorizedAccessException` | "Acesso negado à conta do SIAT." |
| Tempo limite | "Tempo limite de {n} s esgotado ao ler o arquivo." |
| HTTP fora de 2xx | "O servidor respondeu {código}." |
| `HttpRequestException` | "Não foi possível acessar o endereço: {motivo}." |
| Sistema raiz com erro | "Versão do sistema raiz não obtida: {mensagem do raiz}." |
| Outra exceção | "Falha ao ler: {mensagem}." (e `ILogger` Warning) |

A interpretação do texto (12 caracteres, `VERSAO=`, vazio) é do `InterpretadorDeVersao` ([research.md R2](../research.md#r2-interpretação-do-conteúdo-regras-do-script)), não do leitor.
