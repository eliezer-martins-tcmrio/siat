# Modelo de dados: Ficha do Sistema

**Feature**: [spec.md](./spec.md) | **Plano**: [plan.md](./plan.md) | **Pesquisa**: [research.md](./research.md)

Nada é persistido pelo SIAT (Princípio I). As entidades abaixo existem em memória, durante a leitura e nos caches que já existem (catálogo, versões) ou no novo cache das versões da ficha. As entidades da 002 (`Sistema`, `Instancia`, `CatalogoLido`) e da 003 (`EnderecoDeVersao`, `LeituraDeCelula`, `SistemaVersionado`, `VersoesLidas`) continuam como estão, salvo o que está marcado aqui.

## Enums

| Enum | Valores | Observação |
|---|---|---|
| `FiltroDeSituacao` (App, existente) | `Todos`, `ComDivergencia`, `ComErro`, **`SemFicha`** | Na URL: ausente, `div`, `erro`, **`semficha`** |

## NomeDeSistema (Domain, novo, estático)

| Membro | Regra |
|---|---|
| `Normalizar(string)` | Sem espaços nas pontas, sem acentos (`FormD` sem marcas não espaçadas, depois `FormC`) e em minúsculas (pt-BR) ([R2](./research.md#r2-comparação-de-nomes)). `SistemaVersoesVM.Normalizar` passa a chamá-lo |

## VinculoDeVersoes (Domain, novo)

O campo `Versoes` de um item ([contracts/campo-versoes.md](./contracts/campo-versoes.md)).

| Campo | Tipo | Regra |
|---|---|---|
| `Nomes` | `IReadOnlyList<string>` | Como escritos no catálogo, sem espaços nas pontas, sem vazios, sem repetidos (pela forma normalizada, fica a primeira grafia), na ordem do campo |
| `Invalido` | `bool` | O campo existe e não é lista de textos; então `Nomes` é vazio |
| `Vazio` | `bool` (calculado) | `Nomes.Count == 0` |
| `Chave` | `string` (calculado) | Nomes normalizados, na ordem, unidos por `\n`; parte da `ChaveDaFicha` |
| `Contem(string nome)` | `bool` | Algum nome normalizado é igual a `NomeDeSistema.Normalizar(nome)` |

Valores prontos: `VinculoDeVersoes.Nenhum` (campo ausente ou `null`) e `VinculoDeVersoes.ForaDoFormato` (com `Invalido = true`).

## Sistema (Domain, existente, alterado)

| Campo | Tipo | Regra |
|---|---|---|
| **`Versoes`** | `VinculoDeVersoes` | Lido do campo `Versoes`; `Nenhum` quando ausente |
| **`Posicao`** | `int` | Posição do item na lista do `sistemasDb.js`, a partir de 1 (a mesma do log de itens ignorados); desempata itens com o mesmo nome ([R4](./research.md#r4-localizar-o-item-e-montar-o-mapa-de-fichas)) |

O resto (`Nome`, `Observacao`, `Ambientes`) não muda. `SistemaVM`, a busca do Catálogo e o `TextoDeBusca` ignoram `Versoes` (FR-011).

## CatalogoLido (Domain, existente, novos métodos)

| Método | Regra |
|---|---|
| `Localizar(string nome)` → `ItemLocalizado?` | Itens com `NomeDeSistema.Normalizar(Nome)` igual ao do pedido, por `Posicao`; nulo quando não há nenhum. `ItemLocalizado(Sistema Item, int Repetidos)`: o primeiro e a quantidade dos demais (FR-012, FR-020) |
| `FichasPorVersao()` → `IReadOnlyDictionary<string, string>` | Nome normalizado da tabela → `Nome` do item. Percorre `Sistemas` (ordem alfabética pt-BR); só itens que são o primeiro do seu nome (`Localizar(item.Nome).Item == item`) e com vínculo válido; para cada nome do vínculo, fica o primeiro item (`TryAdd`) |

## VersoesDoSistema (Domain, novo, puro)

As versões de um item num servidor, a partir de um `VersoesLidas` completo (tela de Versões) ou parcial (leitura da ficha) ([R5](./research.md#r5-versões-da-ficha-cache-e-leitura-só-do-sistema)).

| Campo | Tipo | Regra |
|---|---|---|
| `Componentes` | `IReadOnlyList<ComponenteDaFicha>` | Um por nome do vínculo **com** registro, na ordem do vínculo |
| `NaoEncontrados` | `IReadOnlyList<string>` | Nomes do vínculo sem registro ativo no servidor, na ordem do vínculo (FR-017) |
| `TemDivergencia` | `bool` | Algum `SistemaVersionado` dos componentes tem divergência |
| `TemErro` | `bool` | Algum tem célula com erro; nomes não encontrados não contam (FR-017) |
| `SemPendencias` | `bool` | Nem divergência nem erro |

`ComponenteDaFicha`: `Nome` (como escrito no vínculo) e `Registros` (`IReadOnlyList<SistemaVersionado>`, por `Id`; mais de um quando o nome se repete na tabela).

Construção: `VersoesDoSistema.De(VersoesLidas versoes, VinculoDeVersoes vinculo)` agrupa `versoes.Sistemas` por `NomeDeSistema.Normalizar(Nome)` e procura cada nome do vínculo.

## ChaveDaFicha (App, novo)

`readonly record struct ChaveDaFicha(string Servidor, string Vinculo)`: chave do servidor e `VinculoDeVersoes.Chave`. Chave do cache das versões da ficha e do andamento dela (texto `"{Servidor}\n{Vinculo}"` no dicionário de andamento).

O FR-027 pede o cache "separado por servidor e por sistema"; a chave pelo vínculo é equivalente para a ficha: o vínculo é o que determina os registros e arquivos lidos para um sistema, dois itens com o mesmo vínculo leriam exatamente os mesmos registros, e mudar o vínculo de um item no catálogo muda a chave (a ficha lê de novo).

## Ciclo das versões da ficha

```text
pedido (servidor, vínculo, atualizar)
 ├─ atualizar = false
 │   ├─ cache da tela de Versões válido para o servidor  ┐
 │   └─ cache da ficha válido para (servidor, vínculo)    ┘→ o mais recente (GeradoEm) → fim, sem leitura
 └─ nenhum válido, ou atualizar = true
     → LeituraEmCache da ficha: leitura única por chave, cancelamento por contagem, anterior preservado na falha
         lê endereços ativos do servidor → filtra pelo vínculo → LeitorDeVersoes (raiz, paralelo, 5 s) → VersoesLidas parcial
→ VersoesDoSistema.De(resultado, vínculo)
```

"Válido" é o novo `LeituraEmCache.Valido(chave)`: valor na validade (`Versoes:MinutosDeCache`) e sem falha na última releitura. O resultado parcial nunca entra no cache da tela de Versões (FR-027).

## View models (App)

| VM | Campos |
|---|---|
| `FichaVM` (novo) | `Nome`, `Observacao`, `Ambientes` (`AmbienteVM` da 002, quatro, na ordem DSV, HML, PRD, CONT), `Repetidos` (`int`), `Vinculo` (`IReadOnlyList<string>`; vazio sem `Versoes_Sistemas.CanSelect`), `VinculoInvalido` (`bool`; falso sem o recurso), `MostraVersoes` (`bool`: o usuário tem `Versoes_Sistemas.CanSelect`), `LidoEm`, `Origem` (link do `sistemasDb.js`), `Falha` (`FalhaVM?`) |
| `VersoesDaFichaVM` (novo) | `Servidor` (`ServidorVM`), `GeradoEm`, `Duracao`, `Falha` (`FalhaDasVersoesVM?`), `Componentes` (`ComponenteVM`), `NaoEncontrados` (`IReadOnlyList<string>`), `TemDivergencia`, `TemErro`, `SemPendencias` |
| `ComponenteVM` (novo) | `Nome`, `Registros` (`IReadOnlyList<SistemaVersoesVM>` da 003, com as seis `CelulaVersaoVM` e a divergência já calculada) |
| `FichasVM` (novo) | `PorNome` (`IReadOnlyDictionary<string, string>`: nome normalizado → nome do item); `Ficha(SistemaVersoesVM s)` → nome do item ou nulo |

A página separa as células por ambiente (`CelulaVersaoVM.Ambiente`) para montar a tabela de cada seção.

## Validações

| Regra | Onde |
|---|---|
| Campo `Versoes` é lista de textos | `MontadorCatalogo` → `VinculoDeVersoes.Invalido` e log Warning |
| Nome da ficha existe no catálogo | `ObterFicha` → `Erros.Catalogo.SistemaNaoEncontrado(nome)` (novo, código `catalogo.sistema-nao-encontrado`) |
| Servidor na lista fechada | `AutorizacaoDasVersoes.AutorizarAsync` (003), reaproveitada por `ObterVersoesDaFicha` |
| Vínculo vazio em `ObterVersoesDaFicha` | Devolve `VersoesDaFichaVM` sem componentes nem nomes não encontrados, sem chamar o serviço (`GeradoEm` = momento do pedido, `Duracao` zero, sem `Falha`); a página não chama a feature nesse caso (FR-018) |
| Recursos | `ObterFicha` e `ListarFichas`: `Catalogo_Sistemas.CanSelect`; `ObterVersoesDaFicha`: os dois recursos ([R7](./research.md#r7-autorização)) |
