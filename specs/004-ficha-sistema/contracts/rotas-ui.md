# Contrato: rotas e interface da Ficha do Sistema

Complementa [specs/002-catalogo-sistemas/contracts/rotas-ui.md](../../002-catalogo-sistemas/contracts/rotas-ui.md) e [specs/003-versoes-publicadas/contracts/rotas-ui.md](../../003-versoes-publicadas/contracts/rotas-ui.md). As rotas são relativas ao `<base href>` (`SiatConfiguration:BlazorBaseHref`).

## Rotas

| Rota | Exige | Parâmetros | Comportamento |
|---|---|---|---|
| `/sistemas/catalogo/ficha` | autenticação + `Catalogo_Sistemas.CanSelect` (página e feature); a parte de versões exige também `Versoes_Sistemas.CanSelect` | `sistema` (nome do item, obrigatório), `servidor` (chave, opcional; só lido com `Versoes_Sistemas.CanSelect`) | Mostra a ficha do item; ver [Tela](#tela-sistemasficharazor) |
| `/sistemas/catalogo` | como na 002 | `busca` | Igual, com o nome do sistema como link para a ficha |
| `/sistemas/versoes` | como na 003 | `servidor`, `busca`, `filtro` (`div`, `erro` ou **`semficha`**) | Igual, com o nome como link para a ficha e a opção **Sem ficha** para quem tem `Catalogo_Sistemas.CanSelect` |

- `Navegacao.Ficha(string sistema, string? servidor = null)` monta `sistemas/catalogo/ficha?sistema=...&servidor=...`, com `Uri.EscapeDataString`; servidor nulo (o padrão) fica fora da URL ([research.md R3](../research.md#r3-endereço-da-ficha)).
- `sistema` ausente ou só com espaços: a ficha mostra o estado de "sistema não encontrado" com o link para o Catálogo.
- `servidor` desconhecido ou fora da lista, com `Versoes_Sistemas.CanSelect`: a ficha usa o padrão e mostra o `Aviso` "O servidor {chave} não está na lista; mostrando {padrão}." Sem `Versoes_Sistemas.CanSelect`, o parâmetro é ignorado, sem aviso.
- Trocar o servidor na ficha atualiza a URL com `NavigateTo(..., replace: true)`, como na tela de Versões.
- `Navegacao.LerFiltro("semficha")` → `FiltroDeSituacao.SemFicha`; `Navegacao.Versoes(..., FiltroDeSituacao.SemFicha)` grava `filtro=semficha`.

## Menu

Sem item novo. A ficha fica embaixo de `sistemas/catalogo`, e o item **Catálogo** (`NavLinkMatch.Prefix`) aparece como o atual nela (FR-003).

## Tela `Sistemas/Ficha.razor`

Contrato visual: [design/ficha.html](../../../design/ficha.html).

| Área | Conteúdo |
|---|---|
| Trilha | `nav aria-label="Trilha de navegação"` com `ol`: Sistemas › [Catálogo](sistemas/catalogo) › {nome} (`aria-current="page"`); novo parâmetro opcional `Trilha` do `CabecalhoPagina` |
| Cabeçalho | `CabecalhoPagina` com o nome do sistema (título da aba "{nome} · SIAT"), descrição "Onde o sistema roda e o que está publicado em cada ambiente."; com versões, pílulas da situação ("Sem pendências", "Com divergência", "Com erro"; as duas últimas podem aparecer juntas); status "Catálogo lido às HH:mm · Versões geradas às HH:mm em N,N s"; "Atualizar agora" |
| Observação | O `OBS` do item, como texto, com quebras de linha |
| Avisos | Item repetido: "Há {n} outro(s) item(ns) com o nome {nome} no catálogo; esta ficha mostra o primeiro. Corrija no sistemasDb.js." · Vínculo inválido ([campo-versoes.md](./campo-versoes.md)) · Nomes sem registro: título "Nome vinculado não encontrado" e "{nome} não está entre os registros ativos de {servidor}. Confira o campo Versoes do {sistema} no sistemasDb.js." (um por nome) · Falha de releitura do catálogo ou das versões, com o motivo e o horário dos dados exibidos |
| Contexto | Só com `Versoes_Sistemas.CanSelect` e vínculo: `SeletorServidor` "Servidor dos endereços" (`ListarPermitidos(Versoes_Sistemas)`) e `FaixaContexto` com o servidor. Sem `servidor` válido na URL e com o servidor padrão fora da lista do usuário: no bloco de versões, `EstadoVazio` "Escolha o servidor dos endereços" com "O servidor padrão das versões não está entre os servidores que você pode consultar. Escolha um na lista acima.", sem leitura, como na tela de Versões |
| Sem vínculo | Só com `Versoes_Sistemas.CanSelect`: `Aviso` informativo "Este sistema não tem versões vinculadas. Para ver as versões aqui, liste os nomes de Apps_VersionAddresses no campo Versoes do item, no sistemasDb.js." com link para o arquivo no GitLab |
| Seções por ambiente | Uma `section` por ambiente, na ordem DSV, HML, PRD, Contingência, com faixa na cor do ambiente, etiqueta da sigla e `h2` com o nome (`data-amb`, "DSV" + "Desenvolvimento"). Dentro: "No catálogo" e a `ListaDeInstancias` (ou "não configurado"); depois, nos três primeiros e com versões, a tabela "Versões em {AMB}" |
| Tabela de versões do ambiente | Colunas Componente, Máquina 1, Máquina 2; uma linha por registro encontrado (nome vinculado, na ordem do campo; registros repetidos na tabela em linhas separadas, por `Id`) com duas `CelulaVersao`; logo abaixo, linha com "Máquina {n}", o caminho consultado em fonte monoespaçada com `BotaoCopiar` ("Copiar caminho de {AMB}, máquina {n}, do {componente}"), o caminho do sistema raiz quando houver e a mensagem de erro. Sem nenhum caminho no ambiente: "Nenhum endereço de versão configurado em {AMB}." |
| Contingência | Só `ListaDeInstancias` e a nota "Não há endereços de versão para Contingência." (com versões) |
| Carregando | Catálogo: `EstadoCarregando` "Lendo o catálogo no GitLab…" no lugar das seções, com tempo decorrido e "Cancelar" depois de 1 s, como na tela de Catálogo (Princípio VII); Cancelar volta à ficha anterior ou, sem ela, ao `EstadoErro` com "Tentar de novo", e interrompe a leitura no GitLab quando ninguém mais espera por ela. Versões: `EstadoCarregando` "Lendo endereços em {servidor}" e depois "Lendo {N} arquivos de versão" no bloco de versões, com tempo decorrido e "Cancelar"; as instâncias continuam visíveis |
| Erro | Catálogo sem leitura anterior: `EstadoErro` com o motivo e "Tentar de novo", como na tela de Catálogo. Versões sem resultado anterior: `EstadoErro` "Não foi possível ler as versões em {servidor}" no bloco de versões, com "Tentar de novo". Recurso perdido durante a sessão (por exemplo, em "Atualizar agora"): `Erros.Catalogo.AcessoNegado` em `ObterFicha` ou `ObterVersoesDaFicha` → a tela de acesso negado; `Erros.Versoes.AcessoNegado` em `ObterVersoesDaFicha` → o bloco de versões descarta o resultado anterior e mostra o `EstadoErro` com a mensagem, sem "Tentar de novo", e o catálogo continua |
| Não encontrado | `EstadoVazio` "Sistema não encontrado no catálogo", "Não há item com o nome {nome} no catálogo. Ele pode ter sido renomeado ou removido." e a ação "Procurar no Catálogo" → `Navegacao.Catalogo(nome)` |
| Legenda e rodapé | As quatro situações de célula (com versões). "Catálogo: {link sistemasDb.js} · cache de {n} minutos. Endereços: {servidor} · CSLIB_UserManager.dbo.Apps_VersionAddresses (registros ativos) · cache de {n} minutos." |

## Componentes

| Componente | Mudança |
|---|---|
| `ListaDeInstancias` (novo) | O conteúdo atual de `CelulaAmbiente` (instâncias com URL, máquina e caminho, links em nova aba, `BotaoCopiar`, "não configurado"), sem o `td`. Parâmetros: `Sistema`, `Ambiente` |
| `CelulaAmbiente` | Passa a ser `<td><ListaDeInstancias .../></td>`; o HTML gerado não muda (os testes atuais continuam valendo) |
| `CabecalhoPagina` | Parâmetro opcional `Trilha` (`RenderFragment`), desenhado antes do `h1` |
| `CelulaVersao` | Sem mudança; usada também na tabela de versões da ficha |

## Mudanças na tela de Catálogo

| Área | Mudança |
|---|---|
| Coluna Sistema | `<a href="{Navegacao.Ficha(nome)}"><span class="so-leitor">Ficha do </span>{nome}</a>` (FR-004) |

## Mudanças na tela de Versões

| Área | Mudança |
|---|---|
| Fichas | Com `Catalogo_Sistemas.CanSelect`, `ListarFichas` é chamada junto com `ObterVersoes`; a tabela não espera por ela (FR-031) |
| Coluna Sistema | Com o mapa e o nome nele: link para `Navegacao.Ficha(nomeDoItem, servidorDaUrl)`, texto visível igual ao nome da tabela, prefixo "Ficha do " só para leitor de tela; o clique não abre a linha. Sem o nome no mapa, ou sem o mapa: texto, como hoje |
| Filtro | Opção **Sem ficha** com a quantidade, depois de "Com erro", só com o mapa; seleção: nome normalizado fora do mapa. Estado vazio: "Não há nenhum sistema sem ficha nesta leitura." / "... com \"{busca}\" no nome." |
| Aviso | Falha de `ListarFichas`: `Aviso` "As fichas dos sistemas estão indisponíveis: {motivo}." Os nomes ficam sem link e **Sem ficha** não aparece |
| URL | `filtro=semficha` guardado até o mapa chegar; sem o recurso ou com falha do catálogo, a tela fica em "Todos" (sem aviso) |
