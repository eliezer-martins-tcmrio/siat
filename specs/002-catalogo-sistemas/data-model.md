# Modelo de dados: Catálogo de Sistemas

**Feature**: [spec.md](./spec.md) | **Pesquisa**: [research.md](./research.md)

Nada é persistido pelo SIAT. As entidades abaixo existem só em memória, montadas a partir do `sistemasDb.js` ([contracts/origem-sistemasdb.md](./contracts/origem-sistemasdb.md)).

## Domínio (`Tcmrj.Siat.Domain`)

### `AmbienteDoCatalogo` (enum)

| Valor | Chave no arquivo | Sigla | Nome | Token de cor (`data-amb`) |
|---|---|---|---|---|
| `Dsv` | `DSV` | DSV | Desenvolvimento | `dsv` |
| `Hml` | `HML` | HML | Homologação | `hml` |
| `Prd` | `PRD` | PRD | Produção | `prd` |
| `Cont` | `CONT` | CONT | Contingência | `cont` |

A ordem do enum é a ordem das colunas (FR-005).

### `Instancia`

Um lugar onde o sistema roda num ambiente.

| Campo | Tipo | Origem | Regra |
|---|---|---|---|
| `Enderecos` | lista de texto | `URL` dividido por `\|` | Partes sem espaços nas pontas; vazias descartadas |
| `Maquinas` | lista de texto | `Machine` dividido por `,` | Idem |
| `Caminho` | texto ou nulo | `Path` | Sem espaços nas pontas; vazio vira nulo |

- **Invariante**: pelo menos um dos três preenchido. Instância toda vazia não é criada (R3).

### `Sistema`

| Campo | Tipo | Origem | Regra |
|---|---|---|---|
| `Nome` | texto | `Title` | Obrigatório; sem espaços nas pontas |
| `Observacao` | texto ou nulo | `OBS` | Sem espaços nas pontas; vazio vira nulo; quebras de linha preservadas |
| `Ambientes` | mapa `AmbienteDoCatalogo` → lista de `Instancia` | `DSV`, `HML`, `PRD`, `CONT` | Sempre com as quatro chaves; lista vazia quando o ambiente falta ou só tinha instâncias vazias |

### `CatalogoLido`

Resultado de uma leitura bem-sucedida da origem.

| Campo | Tipo | Regra |
|---|---|---|
| `Sistemas` | lista de `Sistema` | Ordenada por `Nome` na cultura pt-BR |
| `Ignorados` | inteiro ≥ 0 | Itens sem `Title` válido ou com estrutura inválida (FR-024) |
| `LidoEm` | data e hora (UTC) | Momento da leitura, do `TimeProvider` |
| `Origem` | texto | URL do arquivo na interface do GitLab, para o rodapé (FR-022) |

### `Erros.Catalogo`

Códigos e mensagens em [research.md R6](./research.md#r6-erros-e-mensagens): `NaoConfigurado`, `AcessoRecusado` (401), `ArquivoNaoEncontrado` (403/404), `TempoEsgotado`, `Indisponivel` (falha de rede ou 5xx), `FormatoInvalido(motivo, linha, coluna)`, `Cancelada` ("A leitura do catálogo foi cancelada.", devolvido pelo repositório e pelo serviço quando o chamador cancela; não é falha da origem) e `AcessoNegado` ("Você não tem acesso ao catálogo de sistemas."), todos próprios do catálogo.

### `Modulo` (enum existente)

Recebe `Sistemas`. `Politica()` → `Sistemas.CanSelect` (R5).

## App (`Tcmrj.Siat.App`)

### Estado do `CatalogoSistemasService` (singleton)

| Campo | Significado |
|---|---|
| `ultimaValida` | `CatalogoLido` mais recente que deu certo, ou nulo |
| `ultimaFalha` | `Erro` e horário da última tentativa que falhou depois de `ultimaValida`, ou nulo |
| `leituraEmAndamento` | `Task` compartilhado da leitura atual, ou nulo |
| `cancelamentoDaLeitura` | `CancellationTokenSource` próprio da leitura atual, cancelado quando ninguém mais espera |
| `esperando` | quantos pedidos esperam pela leitura atual |

Transições:

```text
                  pedido (cache vazio ou vencido, ou Atualizar)
   [sem leitura] ───────────────────────────────────────────► [lendo]
        ▲                                                         │
        │ falha (sem anterior): devolve Erro                       │ sucesso: ultimaValida = nova; ultimaFalha = nulo
        └─────────────────────────────────────────────────────────┤
                                                                   │ falha (com anterior): ultimaFalha = erro; devolve anterior + falha
   [válida] ◄──────────────────────────────────────────────────────┘
      │ pedido dentro da validade (agora - LidoEm < MinutosDeCache) e sem Atualizar: devolve ultimaValida (+ ultimaFalha, se houver)
      └─ pedido vencido ou Atualizar ──► [lendo]  (pedidos simultâneos aguardam a mesma leitura)
```

- **Cancelamento**: o pedido cancelado deixa de esperar e sai de `esperando`; com `esperando = 0`, a leitura é cancelada (a requisição ao GitLab é interrompida) e o estado volta ao que era antes dela ([sem leitura] ou [válida]), sem registrar falha.
- A validade conta a partir de `LidoEm` da última leitura válida. Depois de uma falha com leitura anterior, o próximo pedido tenta de novo (não espera nova validade), para que a correção do problema apareça logo.

### View models (`ViewModels/Sistemas`)

- **`CatalogoVM`**: `Sistemas` (lista de `SistemaVM`), `Total`, `Ignorados`, `LidoEm`, `Origem`, `Falha` (mensagem e horário quando a última releitura falhou e a tela mostra a leitura anterior; nulo caso contrário).
- **`SistemaVM`**: `Nome`, `Observacao`, `Ambientes` (as quatro entradas na ordem do enum, cada uma com sigla, nome, `data-amb` e lista de `InstanciaVM`), `TextoDeBusca` (nome, observação, endereços completos, máquinas e caminhos de todas as instâncias, separados por espaço; montado uma vez).
- **`InstanciaVM`**: `Enderecos` (lista de `EnderecoVM`), `Maquinas` (lista de texto), `Caminho`.
- **`EnderecoVM`**: `Texto` (como veio), `Exibicao` (sem `http://`/`https://`, só quando é link), `EhLink` (começa por `http://` ou `https://`, sem diferenciar maiúsculas).

## Tela (`Sistemas.razor`)

| Estado | Quando | O que mostra |
|---|---|---|
| Carregando | Leitura em andamento | `EstadoCarregando` com tempo decorrido e "Cancelar" (com esqueleto da tabela se não houver catálogo; sem esqueleto, acima da tabela, numa releitura). Cancelar volta ao catálogo anterior ou, sem ele, ao erro com "Tentar de novo" |
| Erro | Leitura falhou sem anterior | `EstadoErro` com a mensagem de R6 e "Tentar de novo" (força releitura) |
| Com catálogo | Leitura válida | Tabela, contador, horário, "Atualizar agora"; `Aviso` se `Ignorados > 0` ou `Falha` não nula |
| Vazio da busca | Com catálogo e nenhum sistema casa a busca | `EstadoVazio` com "Limpar busca" |
| Acesso negado | Sem `Sistemas.CanSelect` | `AcessoNegado` com `Recurso="Sistemas.CanSelect"` |

A busca (`busca` na URL) é estado da tela, não do serviço; o filtro roda sobre `SistemaVM.TextoDeBusca` com `FiltroLocal`.
