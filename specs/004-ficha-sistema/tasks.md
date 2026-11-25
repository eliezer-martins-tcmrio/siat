---

description: "Lista de tarefas da feature 004 — Ficha do Sistema"
---

# Tarefas: Ficha do Sistema

**Entrada**: documentos de design em `specs/004-ficha-sistema/`

**Pré-requisitos**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Testes**: incluídos. A constituição (Princípio V) exige testes automatizados das regras e testes de componente para componentes com lógica; os fluxos das histórias ficam no roteiro manual do [quickstart](./quickstart.md). Os testes de cada história são escritos antes da implementação e devem falhar primeiro, salvo os de regressão marcados como tal (T024 e T043), que passam já na primeira execução.

**Organização**: tarefas agrupadas por história da [spec.md](./spec.md), para que cada uma seja implementada, testada e entregue de forma independente.

## Formato: `[ID] [P?] [História] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem depender de tarefa incompleta)
- **[US1]…[US5]**: história da spec a que a tarefa pertence
- Todo caminho é relativo à raiz do repositório (`D:\Projetos\siat`)

## Convenções de caminho

Solução em `src/Siat.slnx`, com `Tcmrj.Siat.Domain`, `Tcmrj.Siat.Infra`, `Tcmrj.Siat.App`, `Tcmrj.Siat.Web` e `Tcmrj.Siat.Test`. Siga os padrões das features 002 e 003:

- `Result<T, Erro>` (`Tcmrj.CSharpFunctionalExtensions`);
- features com `Query` + `Handler` do `NetDevPack.SimpleMediator` que autorizam no servidor antes de qualquer leitura (exemplos: `src/Tcmrj.Siat.App/Web/Features/Catalogo/ObterCatalogo.cs` e `src/Tcmrj.Siat.App/Web/Features/Versoes/ObterVersoes.cs`, com a `AutorizacaoDasVersoes` no mesmo arquivo);
- cache, leitura única e cancelamento por contagem com `LeituraEmCache<TChave, TValor>` (`src/Tcmrj.Siat.App/Web/Services/LeituraEmCache.cs`), usado em `CatalogoSistemasService.cs` e `VersoesPublicadasService.cs`;
- página com dois estados de leitura, "Atualizar agora", andamento e Cancelar (exemplo: `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Versoes.razor`); seletor de servidor e parâmetros na URL (o mesmo exemplo);
- testes bUnit herdando de `BunitContext`, com `IMediator` falso (`src/Tcmrj.Siat.Test/Componentes/CatalogoPaginaTests.cs` e `VersoesPaginaTests.cs`); testes de unidade com `FakeTimeProvider` e funções de leitura controladas por `TaskCompletionSource` (`src/Tcmrj.Siat.Test/Unidade/LeituraEmCacheTests.cs`).

Referências: contrato visual `design/ficha.html` (configuração "design" do `.claude/launch.json`, porta 5178), tokens e classes em `src/Tcmrj.Siat.Web/wwwroot/css/siat.css`, campo `Versoes` em [contracts/campo-versoes.md](./contracts/campo-versoes.md), tela em [contracts/rotas-ui.md](./contracts/rotas-ui.md).

Nenhuma tarefa cria script em `dbchange/`, chave no `appsettings.json` ou pacote ([research.md R10](./research.md#r10-pacotes-e-configuração)).

---

## Fase 1: Preparação

**Objetivo**: branch, linha de base verde e dados de teste para o roteiro manual.

- [X] T001 Criar o branch `features/<tarefa>_ficha_sistema` (ou `features/ficha_sistema`, sem número de tarefa) a partir de `main` e conferir que `dotnet build src/Siat.slnx` e `dotnet test --solution src/Siat.slnx` passam antes de qualquer mudança
- [ ] T002 [P] No TCMRJDoc (fora deste repositório), criar o branch de teste `teste-ficha-siat` com o campo `Versoes` em quatro itens do `sistemasDb.js`, como no pré-requisito 1 do [quickstart.md](./quickstart.md#pré-requisitos): SIGARH com dois nomes que existem e um que não existe, um item com nome em minúsculas e sem acento, um com `"Versoes": "SCP"` e um sem o campo; conferir os nomes em `dbo.Apps_VersionAddresses` de `TCMBD3\DEV` e `TCMBD4\HML` só com `SELECT` ([database.instructions.md](../../.github/instructions/database.instructions.md))

---

## Fase 2: Base (bloqueia todas as histórias)

**Objetivo**: comparação de nomes, campo `Versoes` lido do catálogo, localização do item e mapa de fichas, erro novo, endereços da ficha e espiada no cache.

**⚠️ Nenhuma história começa antes desta fase.**

### Testes da Base ⚠️

- [X] T003 [P] Criar `src/Tcmrj.Siat.Test/Unidade/NomeDeSistemaTests.cs` para `NomeDeSistema.Normalizar` ([research.md R2](./research.md#r2-comparação-de-nomes)), um caso por linha de `[Theory]`: `"  eTCM Impressão "` → `"etcm impressao"`; `"SIGARH"` → `"sigarh"`; `"SIGARH  Agendador"` mantém os espaços internos; `"Ação"` → `"acao"`; `""` → `""`; `null` → `ArgumentNullException`; o mesmo resultado com `CultureInfo.CurrentCulture` em `tr-TR` (minúsculas sempre pela pt-BR); e `SistemaVersoesVM.Normalizar(x)` igual a `NomeDeSistema.Normalizar(x)`
- [X] T004 [P] Criar `src/Tcmrj.Siat.Test/Unidade/Catalogo/VinculoDeVersoesTests.cs` para `VinculoDeVersoes` ([data-model.md](./data-model.md#vinculodeversoes-domain-novo)) com a fábrica `VinculoDeVersoes.De(IEnumerable<string?> nomes)`: `Nomes` "como escritos no catálogo, sem espaços nas pontas, sem vazios, sem repetidos (pela forma normalizada, fica a primeira grafia), na ordem do campo" (`["SIGARH", " ", null, "sigarh", "SIGARH Agendador "]` → `["SIGARH", "SIGARH Agendador"]`); `Vazio` quando não sobra nome; `Chave` = nomes normalizados, na ordem, unidos por `\n`; `Contem("  sigarh agendador")` verdadeiro e `Contem("SIGARH Relatórios")` falso; `Nenhum` (válido, vazio) e `ForaDoFormato` (`Invalido = true`, `Nomes` vazio)
- [X] T005 [P] Em `src/Tcmrj.Siat.Test/Unidade/Catalogo/MontadorCatalogoTests.cs` e na amostra `src/Tcmrj.Siat.Test/Unidade/Catalogo/Amostras/sistemasDb.amostra.js`, testar a leitura do campo conforme a tabela de [contracts/campo-versoes.md](./contracts/campo-versoes.md#leitura-pelo-siat): lista de textos → vínculo com os nomes; ausente ou `null` → `VinculoDeVersoes.Nenhum`; `[]` ou só textos vazios → vínculo vazio e válido; texto solto, número, objeto, `true`/`false` e lista com item que não é texto → `ForaDoFormato`, o item continua no catálogo e um log Warning cita a posição e o nome do item; `Posicao` a partir de 1 na ordem do arquivo, contando os itens ignorados (a mesma posição do log de itens ignorados). Na amostra, acrescentar o campo a itens que já existem (lista válida, texto solto, sem o campo) para não mudar as contagens dos testes atuais; os formatos restantes em `[Theory]` com o JSON embutido no teste
- [X] T006 [P] Criar `src/Tcmrj.Siat.Test/Unidade/Catalogo/CatalogoLidoTests.cs` ([research.md R4](./research.md#r4-localizar-o-item-e-montar-o-mapa-de-fichas)): `Localizar("  sigarh ")` encontra "SIGARH"; nome inexistente, vazio ou só espaços → nulo; com dois itens "SIGARH" (posições 30 e 7, a lista em ordem alfabética), devolve o de `Posicao` 7 e `Repetidos = 1`; `FichasPorVersao()`: nome normalizado da tabela → `Nome` do item; um nome no vínculo de dois itens fica com o primeiro em ordem alfabética (`TryAdd`); o vínculo do segundo item de um nome repetido não entra no mapa (salvo por outro item); vínculo inválido e itens sem vínculo não entram
- [X] T007 [P] Em `src/Tcmrj.Siat.Test/Unidade/NavegacaoTests.cs` ([research.md R3](./research.md#r3-endereço-da-ficha)): `Navegacao.RotaFicha` é `sistemas/catalogo/ficha` e começa com `Navegacao.RotaCatalogo`; `Navegacao.Ficha("SIGARH")` → `sistemas/catalogo/ficha?sistema=SIGARH`; `Navegacao.Ficha("eTCM Impressão", "tcmbd3-dev")` → `sistemas/catalogo/ficha?sistema=eTCM%20Impress%C3%A3o&servidor=tcmbd3-dev`; `Navegacao.Ficha("A&B/C?#%")` → `sistemas/catalogo/ficha?sistema=A%26B%2FC%3F%23%25` (caso extremo dos caracteres de endereço); servidor nulo, vazio ou só espaços fica fora da URL; `Navegacao.Versoes(null, null, FiltroDeSituacao.SemFicha)` → `sistemas/versoes?filtro=semficha`; `Navegacao.LerFiltro("semficha")` → `SemFicha`; os casos atuais continuam passando
- [X] T008 [P] Em `src/Tcmrj.Siat.Test/Unidade/LeituraEmCacheTests.cs`, testar `Valido(chave)` ([research.md R5](./research.md#r5-versões-da-ficha-cache-e-leitura-só-do-sistema)): nulo antes da primeira leitura; o valor dentro da validade; nulo depois da validade; nulo quando a última releitura falhou, mesmo com o anterior guardado; nulo para outra chave; não chama a função de leitura e não entra na contagem de quem espera (com um único pedido em andamento, chamar `Valido` e cancelar o pedido ainda cancela a leitura); durante um "atualizar" em andamento, devolve o valor anterior se ainda estiver na validade

### Implementação da Base

- [X] T009 [P] Criar `src/Tcmrj.Siat.Domain/Models/NomeDeSistema.cs` (estático e puro): `Normalizar(string)` com `Trim()`, decomposição `FormD` sem `UnicodeCategory.NonSpacingMark`, recomposição `FormC` e `ToLower` com `CultureInfo.GetCultureInfo("pt-BR")`; fazer `SistemaVersoesVM.Normalizar` em `src/Tcmrj.Siat.App/Web/ViewModels/Versoes/SistemaVersoesVM.cs` delegar a ele. T003 passa e os testes da tela de Versões continuam verdes
- [X] T010 Criar `src/Tcmrj.Siat.Domain/Models/Catalogo/VinculoDeVersoes.cs` (depois do T009): `Nomes` (`IReadOnlyList<string>`), `Invalido` (`bool`), `Vazio` e `Chave` calculados, `Contem(string nome)`, fábrica `De(IEnumerable<string?>)` e os valores `Nenhum` e `ForaDoFormato`, com as regras de [data-model.md](./data-model.md#vinculodeversoes-domain-novo). T004 passa
- [X] T011 Em `src/Tcmrj.Siat.Domain/Models/Catalogo/Sistema.cs`, acrescentar `Versoes` (`VinculoDeVersoes`; "`Nenhum` quando ausente") e `Posicao` (`int`; "posição do item na lista do `sistemasDb.js`, a partir de 1") como parâmetros opcionais do construtor (`versoes: null` → `Nenhum`), para os testes e chamadas atuais continuarem compilando; `Nome`, `Observacao` e `Ambientes` não mudam
- [X] T012 Em `src/Tcmrj.Siat.Domain/Models/Catalogo/CatalogoLido.cs`, acrescentar `Localizar(string nome)` → `ItemLocalizado?` (novo `sealed record ItemLocalizado(Sistema Item, int Repetidos)` no mesmo arquivo) e `FichasPorVersao()` → `IReadOnlyDictionary<string, string>`, conforme [data-model.md](./data-model.md#catalogolido-domain-existente-novos-métodos). T006 passa
- [X] T013 Em `src/Tcmrj.Siat.Infra/Catalogo/MontadorCatalogo.cs`, ler o campo `Versoes` de cada item ([research.md R1](./research.md#r1-formato-e-leitura-do-campo-versoes)) e a posição do item no arquivo: lista só de textos → `VinculoDeVersoes.De(...)`; ausente ou `null` → `Nenhum`; qualquer outro formato → `ForaDoFormato` e log Warning "O campo Versoes do item {Posicao} ({Nome}) não é uma lista de nomes; o vínculo foi ignorado.", sem descartar o item; `SistemaVM` e `TextoDeBusca` continuam sem o campo (FR-011). T005 passa
- [X] T014 [P] Acrescentar a `Erros.Catalogo` em `src/Tcmrj.Siat.Domain/Models/Erros.cs` a constante `CodigoSistemaNaoEncontrado = "catalogo.sistema-nao-encontrado"` e `SistemaNaoEncontrado(string nome)` com a mensagem "Não há item com o nome {nome} no catálogo. Ele pode ter sido renomeado ou removido."
- [X] T015 [P] Em `src/Tcmrj.Siat.App/Web/Navegacao.cs`, acrescentar `RotaFicha = "sistemas/catalogo/ficha"`, `ParametroSistema = "sistema"`, `ValorFiltroSemFicha = "semficha"`, `Ficha(string sistema, string? servidor = null)` com `Uri.EscapeDataString`, e tratar `SemFicha` em `Versoes(...)` e `LerFiltro`; em `src/Tcmrj.Siat.App/Web/FiltroDeSituacao.cs`, acrescentar `SemFicha` depois de `ComErro`. Até a História 2, a tela de Versões trata `SemFicha` como `Todos`. T007 passa
- [X] T016 [P] Em `src/Tcmrj.Siat.App/Web/Services/LeituraEmCache.cs`, acrescentar `TValor? Valido(TChave chave)`: devolve o último valor da chave quando está na validade e a última releitura não falhou, sem iniciar leitura nem entrar na contagem de quem espera, sob o mesmo bloqueio de `ObterAsync`; as regras atuais não mudam. T008 passa

**Ponto de controle**: T003–T008 passam; os testes do Catálogo e das Versões continuam verdes; as telas de Catálogo e Versões não mudam.

---

## Fase 3: História 1 — Ver num só lugar onde o sistema roda e o que está publicado (Prioridade: P1) 🎯 MVP

**Objetivo**: a página da ficha, aberta pelo nome no Catálogo, com a trilha, uma seção por ambiente com as instâncias e, no servidor padrão, a tabela de versões dos nomes vinculados, vindas do cache da tela de Versões, do cache da ficha ou de uma leitura só daquele sistema.

**Teste independente**: com um usuário que tem os dois recursos e um item com vínculo, abrir a ficha pelo Catálogo e conferir as instâncias com a tela de Catálogo e as versões com a tela de Versões, no mesmo servidor e no mesmo momento (quickstart, seção 1).

### Testes da História 1 ⚠️

- [X] T017 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/VersoesDoSistemaTests.cs` para `VersoesDoSistema.De(versoes, vinculo)` ([data-model.md](./data-model.md#versoesdosistema-domain-novo-puro)): componentes na ordem do vínculo, não na da tabela; "sigarh agendador" no vínculo acha "SIGARH Agendador" na tabela, e `ComponenteDaFicha.Nome` fica como escrito no vínculo; duas linhas da tabela com o mesmo nome → um componente com dois `Registros`, por `Id`; nome sem linha → `NaoEncontrados`, na ordem do vínculo; `TemDivergencia` e `TemErro` quando algum registro tem; nomes não encontrados não contam em `TemErro` nem em `SemPendencias` (FR-017); sistemas da tabela fora do vínculo são ignorados, tanto num `VersoesLidas` completo quanto num parcial
- [X] T018 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Catalogo/ObterFichaHandlerTests.cs` ([research.md R7](./research.md#r7-autorização)) com `IAutorizacaoConsultaService` e `ICatalogoSistemasService` falsos: sem `Catalogo_Sistemas.CanSelect` → `Erros.Catalogo.AcessoNegado` e o catálogo não é lido; só com o catálogo → `FichaVM` com `MostraVersoes = false`, `Vinculo` vazio e `VinculoInvalido = false` mesmo com vínculo inválido no item; com os dois recursos → `Vinculo` igual a `Nomes`, `VinculoInvalido` do item e `MostraVersoes = true`; `"  sigarh "` acha "SIGARH"; nome inexistente, vazio ou só espaços → `Erros.Catalogo.SistemaNaoEncontrado`; `Repetidos` do `Localizar`; quatro `Ambientes` na ordem DSV, HML, PRD, CONT; `LidoEm`, `Origem` e `Falha` (`FalhaVM`) vindos do `CatalogoObtido`; `Atualizar` repassado ao serviço; erro do catálogo repassado
- [X] T019 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/ObterVersoesDaFichaHandlerTests.cs`: sem `Catalogo_Sistemas.CanSelect` → `Erros.Catalogo.AcessoNegado`; sem `Versoes_Sistemas.CanSelect` → `Erros.Versoes.AcessoNegado`; servidor nulo → `ServidorPadrao`; chave inexistente → `Erros.Consultas.ServidorNaoEncontrado`; em todos esses casos o serviço não é chamado; vínculo vazio → resultado sem componentes, sem chamar o serviço ([data-model.md](./data-model.md#validações)); autorizado → `ObterDoSistemaAsync(servidor, VinculoDeVersoes.De(vinculo), atualizar, token)` e `VersoesDaFichaVM` com `Servidor`, `GeradoEm`, `Duracao`, `Falha` (`FalhaDasVersoesVM`), `NaoEncontrados`, as três situações e `ComponenteVM.Registros` montados por `SistemaVersoesVM.De` do mesmo `SistemaVersionado`, com as mesmas células e divergências da tela de Versões (SC-004)
- [X] T020 [P] [US1] Em `src/Tcmrj.Siat.Test/Unidade/Versoes/VersoesPublicadasServiceTests.cs`, acrescentar `ObterDoSistemaAsync` ([data-model.md](./data-model.md#ciclo-das-versões-da-ficha)) com repositório e leitor de arquivo falsos e `FakeTimeProvider`: com o cache da tela válido para o servidor, devolve dele sem chamar o repositório; com o cache da ficha válido para o servidor e o vínculo, idem; com os dois, usa o de `GeradoEm` maior; com o cache da tela cuja última releitura falhou, não o usa; sem nenhum válido, lê uma vez e o leitor de arquivo recebe só os endereços das linhas do vínculo; nenhuma linha do vínculo na tabela não é erro (resultado sem sistemas); depois de uma leitura da ficha, `ObterAsync` do mesmo servidor faz a sua leitura completa (o parcial não entrou no cache da tela, FR-027); vínculos diferentes no mesmo servidor e o mesmo vínculo em servidores diferentes têm resultados separados; `atualizar = true` ignora os dois caches e relê só o vínculo
- [X] T021 [P] [US1] Criar `src/Tcmrj.Siat.Test/Componentes/ListaDeInstanciasTests.cs` com os casos de instância de `CelulaAmbienteTests.cs` sem o `td` (rótulos de URL, máquina e caminho; links em nova aba; vários endereços separados por `|`; máquinas copiáveis uma a uma; `BotaoCopiar` com o nome acessível; "não configurado"); em `src/Tcmrj.Siat.Test/Componentes/CelulaAmbienteTests.cs`, acrescentar um teste de que o conteúdo do `td` é igual ao `ListaDeInstancias` com os mesmos parâmetros (os testes atuais continuam passando, sem mudança)
- [X] T022 [P] [US1] Criar `src/Tcmrj.Siat.Test/Componentes/FichaPaginaTests.cs` (parte da História 1), no padrão de `VersoesPaginaTests.cs`, conforme [contracts/rotas-ui.md](./contracts/rotas-ui.md#tela-sistemasficharazor): com `?sistema=SIGARH`, envia `ObterFicha.Query("SIGARH", false)`; com `?sistema=A%26B%2FC%3F%23%25`, envia `ObterFicha.Query("A&B/C?#%", false)`; trilha `nav[aria-label="Trilha de navegação"]` com `ol` Sistemas › link Catálogo (`sistemas/catalogo`) › "SIGARH" com `aria-current="page"`; `h1` "SIGARH", título da aba "SIGARH · SIAT" e a descrição; observação como texto, com quebras de linha e marcação exibida literalmente (FR-021); quatro `section` na ordem DSV, HML, PRD e Contingência, com `data-amb`, sigla e `h2` com o nome; instâncias pelo `ListaDeInstancias`; com `MostraVersoes` e vínculo, envia `ObterVersoesDaFicha.Query(null, ["SIGARH", "SIGARH Agendador"], false)` depois da ficha; enquanto as versões não chegam, as instâncias já aparecem com "Lendo endereços em {servidor}" no bloco de versões (FR-024); tabela "Versões em DSV" com `th scope="col"` Componente, Máquina 1 e Máquina 2, uma linha por registro (dois registros com o mesmo nome → duas linhas) com duas `CelulaVersao` e a divergência; embaixo, "Máquina {n}", o caminho em `code`, `BotaoCopiar` "Copiar caminho de DSV, máquina 1, do SIGARH Agendador", o caminho do raiz e a mensagem de erro como texto; ambiente sem nenhum caminho → "Nenhum endereço de versão configurado em HML."; Contingência só com instâncias e "Não há endereços de versão para Contingência."; pílulas "Sem pendências" ou "Com divergência" e "Com erro" juntas; status "Catálogo lido às HH:mm · Versões geradas às HH:mm em N,N s"; legenda das quatro situações e rodapé com o link do `sistemasDb.js`, o servidor, `CSLIB_UserManager.dbo.Apps_VersionAddresses (registros ativos)` e os tempos de cache; enquanto `ObterFicha` não responde, `EstadoCarregando` "Lendo o catálogo no GitLab…" no lugar das seções, com tempo decorrido e Cancelar depois de 1 s, e Cancelar sem ficha anterior leva ao `EstadoErro` com "Tentar de novo" (Princípio VII)
- [X] T023 [P] [US1] Em `src/Tcmrj.Siat.Test/Componentes/CatalogoPaginaTests.cs` ([contracts/rotas-ui.md](./contracts/rotas-ui.md#mudanças-na-tela-de-catálogo)): o nome de cada sistema é `a[href]` para `Navegacao.Ficha(nome)` com o texto visível igual ao nome e `span.so-leitor` "Ficha do " antes dele; nome com espaço e acento vai escapado no `href`; a busca por um nome que só está no vínculo ("agendador") não encontra o item (FR-011); colunas, busca e contagens iguais às de antes
- [X] T024 [P] [US1] Em `src/Tcmrj.Siat.Test/Componentes/MenuTests.cs`, testar com o endereço `sistemas/catalogo/ficha?sistema=SIGARH`: o item "Catálogo" fica marcado como o atual, "Versões" não, e o menu não tem item novo (FR-003). Teste de regressão: deve passar já na primeira execução, pelo `NavLinkMatch.Prefix`

### Implementação da História 1

- [X] T025 [P] [US1] Criar `src/Tcmrj.Siat.Domain/Models/Versoes/VersoesDoSistema.cs` (puro), com `ComponenteDaFicha(string Nome, IReadOnlyList<SistemaVersionado> Registros)` e `De(VersoesLidas versoes, VinculoDeVersoes vinculo)`, que agrupa `versoes.Sistemas` por `NomeDeSistema.Normalizar(Nome)` e procura cada nome do vínculo, conforme [data-model.md](./data-model.md#versoesdosistema-domain-novo-puro). T017 passa
- [X] T026 [P] [US1] Criar `src/Tcmrj.Siat.App/Web/Services/ChaveDaFicha.cs`: `readonly record struct ChaveDaFicha(string Servidor, string Vinculo)`, com a chave de andamento `"{Servidor}\n{Vinculo}"`; fábrica a partir de `Servidor` e `VinculoDeVersoes` (`Vinculo` = `VinculoDeVersoes.Chave`)
- [X] T027 [US1] Em `src/Tcmrj.Siat.App/Web/Services/Interfaces/IVersoesPublicadasService.cs`, acrescentar `Task<Result<VersoesObtidas, Erro>> ObterDoSistemaAsync(Servidor servidor, VinculoDeVersoes vinculo, bool atualizar, CancellationToken cancellationToken)`, com documentação XML de [research.md R5](./research.md#r5-versões-da-ficha-cache-e-leitura-só-do-sistema); implementar em `src/Tcmrj.Siat.App/Web/Services/VersoesPublicadasService.cs`: um segundo `LeituraEmCache<ChaveDaFicha, VersoesLidas>` com o mesmo `MinutosDeCache` e o mesmo `TimeProvider`; sem `atualizar`, `Valido` do cache da tela (chave do servidor) e do cache da ficha, ficando com o de `GeradoEm` maior, sem leitura; senão, a leitura da ficha: `ListarAtivosAsync` → linhas com `vinculo.Contem(endereco.Sistema)` → `LeitorDeVersoes` → `VersoesLidas` parcial; o parcial nunca é gravado no cache da tela. Depende de T016, T025 e T026. T020 passa
- [X] T028 [US1] Criar os view models de [data-model.md](./data-model.md#view-models-app): `src/Tcmrj.Siat.App/Web/ViewModels/Catalogo/FichaVM.cs` (`Nome`, `Observacao`, `Ambientes` como em `SistemaVM.De`, `Repetidos`, `Vinculo`, `VinculoInvalido`, `MostraVersoes`, `LidoEm`, `Origem`, `Falha`; fábrica `De(ItemLocalizado, CatalogoObtido, bool mostraVersoes)` que zera vínculo e `VinculoInvalido` sem o recurso de versões), `src/Tcmrj.Siat.App/Web/ViewModels/Versoes/ComponenteVM.cs` (`Nome`, `Registros` de `SistemaVersoesVM`) e `src/Tcmrj.Siat.App/Web/ViewModels/Versoes/VersoesDaFichaVM.cs` (fábrica `De(VersoesObtidas, VinculoDeVersoes, ServidorVM)` sobre `VersoesDoSistema.De`). Depende de T025
- [X] T029 [US1] Criar `src/Tcmrj.Siat.App/Web/Features/Catalogo/ObterFicha.cs` (`Query(string? Sistema, bool Atualizar)` → `Result<FichaVM, Erro>`): `AutorizarAsync(Modulo.Catalogo_Sistemas)` antes de ler; catálogo pelo `ICatalogoSistemasService`, como a `ObterCatalogo` (mesmo cache, FR-024); `Localizar` → `Erros.Catalogo.SistemaNaoEncontrado` quando nulo; `MostraVersoes` pela política de `Modulo.Versoes_Sistemas`; sem DBLog. Depende de T012, T014 e T028. T018 passa
- [X] T030 [US1] Criar `src/Tcmrj.Siat.App/Web/Features/Versoes/ObterVersoesDaFicha.cs` (`Query(string? Servidor, IReadOnlyList<string> Vinculo, bool Atualizar)` → `Result<VersoesDaFichaVM, Erro>`): `AutorizarAsync(Modulo.Catalogo_Sistemas)` e, depois, `AutorizacaoDasVersoes.AutorizarAsync` da 003 (recurso de versões e servidor na lista fechada); `ObterDoSistemaAsync` e `VersoesDaFichaVM.De`. Depende de T027 e T028. T019 passa
- [X] T031 [P] [US1] Extrair de `src/Tcmrj.Siat.Web/Components/Shared/CelulaAmbiente.razor` o novo `src/Tcmrj.Siat.Web/Components/Shared/ListaDeInstancias.razor` (parâmetros `Sistema` e `Ambiente`, o conteúdo atual sem o `td`, com `Quebravel` e os separadores); `CelulaAmbiente` passa a ser `<td><ListaDeInstancias Sistema="@Sistema" Ambiente="@Ambiente" /></td>`. T021 passa
- [X] T032 [P] [US1] Em `src/Tcmrj.Siat.Web/Components/Shared/CabecalhoPagina.razor`, acrescentar o parâmetro opcional `Trilha` (`RenderFragment?`), desenhado antes do `h1`; sem ele, o HTML é o de antes (os testes das páginas atuais continuam verdes)
- [X] T033 [US1] Criar `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Ficha.razor` (rota `/sistemas/catalogo/ficha`, `[Authorize]`, `sistema` por `[SupplyParameterFromQuery(Name = Navegacao.ParametroSistema)]`) conforme [contracts/rotas-ui.md](./contracts/rotas-ui.md#tela-sistemasficharazor) e [research.md R8](./research.md#r8-tela-da-ficha): verifica `PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)` antes de ler parâmetros e de chamar qualquer feature (sem ele, `AcessoNegado` com o recurso); dois `ControleConsulta`, um para `ObterFicha` e outro para `ObterVersoesDaFicha` (servidor padrão, só com `MostraVersoes` e vínculo não vazio, iniciado quando a ficha chega); leitura do catálogo com `EstadoCarregando` "Lendo o catálogo no GitLab…", tempo decorrido e Cancelar depois de 1 s, como em `Catalogo.razor`; `CabecalhoPagina` com `Trilha`, pílulas e status; seções por ambiente com `ListaDeInstancias` e a tabela de versões montada a partir de `CelulaVersaoVM.Ambiente`; legenda e rodapé; erro de `ObterFicha` → `EstadoErro` com a mensagem (o estado próprio de "não encontrado" entra na História 3). Depende de T029–T032. T022 passa
- [X] T034 [US1] Em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Catalogo.razor`, trocar o nome na coluna Sistema por `<a href="@Navegacao.Ficha(sistema.Nome)"><span class="so-leitor">Ficha do </span>@sistema.Nome</a>`, sem mudar busca nem colunas (FR-004, FR-011). T023 passa
- [X] T035 [P] [US1] Acrescentar a `src/Tcmrj.Siat.Web/wwwroot/css/siat.css` o que o contrato visual `design/ficha.html` usa e ainda não existe: trilha de navegação, seção por ambiente (faixa e etiqueta da sigla pela cor de `data-amb`), pílulas da situação, `.tabela-ficha` com a linha de caminhos (`tr.caminhos-linha td { white-space: normal }`, `.caminhos { grid-template-columns: auto minmax(0, 1fr) }`, `.erro-msg { grid-column: 2 }`), reaproveitando tokens e classes existentes, nos temas claro, escuro e do sistema

**Ponto de controle**: T017–T024 passam (T024 sem mudança no `Menu.razor`, pelo `NavLinkMatch.Prefix`); com o SIAT rodando, o Catálogo leva à ficha do SIGARH com instâncias e versões do servidor padrão (quickstart, seção 1).

---

## Fase 4: História 2 — Chegar à ficha pela tela de Versões e compartilhar (Prioridade: P2)

**Objetivo**: seletor de servidor e servidor na URL da ficha; na tela de Versões, nomes com link para a ficha, levando o servidor, e o filtro **Sem ficha**, sem atrasar a tabela.

**Teste independente**: na tela de Versões, com um servidor que não seja o padrão, acionar o nome de um sistema vinculado, conferir o servidor na ficha, copiar a URL e abri-la com outro usuário autorizado (quickstart, seção 2).

### Testes da História 2 ⚠️

- [X] T036 [P] [US2] Criar `src/Tcmrj.Siat.Test/Unidade/Catalogo/ListarFichasHandlerTests.cs`: sem `Catalogo_Sistemas.CanSelect` → `Erros.Catalogo.AcessoNegado` e o catálogo não é lido; autorizado → `FichasVM.PorNome` igual a `FichasPorVersao()`; `FichasVM.Ficha(sistemaVersoesVM)` pelo `NomeNormalizado` → nome do item ou nulo; falha do catálogo sem leitura anterior → o erro; com leitura anterior (`Falha`) → o mapa da leitura anterior
- [X] T037 [P] [US2] Em `src/Tcmrj.Siat.Test/Componentes/VersoesPaginaTests.cs` ([contracts/rotas-ui.md](./contracts/rotas-ui.md#mudanças-na-tela-de-versões)): com `Catalogo_Sistemas.CanSelect`, `ListarFichas` é enviada junto com `ObterVersoes` e a tabela aparece com `ListarFichas` ainda pendente (FR-031); quando o mapa chega, nome no mapa → `a[href]` para `Navegacao.Ficha(nomeDoItem, servidorDaUrl)` com o texto visível da tabela e "Ficha do " só para leitor de tela, e nome fora do mapa → texto; com `?servidor=tcmbd3-dev` o link leva `&servidor=tcmbd3-dev`, e no servidor padrão não leva; clicar no link não abre a linha; botão **Sem ficha** depois de "Com erro", com a quantidade e `aria-pressed`, mostra só os nomes fora do mapa, combina com a busca e grava `filtro=semficha` com `NavigateTo(..., replace: true)`; estados vazios "Não há nenhum sistema sem ficha nesta leitura." e "... com \"{busca}\" no nome."; `?filtro=semficha` com o mapa pendente mostra a tabela toda e seleciona **Sem ficha** quando o mapa chega; falha de `ListarFichas` → `Aviso` "As fichas dos sistemas estão indisponíveis: {motivo}.", nomes sem link, sem **Sem ficha** e `filtro=semficha` tratado como "Todos", sem aviso próprio
- [X] T038 [P] [US2] Em `src/Tcmrj.Siat.Test/Componentes/FichaPaginaTests.cs`, acrescentar: com `MostraVersoes` e vínculo, `SeletorServidor` "Servidor dos endereços" com os grupos de `ListarPermitidos(Modulo.Versoes_Sistemas)` e `FaixaContexto` com o servidor; `?servidor=tcmbd3-dev` → `ObterVersoesDaFicha` com `tcmbd3-dev`; trocar o servidor envia só `ObterVersoesDaFicha` (sem nova `ObterFicha`) e navega com `NavigateTo(Navegacao.Ficha(sistema, chave), replace: true)`, sem `servidor` quando é o padrão; `?servidor=xyz` → `Aviso` "O servidor xyz não está na lista; mostrando {padrão}." e leitura do padrão (FR-023); sem `servidor` na URL, o seletor mostra o padrão selecionado (História 2, cenário 2); servidor padrão fora da lista do usuário e sem `servidor` válido na URL → `EstadoVazio` "Escolha o servidor dos endereços" no bloco de versões, sem `ObterVersoesDaFicha`

### Implementação da História 2

- [X] T039 [P] [US2] Criar `src/Tcmrj.Siat.App/Web/ViewModels/Catalogo/FichasVM.cs` (`PorNome`; `Ficha(SistemaVersoesVM s)` → nome do item ou nulo) e `src/Tcmrj.Siat.App/Web/Features/Catalogo/ListarFichas.cs` (`Query()` → `Result<FichasVM, Erro>`, `AutorizarAsync(Modulo.Catalogo_Sistemas)` antes de ler, mapa de `CatalogoLido.FichasPorVersao()`). T036 passa
- [X] T040 [US2] Em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Ficha.razor`, acrescentar o `SeletorServidor` e a `FaixaContexto` (só com `MostraVersoes` e vínculo), o parâmetro `servidor` na URL, a troca de servidor (descarta só a leitura de versões em curso) o `Aviso` de servidor fora da lista e o `EstadoVazio` de servidor padrão fora da lista do usuário, como em `Versoes.razor`. T038 passa
- [X] T041 [US2] Em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Versoes.razor`, com `PodeAcessarModuloAsync(Modulo.Catalogo_Sistemas)`, enviar `ListarFichas` em paralelo com `ObterVersoes`, sem esperar por ela para mostrar a tabela; links na coluna Sistema com `@onclick:stopPropagation`; opção **Sem ficha** no grupo de filtros (só com o mapa), com a quantidade e a seleção pelo `NomeNormalizado` fora do mapa; `filtro=semficha` guardado até o mapa chegar; `Aviso` de fichas indisponíveis na falha. T037 passa

**Ponto de controle**: T036–T038 passam; quickstart, seção 2.

---

## Fase 5: História 3 — Vínculo explícito e avisos de vínculo quebrado (Prioridade: P2)

**Objetivo**: os avisos e estados do vínculo na ficha: inválido, sem vínculo, nome sem registro no servidor, item repetido e sistema não encontrado.

**Teste independente**: vincular a um item um nome existente, um com maiúsculas e acentos diferentes e um inexistente; conferir a ficha em dois servidores (`TCMBD3\DEV` e `TCMBD4\HML`) e o filtro **Sem ficha** (quickstart, seção 3).

### Testes da História 3 ⚠️

- [X] T042 [P] [US3] Em `src/Tcmrj.Siat.Test/Componentes/FichaPaginaTests.cs`, acrescentar ([contracts/rotas-ui.md](./contracts/rotas-ui.md#tela-sistemasficharazor), [contracts/campo-versoes.md](./contracts/campo-versoes.md#leitura-pelo-siat)): `VinculoInvalido` → `Aviso` "O campo Versoes deste sistema no catálogo não é uma lista de nomes; corrija no sistemasDb.js." e nenhuma `ObterVersoesDaFicha`; vínculo vazio com `MostraVersoes` → `Aviso` informativo "Este sistema não tem versões vinculadas. Para ver as versões aqui, liste os nomes de Apps_VersionAddresses no campo Versoes do item, no sistemasDb.js." com link para a `Origem`, sem seletor, sem tabelas e sem `ObterVersoesDaFicha`; um `Aviso` por nome de `NaoEncontrados`, com o título "Nome vinculado não encontrado" e "{nome} não está entre os registros ativos de {servidor}. Confira o campo Versoes do {sistema} no sistemasDb.js.", fora das pílulas; `Repetidos = 2` → "Há 2 outro(s) item(ns) com o nome SIGARH no catálogo; esta ficha mostra o primeiro. Corrija no sistemasDb.js."; `Erros.Catalogo.SistemaNaoEncontrado` → `EstadoVazio` "Sistema não encontrado no catálogo", "Não há item com o nome Inexistente no catálogo. Ele pode ter sido renomeado ou removido." e a ação "Procurar no Catálogo" para `Navegacao.Catalogo("Inexistente")`; `sistema` ausente ou só com espaços → o mesmo estado, sem enviar `ObterFicha`; nomes com marcação exibidos como texto nos avisos (FR-021)
- [X] T043 [P] [US3] Em `src/Tcmrj.Siat.Test/Unidade/Catalogo/CatalogoLidoTests.cs`, acrescentar as invariantes de SC-005 sobre o catálogo da amostra lido pelo `MontadorCatalogo` e sobre catálogos gerados no teste: para cada par do `FichasPorVersao()`, o vínculo do item de destino (o primeiro do seu nome) contém aquele nome; todo nome de vínculo válido de um item que é o primeiro do seu nome está no mapa. Teste de regressão das invariantes da Base: deve passar já na primeira execução

### Implementação da História 3

- [X] T044 [US3] Em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Ficha.razor`, acrescentar os avisos de item repetido, vínculo inválido e nomes não encontrados, a nota de "sem vínculo" com o link do `sistemasDb.js`, e o estado de sistema não encontrado (também para `sistema` ausente ou só com espaços), todos com o texto como conteúdo, sem `MarkupString`. T042 passa

**Ponto de controle**: T042 e T043 passam; quickstart, seção 3.

---

## Fase 6: História 4 — Ficha rápida, atual e que não trava (Prioridade: P2)

**Objetivo**: leitura única por ficha e servidor, andamento com tempo decorrido e Cancelar, "Atualizar agora" e os estados de falha do catálogo e das versões, cada um na sua parte.

**Teste independente**: abrir a ficha logo após a tela de Versões sem nova leitura; com o cache vencido e uma máquina inacessível, conferir o tempo e o erro só na célula; publicar em DSV e acionar "Atualizar agora" (quickstart, seção 4).

### Testes da História 4 ⚠️

- [X] T045 [P] [US4] Em `src/Tcmrj.Siat.Test/Unidade/Versoes/VersoesPublicadasServiceTests.cs`, acrescentar para `ObterDoSistemaAsync` (FR-027, FR-028, FR-030): dois pedidos simultâneos do mesmo servidor e vínculo → uma leitura do repositório; cancelar o único pedido cancela o token da leitura e nada fica guardado; com dois esperando, cancelar um não interrompe; falha sem anterior → o erro; falha com anterior → o anterior com a falha, e o pedido seguinte relê; `QuantidadeEmLeitura("tcmbd3-dev", vinculo)` nulo enquanto a tabela é lida, a quantidade de arquivos do parcial durante a leitura dos arquivos e nulo no fim; durante a leitura da ficha, `QuantidadeEmLeitura("tcmbd3-dev")` da tela não muda
- [X] T046 [P] [US4] Em `src/Tcmrj.Siat.Test/Unidade/Versoes/ObterAndamentoHandlerTests.cs`, acrescentar ([research.md R6](./research.md#r6-andamento-da-leitura-da-ficha)): com vínculo, devolve `QuantidadeEmLeitura(chave, VinculoDeVersoes.De(vinculo))`; sem vínculo, o comportamento atual; a autorização (recurso de versões e servidor na lista) é a mesma nos dois casos e vem antes de consultar o serviço
- [X] T047 [P] [US4] Em `src/Tcmrj.Siat.Test/Componentes/FichaPaginaTests.cs`, acrescentar: "Atualizar agora" envia `ObterFicha` com `Atualizar = true` e depois `ObterVersoesDaFicha` com `Atualizar = true`, e o status muda (FR-029); durante a leitura de versões, `EstadoCarregando` no bloco de versões com "Lendo endereços em {servidor}", tempo decorrido e Cancelar, trocado por "Lendo 4 arquivos de versão" quando `ObterAndamento` (com o vínculo) devolve 4; Cancelar volta o bloco ao resultado anterior ou a `EstadoErro` com "Tentar de novo", com as instâncias visíveis; erro das versões sem resultado anterior → `EstadoErro` "Não foi possível ler as versões em {servidor}" com o motivo e "Tentar de novo" só no bloco de versões; com resultado anterior (`Falha`) → o anterior e `Aviso` com o motivo e o horário; erro do catálogo sem leitura anterior → `EstadoErro` com o motivo e "Tentar de novo", como na tela de Catálogo; com leitura anterior → a ficha e o `Aviso` da falha (FR-030); Cancelar durante a releitura do catálogo ("Atualizar agora") volta à ficha anterior; recurso perdido durante a sessão ([contracts/rotas-ui.md](./contracts/rotas-ui.md#tela-sistemasficharazor), linha Erro): `Erros.Catalogo.AcessoNegado` em `ObterFicha` ou `ObterVersoesDaFicha` → a tela de acesso negado; `Erros.Versoes.AcessoNegado` em `ObterVersoesDaFicha` → o bloco de versões sem o resultado anterior, com `EstadoErro` e a mensagem, sem "Tentar de novo", e o catálogo visível

### Implementação da História 4

- [X] T048 [US4] Em `src/Tcmrj.Siat.App/Web/Services/Interfaces/IVersoesPublicadasService.cs` e `src/Tcmrj.Siat.App/Web/Services/VersoesPublicadasService.cs`, guardar o andamento também pela chave da ficha (`"{Servidor}\n{Vinculo}"`) e acrescentar a sobrecarga `int? QuantidadeEmLeitura(string chaveServidor, VinculoDeVersoes vinculo)`; conferir que o `CancellationToken` chega ao repositório e ao `LeitorDeVersoes` na leitura da ficha, e que uma leitura cancelada não fica como resultado nem como falha. T045 passa
- [X] T049 [US4] Em `src/Tcmrj.Siat.App/Web/Features/Versoes/ObterAndamento.cs`, mudar para `Query(string? Servidor, IReadOnlyList<string>? Vinculo = null)` (as chamadas atuais continuam compilando) e, com vínculo, consultar a sobrecarga do T048. T046 passa
- [X] T050 [US4] Em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Ficha.razor`, acrescentar "Atualizar agora" (relê o catálogo e depois as versões), o andamento no bloco de versões consultando `ObterAndamento` com o vínculo a cada segundo, o tempo decorrido, Cancelar (também na releitura do catálogo), os estados e avisos de falha do catálogo e das versões e o recurso perdido durante a sessão, como em `Versoes.razor` e `Catalogo.razor`. T047 passa

**Ponto de controle**: T045–T047 passam; quickstart, seção 4 (bloco de versões completo em até 15 s com as máquinas fora do ar, SC-003).

---

## Fase 7: História 5 — Acesso controlado à ficha (Prioridade: P3)

**Objetivo**: comprovar, pela URL direta e pelas features, que cada parte só chega a quem tem o recurso (SC-006).

**Teste independente**: abrir a URL direta de uma ficha, com e sem servidor, com um usuário sem nenhum dos recursos, com só o do catálogo e com só o de versões (quickstart, seção 5).

### Testes da História 5 ⚠️

- [X] T051 [P] [US5] Em `src/Tcmrj.Siat.Test/Features/Permissoes.feature`, citar a ficha no texto da funcionalidade e acrescentar o esquema de cenário "Partes da ficha liberadas por recurso", com os exemplos catálogo × versões (não/não, sim/não, não/sim, sim/sim) e as colunas da ficha (negada, sem vínculo, com vínculo), das versões da ficha (negadas, entregues) e das fichas da tela de Versões (negadas, entregues), e o passo de que nenhuma leitura é feita para o que foi negado; criar os passos em `src/Tcmrj.Siat.Test/Features/Steps/PermissoesDaFichaSteps.cs`, chamando os handlers `ObterFicha`, `ObterVersoesDaFicha` e `ListarFichas` com autorização e serviços falsos que contam as leituras
- [X] T052 [P] [US5] Em `src/Tcmrj.Siat.Test/Componentes/FichaPaginaTests.cs`, acrescentar: sem `Catalogo_Sistemas.CanSelect`, com e sem `servidor` na URL, a página mostra o acesso negado citando `Catalogo_Sistemas.CanSelect`, não envia nenhuma feature (`ObterFicha`, `ListarPermitidos`, `ObterVersoesDaFicha`, `ObterAndamento`) e não renderiza dado do catálogo; só com o catálogo (`MostraVersoes = false`) e `?servidor=tcmbd3-dev`, a ficha mostra só o catálogo: sem seletor, faixa, tabelas, legenda das versões, avisos de vínculo, nota de como vincular ou aviso de servidor, e sem `ObterVersoesDaFicha` nem `ListarPermitidos`
- [X] T053 [P] [US5] Em `src/Tcmrj.Siat.Test/Componentes/VersoesPaginaTests.cs`, acrescentar: só com `Versoes_Sistemas.CanSelect`, a página não envia `ListarFichas`, mostra os nomes sem link e sem **Sem ficha**, e `?filtro=semficha` fica em "Todos", sem aviso (FR-005)

### Implementação da História 5

- [X] T054 [US5] Conferir em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Ficha.razor` que a verificação de `Catalogo_Sistemas.CanSelect` acontece antes de ler parâmetros e de chamar qualquer feature, que o `servidor` da URL só é lido com `MostraVersoes`, e em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Versoes.razor` que `ListarFichas` só é enviada com o recurso do catálogo; ajustar o que faltar. T051, T052 e T053 passam

**Ponto de controle**: T051–T053 passam; quickstart, seção 5.

---

## Fase 8: Acabamento

**Objetivo**: acessibilidade, documentação, verificação completa e publicação.

- [ ] T055 [P] Revisar a acessibilidade (WCAG 2.2 AA) da ficha e das mudanças em Catálogo e Versões, só com teclado e com leitor de tela: ordem de foco (trilha, Atualizar agora, seletor, links, botões de cópia), foco visível, link "Ficha do {sistema}" com o nome acessível contendo o texto visível, botões de cópia com ambiente, máquina e componente, seções com `aria-labelledby` no `h2`, `th scope`, região `aria-live="polite"` no andamento e no resultado do filtro (incluindo **Sem ficha**), contraste nos três temas, e todos os textos novos em português do Brasil (FR-035); ajustar `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Ficha.razor`, `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Versoes.razor` e `src/Tcmrj.Siat.Web/wwwroot/css/siat.css` (FR-034, SC-007)
- [X] T056 [P] Atualizar a documentação: `README.md` (a ficha e o campo `Versoes` do `sistemasDb.js`), `DESIGN.md` (trilha de navegação e seções por ambiente, se forem padrão novo), `.github/copilot-instructions.md` (telas de Sistemas com a ficha) e `PRD.md` (perto da linha "VE-01 a VE-05 atendidos pela feature 003", registrar "VE-06 atendido pela feature 004 (`specs/004-ficha-sistema/`)")
- [X] T057 [P] Rodar `dotnet build src/Siat.slnx` sem avisos novos e `dotnet test --solution src/Siat.slnx` com todos os testes verdes
- [ ] T058 Executar o [quickstart.md](./quickstart.md) inteiro em desenvolvimento com o branch de teste do T002 e registrar o resultado em `specs/004-ficha-sistema/validacao.md`, como na 003, incluindo que a falha do catálogo ou das versões não afeta MailQueue e AuditLog (FR-032); o roteiro usa no `appsettings.json` local `CatalogoSistemas:GitLab:Ref` no branch de teste e `Versoes:ServidorPadrao` em `tcmbd3-dev`; ao fim, voltar os dois (`master` e `tcmbd-prd`), sem commitar as trocas
- [ ] T059 Antes de publicar, conferir que nenhuma configuração, script ou pacote novo vai junto (R10) e combinar com a equipe o preenchimento inicial do campo `Versoes` no TCMRJDoc ([contracts/campo-versoes.md](./contracts/campo-versoes.md#preenchimento-inicial)), que pode ser feito antes ou depois da publicação

---

## Dependências e ordem de execução

### Entre fases

- **Preparação (Fase 1)**: sem dependências. T002 só é necessária para o quickstart.
- **Base (Fase 2)**: depende da Preparação; bloqueia todas as histórias. T009 → T010 → T011 → T012 e T013.
- **História 1 (Fase 3)**: depende da Base. É o MVP.
- **História 2 (Fase 4)**: a parte da tela de Versões (T036, T037, T039, T041) depende só da Base, mas os links só têm destino depois do T033; a parte da ficha (T038, T040) depende da História 1.
- **História 3 (Fase 5)**: depende da História 1 (página e `VersoesDaFichaVM`).
- **História 4 (Fase 6)**: depende da História 1; T048 e T049 podem correr em paralelo com as Histórias 2 e 3.
- **História 5 (Fase 7)**: depende das Histórias 1 e 2 (T053 usa `ListarFichas`).
- **Acabamento (Fase 8)**: depois das histórias desejadas.
- `Ficha.razor` é alterado em T033, T040, T044, T050 e T054: uma pessoa de cada vez, nessa ordem.

### Dentro da História 1

- Testes T017–T024 antes da implementação, e falhando (T024 pode já passar, pelo `NavLinkMatch.Prefix`).
- T025, T026, T031, T032 e T035 em paralelo.
- T027 depende de T016, T025 e T026; T028 depende de T025; T029 depende de T012, T014 e T028; T030 depende de T027 e T028; T033 depende de T029–T032; T034 depende de T015.

## Paralelismo

### Preparação e Base

```text
T001  ||  T002 (TCMRJDoc)
T003 NomeDeSistema | T004 vínculo | T005 montador | T006 CatalogoLido | T007 Navegacao | T008 LeituraEmCache
T009 → T010 → T011 → T012 | T013     ||     T014 Erros | T015 Navegacao | T016 Valido
```

### História 1

```text
Testes:  T017 VersoesDoSistema | T018 ObterFicha | T019 ObterVersoesDaFicha | T020 serviço | T021 ListaDeInstancias | T022 página | T023 Catálogo | T024 menu
Depois:  T025 | T026 | T031 | T032 | T035 | T034
Por fim: T028 → T029 | T027 → T030 → T033
```

### Histórias 2 a 5

```text
T036 | T037 | T038 | T042 | T043 | T045 | T046 | T047 | T051 | T052 | T053
T039 → T041 (Versões)   ||   T048 → T049 (andamento)
T040 → T044 → T050 → T054 (Ficha.razor, um de cada vez)
```

---

## Estratégia de implementação

### MVP primeiro (História 1)

1. Fases 1 e 2.
2. Fase 3 (História 1): a ficha já abre pelo Catálogo, com instâncias e versões do servidor padrão, vindas do cache da tela de Versões ou de uma leitura só do sistema, com cache de 5 minutos e leitura única (vêm do `LeituraEmCache`).
3. **Parar e validar**: quickstart, seção 1, com a tela de Versões lado a lado (SC-004).
4. Publicar em DSV para o suporte experimentar.

### Entrega incremental

1. Base + História 1 → MVP.
2. História 2 → servidor na ficha, links e **Sem ficha** em Versões.
3. História 3 → avisos de vínculo, que guiam o preenchimento do campo `Versoes`.
4. História 4 → "Atualizar agora", andamento, Cancelar e falhas.
5. História 5 → testes de acesso pela URL.
6. Acabamento, quickstart completo e publicação.

### Em equipe

- Uma pessoa no Domain e no Infra (T009–T014, T025), outra no App (T016, T026–T030, T039, T048, T049), outra no Web (T031–T035, T041).
- Nas Fases 4 a 7, uma pessoa integra `Ficha.razor` de cada vez.

---

## Observações

- Fora do SIAT e desta lista: preencher o campo `Versoes` no `sistemasDb.js` do TCMRJDoc, pelo GitLab, com os nomes como estão em `TCMBD` ([contracts/campo-versoes.md](./contracts/campo-versoes.md#preenchimento-inicial)); o filtro **Sem ficha** em PRD mostra o que falta, e os avisos da ficha, o que está escrito errado.
- O `sistemas.html` do TCMRJDoc ignora o campo; nenhuma tarefa o altera.
- Não consultar `TCMBD` (PRD) a partir da máquina de desenvolvimento: os testes usam fontes falsas e o quickstart usa `TCMBD3\DEV` como servidor padrão local e `TCMBD4\HML` como o outro servidor.
- Commit ao fim de cada tarefa ou grupo lógico, no branch `features/<tarefa>_ficha_sistema`.
