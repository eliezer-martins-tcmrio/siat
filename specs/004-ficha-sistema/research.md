# Pesquisa: Ficha do Sistema

**Feature**: [spec.md](./spec.md) | **Plano**: [plan.md](./plan.md) | **Data**: 2026-11-25

As decisões abaixo resolvem os pontos em aberto do contexto técnico. A ficha junta o que as features 002 (catálogo) e 003 (versões) já leem; a pesquisa trata de como ligar as duas sem nova origem, sem copiar código concorrente e sem mudar o que as telas existentes mostram.

## R1. Formato e leitura do campo `Versoes`

- **Decisão**: o campo é uma lista JSON de textos, opcional, no item do `sistemasDb.js`:
  `"Versoes": ["SIGARH", "SIGARH Agendador"]` ([contracts/campo-versoes.md](./contracts/campo-versoes.md)). O `MontadorCatalogo` lê assim:
  - ausente ou `null`: vínculo vazio;
  - lista de textos: cada nome sem espaços nas pontas, vazios descartados, repetidos (pela comparação de R2) contados uma vez, na ordem do campo (FR-009);
  - qualquer outra forma (texto solto, número, objeto, lista com algo que não seja texto): vínculo **inválido** (FR-010). O item continua no catálogo, não conta em `Ignorados` e o motivo vai para o log (Warning), com a posição do item, como os itens ignorados.
- **Por quê lista**: é o tipo nativo do JSON para "vários nomes", não precisa de separador (um nome da tabela pode ter vírgula ou barra) e é fácil de editar à mão no GitLab.
- **Compatibilidade, conferida em 2026-11-25**: o `sistemas.html` percorre só `["DSV", "HML", "PRD", "CONT"]` e `OBS` (linhas 144, 203 e 204), e o `MontadorCatalogo` já ignora campos desconhecidos (comentário da classe e `MontadorCatalogoTests`). O campo pode, então, ser preenchido no TCMRJDoc antes mesmo da publicação desta feature, sem mudar nenhuma das duas páginas.
- **Alternativas consideradas**: texto com nomes separados por `|`, como o campo `URL` (separador ambíguo e uma regra a mais); aceitar texto solto ou lista (duas formas para documentar e testar); campo `Versao` no singular (um item corresponde a vários registros: VE-06.1).

## R2. Comparação de nomes

- **Decisão**: `NomeDeSistema.Normalizar` no `Domain`, puro: tira os espaços das pontas, tira os acentos (decomposição `FormD` sem as marcas não espaçadas, depois `FormC`) e passa para minúsculas na cultura pt-BR. É o mesmo algoritmo que hoje está em `SistemaVersoesVM.Normalizar` (busca da tela de Versões), acrescido do `Trim`; a VM passa a chamá-lo, e a busca de Versões continua igual, porque o nome que vem da tabela já chega sem espaços nas pontas (`EnderecoDeVersao`).
- **Onde vale**: nome do vínculo contra `Apps_VersionAddresses.Sistema` (FR-008); nome pedido no endereço da ficha contra o nome do item do catálogo (FR-012); detecção de item repetido (FR-020) e de nome repetido no vínculo (FR-009).
- **Espaços internos**: não são colapsados ("SCP  Multas" e "SCP Multas" são nomes diferentes), como na busca de Versões. Diferença assim aparece como aviso de nome não encontrado, que é o que se quer: o nome tem de ser escrito como está na tabela.
- **Alternativas consideradas**: comparação no SQL Server com `COLLATE Latin1_General_CI_AI` (só valeria para a tabela, não para o catálogo, e dependeria do collation da coluna); `CompareInfo.Compare` com `IgnoreNonSpace | IgnoreCase` (não gera uma chave para dicionário, que R4 e R5 precisam).

## R3. Endereço da ficha

- **Decisão**: rota `sistemas/catalogo/ficha`, com o nome do item no parâmetro `sistema` e a chave do servidor no parâmetro `servidor` (fora da URL quando é o padrão), montada por `Navegacao.Ficha(sistema, servidor)` ([contracts/rotas-ui.md](./contracts/rotas-ui.md)). Exemplo: `sistemas/catalogo/ficha?sistema=SCP%20-%20Multas&servidor=tcmbd3-dev`.
- **Por quê query string**: `Uri.EscapeDataString` resolve espaços, acentos e `/ ? # & % +`, que aparecem ou podem aparecer em nomes do catálogo. Como parte do caminho (`sistemas/catalogo/{nome}`), `/` exigiria parâmetro catch-all e `%2F`, `%25` e `+` esbarram na filtragem de requisições do IIS (dupla codificação).
- **Por quê embaixo de `sistemas/catalogo`**: o `Menu` usa `NavLinkMatch.Prefix`; com a ficha dentro do caminho do Catálogo, o item **Catálogo** fica marcado sem mudança no menu (FR-003).
- **Identidade pelo nome**: o catálogo não tem identificador estável; criar um campo de identificador no `sistemasDb.js` seria mais um dado para manter à mão. Link antigo de item renomeado cai em "sistema não encontrado" com a busca preenchida (FR-019, premissa da spec).
- **Alternativas consideradas**: rota `sistemas/ficha` (exigiria regra própria no `Menu`); posição do item no arquivo (muda a cada inclusão).

## R4. Localizar o item e montar o mapa de fichas

- **Decisão**: duas funções puras no `Domain`, sobre o `CatalogoLido`:
  - `Localizar(nome)`: os itens cujo nome normalizado é igual ao pedido, em ordem de posição no arquivo; a ficha é do primeiro, e a quantidade dos demais vira o aviso de repetição (FR-020). Para isso, `Sistema` ganha `Posicao` (posição do item na lista do arquivo, a mesma que o log de itens ignorados já usa), porque o `CatalogoLido` guarda os itens em ordem alfabética.
  - `FichasPorVersao()`: dicionário de nome normalizado da tabela → nome do item. Percorre os itens em ordem alfabética (a ordem do catálogo) e, para cada nome do vínculo, fica o primeiro item (edge case da spec: "leva à ficha do primeiro desses itens, em ordem alfabética"). Item repetido que não é o primeiro do seu nome (R4, `Localizar`) e item com vínculo inválido não entram: assim nenhum link de Versões leva a uma ficha cujo vínculo não tenha aquele nome (SC-005), e o nome fica em **Sem ficha**.
- **Custo**: 63 itens e poucas dezenas de nomes; calculado a cada pedido, sem cache próprio.
- **Alternativas consideradas**: guardar o mapa no `CatalogoSistemasService` (mais estado no singleton para um cálculo de microssegundos).

## R5. Versões da ficha: cache e leitura só do sistema

- **Decisão**: o `VersoesPublicadasService` (que já guarda as versões por servidor) ganha `ObterDoSistemaAsync(servidor, vinculo, atualizar, token)`:
  1. Sem "Atualizar agora", procura um resultado válido sem iniciar leitura: o da tela de Versões para o servidor e o da própria ficha para o par servidor e vínculo. Havendo os dois, usa o mais recente (`GeradoEm`). É o caso comum de quem chega à ficha pela tela de Versões (FR-025).
  2. Sem resultado válido, ou com "Atualizar agora", pede ao segundo cache, `LeituraEmCache<ChaveDaFicha, VersoesLidas>`, cuja leitura: lista os endereços ativos do servidor pelo mesmo `IEnderecosDeVersaoRepository` (mesmo SQL), fica só com as linhas cujo nome normalizado está no vínculo e chama o mesmo `LeitorDeVersoes` com elas (FR-026). O resultado é um `VersoesLidas` parcial.
  3. `VersoesDoSistema.De(versoesLidas, vinculo)` (Domain, puro) separa, por nome vinculado, os registros encontrados e os nomes sem registro (FR-017). Serve tanto para o resultado completo da tela de Versões quanto para o parcial da ficha.
- **`LeituraEmCache.Valido(chave)`**: método novo, só de leitura: devolve o último valor da chave quando está na validade e a última releitura não falhou, sem iniciar leitura nem entrar na contagem de quem espera. As regras atuais da classe não mudam.
- **`ChaveDaFicha`**: chave do servidor e `VinculoDeVersoes.Chave` (nomes normalizados, na ordem, unidos por quebra de linha). Mudar o vínculo no catálogo muda a chave, e a ficha lê de novo.
- **O parcial nunca entra no cache da tela de Versões** (FR-027): lá, um `VersoesLidas` com só três sistemas apagaria os demais da tela até a próxima leitura.
- **Memória**: uma entrada por servidor e vínculo usado, no máximo cerca de 63 itens × 3 servidores, cada uma com poucos sistemas; sem expulsão, como as entradas por servidor da 003.
- **Por quê filtrar em memória e não no SQL**: a tabela tem dezenas de linhas (003, R1); o SQL, o mapeamento das 14 colunas e o tratamento de erros continuam os mesmos, e a comparação é exatamente a de R2, qualquer que seja o collation da coluna. Os nomes sem registro saem da mesma lista.
- **Tempo (SC-003)**: consulta à tabela (menos de 1 s no caso normal), sistema raiz (até 5 s) e arquivo montado (até 5 s), com as leituras de um mesmo passo em paralelo: até cerca de 11 s com todas as máquinas do sistema fora do ar, dentro dos 15 s.
- **Alternativas consideradas**: disparar a leitura completa da tela de Versões (até 30 s no pior caso para mostrar um sistema; o VE-06.4 pede ler só o sistema); `FichaDoSistemaService` separado com um método de espiar o cache em `IVersoesPublicadasService` (dois singletons e o andamento duplicado, sem ganho); SQL com `WHERE Sistema IN (...)` (R2).

## R6. Andamento da leitura da ficha

- **Decisão**: o andamento da 003 (R10) passa a ser guardado também para a chave da ficha. `QuantidadeEmLeitura` ganha a sobrecarga `(chaveServidor, vinculo)`, e `ObterAndamento.Query` ganha o vínculo opcional; sem vínculo, é o andamento da tela de Versões, como hoje. A ficha consulta uma vez por segundo, como a tela de Versões, e troca "Lendo endereços em {servidor}" por "Lendo {N} arquivos de versão" (FR-028).
- **Alternativas consideradas**: mostrar só o tempo decorrido na ficha (destoaria da tela de Versões, que mostra a quantidade).

## R7. Autorização

- **Decisão**: três features novas no `App`, cada uma verificando no servidor antes de qualquer leitura:
  - `ObterFicha.Query(Sistema, Atualizar)`: exige `Catalogo_Sistemas.CanSelect` (FR-001); lê o catálogo pelo `ICatalogoSistemasService` (mesmo cache da tela de Catálogo, FR-024) e monta a `FichaVM`. Só inclui o vínculo (nomes e se é inválido) quando o usuário tem também `Versoes_Sistemas.CanSelect` (FR-002, FR-010). Nome que não está no catálogo devolve `Erros.Catalogo.SistemaNaoEncontrado` (FR-019).
  - `ObterVersoesDaFicha.Query(Servidor, Vinculo, Atualizar)`: exige `Catalogo_Sistemas.CanSelect` e, pela mesma `AutorizacaoDasVersoes.AutorizarAsync` da 003, `Versoes_Sistemas.CanSelect` e o servidor na lista fechada (FR-002, FR-022). O vínculo vem do componente da página, que roda no servidor (Blazor Interactive Server): o navegador não o altera, e ele só filtra dados que o usuário já pode ver na tela de Versões.
  - `ListarFichas.Query()`: exige `Catalogo_Sistemas.CanSelect` e devolve o mapa de R4. A tela de Versões só a chama quando o usuário tem o recurso; sem ele, o catálogo não é lido por causa dele (FR-005).
- **Sem DBLog**: catálogo e versões não são dados sensíveis (spec, Premissas; 002 R5 e 003 R6).
- **Alternativas consideradas**: o handler das versões ler o catálogo para achar o vínculo (mais uma passagem pelo cache do catálogo a cada leitura de versões, sem ganho de segurança no Blazor Server).

## R8. Tela da ficha

- **Decisão**: página `Components/Pages/Sistemas/Ficha.razor`, contrato visual [`design/ficha.html`](../../design/ficha.html):
  - `CabecalhoPagina` com o nome do sistema como título, uma trilha de navegação nova (`Trilha`: **Sistemas › Catálogo › nome**, `nav` com `ol` e `aria-current="page"`), a situação das versões em pílulas ("Sem pendências", "Com divergência", "Com erro"), o status das leituras e "Atualizar agora";
  - observação e avisos: item repetido, vínculo inválido, nomes sem registro no servidor, falha de releitura do catálogo ou das versões;
  - `SeletorServidor` e `FaixaContexto`, só com `Versoes_Sistemas.CanSelect`;
  - uma seção por ambiente (esclarecimento de 2026-11-25), na ordem DSV, HML, PRD e Contingência, com título na sigla e na cor (`data-amb`): primeiro as instâncias do catálogo (componente `ListaDeInstancias`, extraído de `CelulaAmbiente`, que passa a só envolvê-lo num `td`), depois a tabela de versões do ambiente (colunas Componente, Máquina 1 e Máquina 2, com a `CelulaVersao` da 003) e, embaixo de cada registro, o caminho consultado com `BotaoCopiar` e a mensagem de erro, sempre visíveis (a ficha já é o detalhe; não há linha para abrir). Contingência mostra só as instâncias e a nota de que não há endereços de versão para ela;
  - legenda das quatro situações e rodapé com as origens (link do `sistemasDb.js` no GitLab; servidor e `Apps_VersionAddresses`) e os tempos de cache.
- **Leitura**: dois `ControleConsulta`, um para o catálogo e um para as versões, como nas telas da 002 e 003. As versões começam quando o catálogo chega (precisam do vínculo); com o catálogo em cache, isso é imediato. Trocar de servidor descarta só a leitura de versões. "Atualizar agora" relê o catálogo e depois as versões (FR-029). As duas leituras mostram tempo decorrido e Cancelar depois de 1 s (Princípio VII): a do catálogo no lugar das seções, como na tela de Catálogo, e a das versões no bloco de versões, com as instâncias visíveis.
- **Estados**: sem `Catalogo_Sistemas.CanSelect`, `AcessoNegado`; sistema não encontrado, `EstadoVazio` com o link para o Catálogo com a busca (FR-019); catálogo com erro, `EstadoErro` com "Tentar de novo"; versões com erro, `EstadoErro` só no bloco de versões, com o catálogo visível (FR-030); servidor padrão fora da lista do usuário, `EstadoVazio` pedindo para escolher o servidor, como na tela de Versões; recurso perdido durante a sessão, acesso negado na página (catálogo) ou só no bloco de versões, sem o resultado anterior (versões).
- **Alternativas consideradas**: linha expansível por registro, como na tela de Versões (um clique a mais numa página que só tem poucos registros); um `CabecalhoPagina` próprio da ficha (a trilha cabe como parâmetro opcional do existente).

## R9. Mudanças nas telas de Catálogo e Versões

- **Catálogo**: o nome na coluna Sistema vira link para a ficha (FR-004), com o texto visível igual ao nome e o prefixo "Ficha do" só para leitor de tela (`so-leitor`), para que o nome acessível contenha o texto visível (WCAG 2.5.3). A busca e as colunas não mudam (FR-011).
- **Versões**:
  - Com `Catalogo_Sistemas.CanSelect`, a página chama `ListarFichas` junto com `ObterVersoes`, sem esperar por ela para mostrar a tabela (FR-031). Com o mapa, o nome do sistema vira link para a ficha, levando o servidor escolhido; o clique no link não abre a linha (`stopPropagation`).
  - `FiltroDeSituacao` ganha `SemFicha`, na URL `filtro=semficha` (FR-006, esclarecimento de 2026-11-25). A opção só aparece quando o mapa está disponível; enquanto ele não chega, o filtro da URL fica guardado e é aplicado na chegada; sem o recurso ou com falha do catálogo, vale "Todos" (casos extremos).
  - Falha do catálogo: aviso "As fichas dos sistemas estão indisponíveis: {motivo}", nomes sem link e sem **Sem ficha**.
- **Alternativas consideradas**: esperar o catálogo antes de mostrar a tabela (atrasaria Versões pelo GitLab); ler o catálogo dentro de `ObterVersoes` (misturaria os dois recursos numa feature e leria o catálogo para quem não tem acesso a ele).

## R10. Pacotes e configuração

- **Decisão**: nenhum pacote novo e nenhuma chave nova de configuração: a ficha usa `CatalogoSistemas` (origem e `MinutosDeCache`) e `Versoes` (`ServidorPadrao`, `TempoLimiteSegundos`, `LeiturasSimultaneas`, `MinutosDeCache`) como estão. Nenhum script novo no `dbchange`: os recursos `Catalogo_Sistemas` e `Versoes_Sistemas` já existem.
