# Modelo de dados: Versões Publicadas

**Feature**: [spec.md](./spec.md) | **Plano**: [plan.md](./plan.md) | **Pesquisa**: [research.md](./research.md)

Nada é persistido pelo SIAT (Princípio I). As entidades abaixo existem em memória, durante a leitura e no cache.

## Enums

| Enum | Valores | Observação |
|---|---|---|
| `Ambiente` (existente) | `Dsv`, `Hml`, `Prd` | Sigla e cor já definidas na 001 |
| `Maquina` (novo) | `Maquina1`, `Maquina2` | Rótulos "Máquina 1" e "Máquina 2"; colunas sem sufixo e com sufixo `2` da tabela |
| `SituacaoDaCelula` (novo) | `Versao`, `Erro`, `NaoConfigurado` | "Divergente" não é situação da célula: é calculada no sistema (abaixo) |
| `FiltroDeSituacao` (novo, App) | `Todos`, `ComDivergencia`, `ComErro` | Na URL: ausente, `div`, `erro` |

## EnderecoDeVersao (Domain, origem)

Uma linha ativa de `Apps_VersionAddresses` ([contracts/origem-enderecos.md](./contracts/origem-enderecos.md)).

| Campo | Tipo | Regra |
|---|---|---|
| `Id` | `int` | Identifica a linha (nomes podem se repetir) |
| `Sistema` | `string` | Sem espaços nas pontas; obrigatório na tabela (`NOT NULL`) |
| `Enderecos` | `IReadOnlyDictionary<(Ambiente, Maquina), ParDeEndereco>` | Seis entradas, sempre presentes |

`ParDeEndereco`: `Versao` (`string?`) e `SistemaRaiz` (`string?`); `NULL` ou só espaços vira `null`. `Versao` nulo = célula "não configurado", qualquer que seja o `SistemaRaiz`.

## LeituraDeCelula (Domain)

Resultado de uma máquina num ambiente.

| Campo | Tipo | Regra |
|---|---|---|
| `Ambiente`, `Maquina` | enums | Posição da célula |
| `Situacao` | `SituacaoDaCelula` | |
| `Versao` | `string?` | Só com `Situacao = Versao`; já interpretada (R2), sem espaços nas pontas |
| `Caminho` | `string?` | Endereço efetivamente consultado, com `{0}` já trocado; nulo só em `NaoConfigurado` sem endereço |
| `CaminhoDoRaiz` | `string?` | Endereço do sistema raiz, quando houver |
| `Mensagem` | `string?` | Só com `Situacao = Erro`; em português (R8) |

Transições de uma célula durante a leitura: `pendente → lendo raiz → lendo → (Versao | Erro | NaoConfigurado)`; sem raiz, começa em `lendo`; sem endereço, já nasce `NaoConfigurado`. Conteúdo vazio ou sem `VERSAO=` termina em `NaoConfigurado` com `Caminho` preenchido (esclarecimento de 2026-11-24).

## SistemaVersionado (Domain)

| Campo | Tipo | Regra |
|---|---|---|
| `Id`, `Nome` | | Da linha de origem |
| `Celulas` | 6 × `LeituraDeCelula` | Ordem DSV-1, DSV-2, HML-1, HML-2, PRD-1, PRD-2 |
| `DivergeEm(Ambiente)` | `bool` | As duas células do ambiente com `Situacao = Versao` e versões diferentes (ordinal) |
| `TemDivergencia` | `bool` | Algum ambiente diverge |
| `TemErro` | `bool` | Alguma célula com `Situacao = Erro` |
| `SemPendencias` | `bool` | `!TemDivergencia && !TemErro` (inclui o sistema com tudo "não configurado") |

Um sistema pode ter divergência e erro ao mesmo tempo e conta nas duas situações.

## VersoesLidas (Domain, valor do cache)

| Campo | Tipo | Regra |
|---|---|---|
| `Servidor` | `Servidor` | De onde vieram os endereços |
| `GeradoEm` | `DateTimeOffset` | Fim da leitura; base da validade de 5 min |
| `Duracao` | `TimeSpan` | Da consulta à tabela até a última célula |
| `Sistemas` | `IReadOnlyList<SistemaVersionado>` | Ordem pt-BR pelo nome, depois `Id` (R8) |
| `QuantidadeDeEnderecos` | `int` | Endereços distintos lidos (para "Lendo N arquivos") |

## Estado do cache (App, `LeituraEmCache<string, VersoesLidas>`)

Por chave de servidor, o mesmo ciclo do catálogo (002):

| Estado | Entra quando | Pedido sem "Atualizar" | Pedido com "Atualizar" |
|---|---|---|---|
| Vazio | início, ou só houve cancelamento | inicia leitura | inicia leitura |
| Válido | leitura concluída há menos de 5 min | devolve o resultado | inicia leitura |
| Vencido | passou a validade | inicia leitura | inicia leitura |
| Lendo | leitura em andamento | espera a mesma leitura | espera a mesma leitura |
| Válido com falha | releitura falhou e havia resultado | inicia leitura; enquanto isso, a tela mostra o anterior com aviso | inicia leitura |

Cancelamento: cada pedido entra na contagem de quem espera; quando sai o último, a leitura é cancelada (SQL, HTTP, leitura dos arquivos já abertos e espera das aberturas em curso, que são abandonadas) e o estado volta ao anterior.

Andamento ([research.md R10](./research.md#r10-andamento-da-leitura)): enquanto um servidor está no estado **Lendo**, o serviço guarda a quantidade de endereços distintos daquela leitura, conhecida depois da consulta à tabela; antes disso, e fora do estado **Lendo**, a quantidade é nula.

## View models (App)

- `VersoesVM`: `Servidor` (`ServidorVM`), `GeradoEm`, `Duracao`, `Total`, `SemPendencias`, `ComDivergencia`, `ComErro`, `Sistemas`, `Falha` (mensagem e horário, opcional).
- `SistemaVersoesVM`: `Id`, `Nome`, `NomeNormalizado` (sem acento, minúsculo, para a busca), `TemDivergencia`, `TemErro`, `Celulas`.
- `CelulaVersaoVM`: `Ambiente`, `Maquina`, `Situacao`, `Divergente`, `Versao`, `Caminho`, `CaminhoDoRaiz`, `Mensagem`, `RotuloAcessivel` (por exemplo, "PRD, máquina 2: 7.23.9, difere da outra máquina").

## Erros (`Erros.Versoes`)

| Erro | Quando |
|---|---|
| `AcessoNegado` | Sem `Versoes_Sistemas.CanSelect` |
| `ServidorNaoEncontrado` | Reaproveita `Erros.Consultas.ServidorNaoEncontrado` |
| `TabelaIndisponivel(servidor, motivo)` | Falha de conexão, permissão ou tabela inexistente |
| `TempoEsgotado(servidor)` | `CommandTimeout` na consulta à tabela |
| `SemSistemasAtivos(servidor)` | Tabela sem registro ativo: estado vazio, não erro de leitura |
| `Cancelada` | Cancelamento pelo usuário |
| De célula (só mensagem, não `Erro` da tela) | Arquivo/caminho não encontrado, acesso negado, tempo esgotado, HTTP com código de erro, endereço não suportado, raiz ilegível, falha inesperada |
