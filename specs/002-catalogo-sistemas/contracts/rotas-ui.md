# Contrato: rotas e interface do Catálogo

Complementa [specs/001-consultas/contracts/rotas-ui.md](../../001-consultas/contracts/rotas-ui.md). As rotas são relativas ao `<base href>` (`SiatConfiguration:BlazorBaseHref`).

## Rotas

| Rota | Exige | Parâmetros | Comportamento |
|---|---|---|---|
| `/catalogo/sistemas` | autenticação + `Sistemas.CanSelect` (verificado na página e na feature) | `busca` (texto, opcional) | Mostra o catálogo filtrado pela busca |
| `/` | autenticação | — | Leva a `catalogo/sistemas` se houver `Sistemas.CanSelect`; senão à primeira ferramenta de Consultas permitida (MailQueue, AuditLog); sem nenhuma, explica a quem pedir acesso |
| `/acesso-negado` | autenticação | — | Sem mudança de rota; o texto padrão deixa de citar só o grupo Consultas |

- `Navegacao.Sistemas(string? busca)` monta `catalogo/sistemas?busca=...`; busca vazia ou só com espaços fica fora da URL.
- A busca da URL é lida com `[SupplyParameterFromQuery(Name = "busca")]`; texto de qualquer tamanho ou com qualquer caractere vai para o campo sem erro.
- Ao digitar, a URL é atualizada com `NavigateTo(..., replace: true)`.

## Menu

```text
Catálogo            ← novo grupo, antes de Consultas; some se nenhum item for visível
  Sistemas          ← ícone "server"; visível com Sistemas.CanSelect; Match = Prefix
Consultas
  MailQueue
  AuditLog
```

## Tela `Catalogo/Sistemas.razor`

Contrato visual: [design/sistemas.html](../../../design/sistemas.html).

- **Cabeçalho**: título "Sistemas", descrição "Onde cada sistema roda: URL, máquina e caminho por ambiente.", à direita "Catálogo lido às HH:mm" e o botão "Atualizar agora".
- **Barra**: o componente `FiltroLocal` existente, com `Rotulo` "Buscar sistemas" (texto para leitor de tela), `Placeholder` "Sistema, máquina, URL ou caminho" (parâmetro novo e opcional), `Unidade` "sistemas" e `Id` "busca-sistemas"; ele já traz o `data-busca`, o `kbd` "/" e o contador com `aria-live="polite"`: "63 sistemas" ou "5 de 63 sistemas".
- **Teclado**: `/` foca a busca (pelo `siat.js`); `Esc` na busca limpa o texto (pelo `FiltroLocal`) e, com isso, a URL.
- **Carregando**: `EstadoCarregando` com tempo decorrido e "Cancelar", como nas Consultas; com esqueleto da tabela quando ainda não há catálogo e sem esqueleto numa releitura (a tabela anterior continua visível).
- **Tabela**: colunas Sistema, DSV Desenvolvimento, HML Homologação, PRD Produção, CONT Contingência, Observação; cada cabeçalho de ambiente com `data-amb` e a etiqueta `.amb`; cabeçalho fixo e rolagem horizontal (`.tabela-rolagem`).
- **Avisos**: `Aviso` de alerta quando a última releitura falhou ("Não foi possível atualizar o catálogo: {mensagem}. Mostrando os dados lidos às HH:mm.") e quando há itens ignorados ("N itens do sistemasDb.js foram ignorados por estarem incompletos.").
- **Rodapé**: "Fonte: sistemasDb.js no TCMRJDoc (GitLab). Para corrigir um dado, edite esse arquivo." com link para o arquivo (nova aba).

## Componente `CelulaAmbiente`

Parâmetros: `Sistema` (nome, para os rótulos), `Ambiente` (`AmbienteDoCatalogo`), `Instancias` (lista de `InstanciaVM`).

| Situação | Saída |
|---|---|
| Lista vazia | `<span class="vazio-cel">–</span>` com texto para leitor de tela "não configurado" |
| Endereço com `EhLink` | `<a href target="_blank" rel="noopener noreferrer">` com `Exibicao` quebrável e "(abre em nova aba)" só para leitor de tela |
| Endereço sem `EhLink` | Texto, sem link |
| Máquina | Valor em fonte monoespaçada + `BotaoCopiar` com `Rotulo` "máquina {M} de {SIGLA} do {Sistema}" |
| Caminho | Valor monoespaçado quebrável + `BotaoCopiar` com `Rotulo` "caminho de {SIGLA} do {Sistema}" |
| Várias instâncias | Uma `div.instancia` por instância, separadas pela linha tracejada do `siat.css` |

O texto vem sempre por renderização normal do Razor (codificado); nenhum `MarkupString` com dado da origem. A quebra após `/ \ _ . -` é feita intercalando `<wbr />` entre segmentos.
