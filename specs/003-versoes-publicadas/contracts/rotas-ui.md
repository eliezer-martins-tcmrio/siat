# Contrato: rotas e interface de Versões

Complementa [specs/001-consultas/contracts/rotas-ui.md](../../001-consultas/contracts/rotas-ui.md) e [specs/002-catalogo-sistemas/contracts/rotas-ui.md](../../002-catalogo-sistemas/contracts/rotas-ui.md). As rotas são relativas ao `<base href>` (`SiatConfiguration:BlazorBaseHref`).

## Rotas

| Rota | Exige | Parâmetros | Comportamento |
|---|---|---|---|
| `/sistemas/versoes` | autenticação + `Versoes_Sistemas.CanSelect` (verificado na página e na feature) | `servidor` (chave, opcional), `busca` (texto, opcional), `filtro` (`div` ou `erro`, opcional) | Mostra as versões dos endereços do servidor, filtradas |
| `/` | autenticação | — | Primeira ferramenta permitida na ordem do menu: Catálogo, Versões, MailQueue, AuditLog; sem nenhuma, explica a quem pedir acesso (grupo **Sistemas**: catálogo e versões; grupo **Consultas**) |

- `Navegacao.Versoes(string? servidor, string? busca, FiltroDeSituacao filtro)` monta `sistemas/versoes?servidor=...&busca=...&filtro=...`; o servidor padrão, a busca vazia e o filtro `Todos` ficam fora da URL.
- `servidor` desconhecido: a tela usa o padrão e mostra `Aviso` "O servidor {chave} não está na lista; mostrando {padrão}". `filtro` desconhecido: `Todos`, sem aviso.
- Ao trocar servidor, busca ou filtro, a URL é atualizada com `NavigateTo(..., replace: true)`. Trocar o servidor inicia a obtenção para o novo servidor (do cache, se válido).

## Menu

```text
Sistemas
  Catálogo          ← existente
  Versões           ← ícone "git-compare" (Lucide); visível com Versoes_Sistemas.CanSelect; Match = Prefix
Consultas
  MailQueue
  AuditLog
```

## Tela `Sistemas/Versoes.razor`

Contrato visual: [design/versoes.html](../../../design/versoes.html).

| Área | Conteúdo |
|---|---|
| Cabeçalho | `CabecalhoPagina` "Versões publicadas", descrição "Versão de cada sistema em DSV, HML e PRD, nas duas máquinas de cada ambiente."; status "Gerado às HH:mm em N,N s"; botões "Atualizar agora" e, durante a leitura, "Cancelar" |
| Contexto | `SeletorServidor` (servidores de `ListarPermitidos(Versoes_Sistemas)`) e `FaixaContexto` com o servidor dos endereços |
| Aviso de falha | Com resultado anterior e releitura falha: `Aviso` com o motivo e "Dados gerados às HH:mm" |
| Resumo | `dl`: Sistemas, Sem pendências, Com divergência, Com erro (marcador de cor + texto) |
| Barra | `FiltroLocal` (rótulo "Filtrar por sistema", `/` e `Esc`) e grupo segmentado "Filtrar por situação" (Todos, Com divergência, Com erro, cada um com a quantidade; `aria-pressed`) |
| Tabela | Cabeçalho fixo em duas linhas: Sistema; DSV, HML, PRD (`colspan=2`, borda na cor do ambiente); Máquina 1, Máquina 2. Linha com botão expansor (`aria-expanded`, `aria-label` "Caminhos consultados de {sistema}") e seis `CelulaVersao` |
| Detalhe da linha | Por célula com endereço: "{AMB} Máquina {n}", caminho consultado em fonte monoespaçada com `BotaoCopiar` ("Copiar caminho de {AMB}, máquina {n}, do {sistema}"), caminho do sistema raiz quando houver, e a mensagem de erro |
| Estado vazio | Sem resultado no filtro: "Nenhum sistema neste filtro", texto conforme filtro e busca, ação "Ver todos". Sem sistema ativo: "Nenhum sistema cadastrado em {servidor}" e onde cadastrar (`Apps_VersionAddresses`) |
| Carregando | `EstadoCarregando` "Lendo endereços em {servidor}" e, assim que a quantidade é conhecida (feature `ObterAndamento`, consultada a cada segundo, [research.md R10](../research.md#r10-andamento-da-leitura)), "Lendo {N} arquivos de versão"; tempo decorrido, "Cancelar" |
| Erro | `EstadoErro` com servidor, motivo e "Tentar de novo" |
| Legenda e rodapé | As quatro situações; "Abra a linha para ver o caminho consultado."; "Endereços: {servidor} · CSLIB_UserManager.dbo.Apps_VersionAddresses (registros ativos). O resultado fica em cache por {MinutosDeCache} minutos." (valor da configuração) |

## `CelulaVersao`

| Situação | Mostra | Texto para leitor de tela |
|---|---|---|
| Versão | Selo com a versão (classe `ok`) | "{versão}" |
| Versão divergente | Selo com a versão (classe `divergente`) e marcador visual | "{versão}, difere da outra máquina" |
| Erro | Selo "Erro" (classe `erro`) | "Erro ao ler a versão" |
| Não configurado | Traço | "não configurado" |

A coluna da máquina 1 de cada ambiente tem a borda divisória (`divisa`), como no contrato visual.
