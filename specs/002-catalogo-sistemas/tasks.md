---

description: "Lista de tarefas da feature 002 — Catálogo de Sistemas"
---

# Tarefas: Catálogo de Sistemas

**Entrada**: documentos de design em `specs/002-catalogo-sistemas/`

**Pré-requisitos**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Testes**: incluídos. A constituição (Princípio V) exige testes automatizados das regras e testes de componente para componentes com lógica; os fluxos das histórias ficam no roteiro manual do [quickstart](./quickstart.md). Os testes de cada história são escritos antes da implementação e devem falhar primeiro.

**Organização**: tarefas agrupadas por história da [spec.md](./spec.md), para que cada uma seja implementada, testada e entregue de forma independente.

## Formato: `[ID] [P?] [História] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem depender de tarefa incompleta)
- **[US1]…[US4]**: história da spec a que a tarefa pertence
- Todo caminho é relativo à raiz do repositório (`D:\Projetos\siat`)

## Convenções de caminho

Solução em `src/Siat.slnx`, com `Tcmrj.Siat.Domain`, `Tcmrj.Siat.Infra`, `Tcmrj.Siat.App`, `Tcmrj.Siat.Web` e `Tcmrj.Siat.Test`. Siga os padrões já existentes da feature 001: `Result<T, Erro>` (`Tcmrj.CSharpFunctionalExtensions`), features com `Query` + `Handler` do `NetDevPack.SimpleMediator` (exemplo: `src/Tcmrj.Siat.App/Web/Features/AuditLog/ListarAplicacoes.cs`), erros em `Erros.*` (`src/Tcmrj.Siat.Domain/Models/Erros.cs`), páginas que verificam a política no `OnInitializedAsync` e mostram `AcessoNegado` (`src/Tcmrj.Siat.Web/Components/Pages/Consultas/MailQueue.razor`), testes bUnit herdando de `BunitContext` (`src/Tcmrj.Siat.Test/Componentes/MenuTests.cs`).

Referências: contrato visual `design/sistemas.html`, tokens e classes em `src/Tcmrj.Siat.Web/wwwroot/css/siat.css`, origem real `D:\Projetos\TCMRJDoc\sistemasDb.js` (só para conferência; não copiar inteiro para o repositório).

---

## Fase 1: Preparação

**Objetivo**: configuração, script do UserManager e amostra de teste.

- [X] T001 Conferir que `src/Tcmrj.Siat.Web/Tcmrj.Siat.Web.csproj` não tem `UserSecretsId`: o SIAT não usa `dotnet user-secrets` e o token do GitLab fica no `appsettings` (constituição 3.1.0, [contracts/configuracao.md](./contracts/configuracao.md))
- [X] T002 [P] Acrescentar a seção `CatalogoSistemas` a `src/Tcmrj.Siat.Web/appsettings.json`, com `GitLab:Token` vazio para quem roda preencher: `GitLab:BaseUrl` `https://git.tcmnet.tcm`, `GitLab:Projeto` `asi/TCMRJDoc`, `GitLab:Arquivo` `sistemasDb.js`, `GitLab:Ref` `master`, `TempoLimiteSegundos` 10, `MinutosDeCache` 10
- [X] T003 [P] Criar o script idempotente `dbchange/196.1.1/003 - TCMRio - CSLIB_UserManager - Recurso Sistemas e grupo Catalogo do SIAT.sql`, no padrão de `dbchange/196.1.1/001 - ...sql`: localizar a aplicação `SIAT` pelo nome (erro se não existir), inserir em `AppResources` o recurso `Id = 30`, `ResourceKey = 'Sistemas'`, `Description = 'Catálogo de sistemas'`, `ParentId = NULL`; criar o grupo `Catalogo` (Id sequencial na tabela `Groups` inteira) e em `Accesses` `CanSelect = 1` em `Sistemas`, demais permissões em 0; sem usuários ([contracts/usermanager.md](./contracts/usermanager.md)). Antes, confirmar com o responsável se a sprint ainda é a 196; se mudou, usar a pasta da sprint nova
- [X] T004 [P] Criar a amostra `src/Tcmrj.Siat.Test/Unidade/Catalogo/Amostras/sistemasDb.amostra.js`, em UTF-8 com BOM, recortada do arquivo real e cobrindo: `var data = [` no início e `];` no fim; vírgula sobrando depois de `]` e de `}`; um sistema com `OBS:` sem aspas ("Portal do Servidor - INTRANET"); "SCP - Sistema de Controle de Processos" com `"G:\DESENVOLVIMENTO\VBSCP\\SCP DESENVOLVIMENTO.EXE"` e `URL` `"N/A"`; "SIGA-RH - AgenteCalculo" com `"C:\\TcmRoot\SIGARH_Agentes\\AgenteCalculo\\DSV\\"`; "SCP Api " (espaço no fim) com `"W:\\TcmRoot\\\DSV\\Sites\\scp_api_dsv"`; "WebNews - Intranet" com HML de três campos vazios; "RabbitMQ - Gerenciamento" com URLs separadas por `|`; "Contratos" sem nenhum ambiente; "SCP (Visualização de Processos)" com `<br />` na `OBS`; e máquinas "TCM2016WEB1, TCM2016WEB2". Marcar no `src/Tcmrj.Siat.Test/Tcmrj.Siat.Test.csproj` como `None` com `CopyToOutputDirectory` `PreserveNewest`

---

## Fase 2: Base (bloqueia todas as histórias)

**Objetivo**: módulo, modelo do domínio, configuração, rota e item de menu.

**⚠️ Nenhuma história começa antes desta fase.**

### Testes da Base ⚠️

- [X] T005 [P] Acrescentar a linha `| Sistemas | Sistemas.CanSelect | Sistemas |` aos exemplos de `src/Tcmrj.Siat.Test/Features/Permissoes.feature` e ajustar o texto da funcionalidade para não falar só da seção Consultas
- [X] T006 [P] Em `src/Tcmrj.Siat.Test/Unidade/NavegacaoTests.cs`, testar `Navegacao.Sistemas()` → `catalogo/sistemas`, `Navegacao.Sistemas("multas hml")` → `catalogo/sistemas?busca=multas%20hml`, e busca nula, vazia ou só com espaços → sem parâmetro
- [X] T007 [P] Em `src/Tcmrj.Siat.Test/Componentes/MenuTests.cs`, testar: com `Sistemas.CanSelect` aparece o grupo "Catálogo" com o item "Sistemas" apontando para `catalogo/sistemas`; o grupo "Catálogo" vem antes de "Consultas" quando os dois aparecem; sem `Sistemas.CanSelect` o grupo "Catálogo" não aparece; os testes existentes de Consultas continuam passando
- [X] T008 [P] Criar `src/Tcmrj.Siat.Test/Unidade/Catalogo/CatalogoSistemasOptionsTests.cs`: válido com os valores do T002 e token; inválido (com a lista de motivos) para `BaseUrl` não absoluto ou fora de `http`/`https`, `Projeto`, `Arquivo` ou `Ref` vazios, `TempoLimiteSegundos` fora de 1 a 60, `MinutosDeCache` fora de 1 a 60, token ausente; os motivos nunca contêm o token. No mesmo arquivo, testar que `builder.AddTcmrjSiat()` com uma configuração sem a seção `CatalogoSistemas` (e sem token) monta o `ServiceProvider` e resolve `ICatalogoSistemasService` sem exceção, ou seja, o SIAT sobe e só a tela de Sistemas mostra `NaoConfigurado` (FR-025)

### Implementação da Base

- [X] T009 [P] Acrescentar `Sistemas` a `src/Tcmrj.Siat.Domain/Enums/Modulo.cs` e reescrever o comentário do enum ("Módulos do SIAT. O nome é também o recurso do UserManager"); `ModuloExtensions.Politica()` já produz `Sistemas.CanSelect`
- [X] T010 [P] Criar `src/Tcmrj.Siat.Domain/Enums/AmbienteDoCatalogo.cs` (`Dsv`, `Hml`, `Prd`, `Cont`, nesta ordem, que é a ordem das colunas) e `src/Tcmrj.Siat.Domain/Extensions/AmbienteDoCatalogoExtensions.cs` com `ChaveNoArquivo()` (`DSV`, `HML`, `PRD`, `CONT`), `Sigla()` (`DSV`, `HML`, `PRD`, `CONT`), `Nome()` (Desenvolvimento, Homologação, Produção, Contingência) e `DataAmb()` (`dsv`, `hml`, `prd`, `cont`) ([data-model.md](./data-model.md#ambientedocatalogo-enum))
- [X] T011 [P] Criar em `src/Tcmrj.Siat.Domain/Models/Catalogo/` os registros imutáveis `Instancia.cs` (`Enderecos` lista de texto, `Maquinas` lista de texto, `Caminho` texto ou nulo; invariante "pelo menos um dos três preenchido"), `Sistema.cs` (`Nome` obrigatório e sem espaços nas pontas, `Observacao` texto ou nulo com quebras de linha preservadas, `Ambientes` mapa `AmbienteDoCatalogo` → lista de `Instancia` "sempre com as quatro chaves") e `CatalogoLido.cs` (`Sistemas` "ordenada por `Nome` na cultura pt-BR", `Ignorados` inteiro ≥ 0, `LidoEm` `DateTimeOffset` UTC, `Origem` URL do arquivo na interface do GitLab)
- [X] T012 [P] Acrescentar `Erros.Catalogo` a `src/Tcmrj.Siat.Domain/Models/Erros.cs`, com códigos `catalogo.*` e as mensagens de [research.md R6](./research.md#r6-erros-e-mensagens): `NaoConfigurado`, `AcessoRecusado` (401), `ArquivoNaoEncontrado` (403/404), `TempoEsgotado(segundos)`, `Indisponivel(motivo)`, `FormatoInvalido(motivo, linha, coluna)`, `Cancelada` ("A leitura do catálogo foi cancelada.") e `AcessoNegado` ("Você não tem acesso ao catálogo de sistemas."); nenhuma recebe o token
- [X] T013 [P] Criar `src/Tcmrj.Siat.Domain/Repositories/ICatalogoSistemasRepository.cs` com `Task<Result<CatalogoLido, Erro>> LerAsync(CancellationToken cancellationToken)`
- [X] T014 Criar `src/Tcmrj.Siat.Infra/Configurations/CatalogoSistemasOptions.cs` (seção `CatalogoSistemas`; `GitLab` com `BaseUrl`, `Projeto`, `Arquivo`, `Ref`, `Token`; `TempoLimiteSegundos`; `MinutosDeCache`) com um método `Validar()` que devolve os motivos de invalidez sem o token, chamado na leitura e não na subida (Princípio IV), e `src/Tcmrj.Siat.Infra/Configurations/CatalogoSistemasConfiguration.cs` com `AddCatalogoSistemas(IConfiguration)`, que por ora só faz `Configure<CatalogoSistemasOptions>` (o `HttpClient` do repositório entra no T025)
- [X] T015 [P] Em `src/Tcmrj.Siat.App/Web/Navegacao.cs`, acrescentar `RotaSistemas = "catalogo/sistemas"`, `ParametroBusca = "busca"` e `Sistemas(string? busca = null)`, usando o `Montar` existente e deixando de fora a busca vazia ou só com espaços
- [X] T016 Em `src/Tcmrj.Siat.Web/Components/Layout/Menu.razor`, transformar a lista fixa de Consultas em uma lista de grupos (`Catálogo` com `Sistemas`, ícone Lucide `server`, rota `Navegacao.RotaSistemas`; depois `Consultas` com MailQueue e AuditLog), avaliando cada item pela `Modulo.Politica()` como hoje e omitindo o grupo sem itens visíveis; `aria-label` e título de cada grupo pelo nome do grupo

**Ponto de controle**: T005 a T007 e a validação de opções do T008 passam (a parte de subida do T008 só passa depois do T029); o menu mostra **Catálogo › Sistemas** para quem tem o recurso (a rota ainda não existe).

---

## Fase 3: História 1 — Localizar onde um sistema roda (Prioridade: P1) 🎯 MVP

**Objetivo**: ler o `sistemasDb.js` do GitLab e mostrar a tabela com busca, links e cópia.

**Teste independente**: com um usuário que tem só `Sistemas.CanSelect`, abrir **Catálogo › Sistemas**, buscar sistemas conhecidos e conferir com o `sistemas.html` (quickstart, seções 1 e 2).

### Testes da História 1 ⚠️

- [X] T017 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Catalogo/LeitorSistemasDbTests.cs` para `LeitorSistemasDb.ParaJson(string)` ([research.md R2](./research.md#r2-interpretação-do-sistemasdbjs)): descarta BOM, `var data =` e `;`; aceita vírgula sobrando; põe aspas em nome de campo sem aspas (`OBS:`) e aceita nome entre aspas simples; aceita texto entre aspas simples; mantém comentários `//` e `/* */` fora de texto e não confunde `//` dentro de texto (`"https://x"`); traduz escapes como o JavaScript, um caso por linha de `[Theory]`: `\n`, `\r`, `\t`, `\b`, `\f`, `\v`, `\0`, `\x41` → `A`, `\u00e7` → `ç`, `\u{1F600}` → 😀, `\'`, `\"`, `\\` → `\`, barra + quebra de linha → nada, `\D` → `D`, `\S` → `S`; `"G:\DESENVOLVIMENTO\VBSCP\\SCP"` → `G:DESENVOLVIMENTOVBSCP\SCP`; `"W:\\TcmRoot\\\DSV"` → `W:\TcmRoot\DSV`; erro de formato com linha e coluna para `undefined`, concatenação `"a" + "b"`, texto sem aspas de fechamento e falta do `]` final; a amostra do T004 converte e o resultado é JSON válido para o `System.Text.Json`
- [X] T018 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Catalogo/MontadorCatalogoTests.cs` para as regras de [research.md R3](./research.md#r3-validação-dos-itens): `Title` sem espaços nas pontas ("SCP Api "); `URL` dividida por `|` e `Machine` por `,`, com partes vazias descartadas; instância com os três campos vazios descartada (WebNews HML vira lista vazia); ambiente ausente vira lista vazia e "Contratos" tem as quatro listas vazias; `OBS` vazia vira nulo e quebras de linha são preservadas; item sem `Title`, com `Title` só de espaços, ou com ambiente que não é lista é ignorado e contado em `Ignorados`; campo desconhecido ignorado; raiz que não é lista ou lista vazia é `FormatoInvalido`; ordenação pt-BR ("Ética" entre "Etc" e "Exame", acentos e maiúsculas); a amostra do T004 produz os sistemas esperados com `Ignorados = 0`
- [X] T019 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Catalogo/CatalogoSistemasRepositoryTests.cs` com um `HttpMessageHandler` falso (classe de teste, sem biblioteca) e `FakeTimeProvider`: requisição `GET https://git.tcmnet.tcm/api/v4/projects/asi%2FTCMRJDoc/repository/files/sistemasDb.js/raw?ref=master` com cabeçalho `PRIVATE-TOKEN`; 200 com a amostra → `CatalogoLido` com `LidoEm` do relógio e `Origem` `https://git.tcmnet.tcm/asi/TCMRJDoc/-/blob/master/sistemasDb.js`; opções inválidas ou token ausente → `NaoConfigurado` sem nenhuma chamada HTTP
- [X] T020 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Catalogo/CatalogoSistemasServiceTests.cs` (parte básica) com repositório falso e `FakeTimeProvider`: o primeiro pedido lê; outro pedido antes de `MinutosDeCache` não lê de novo; depois de `MinutosDeCache` lê de novo; falha sem leitura anterior devolve o erro
- [X] T021 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Catalogo/ObterCatalogoHandlerTests.cs`: sem `Sistemas.CanSelect` devolve `Erros.Catalogo.AcessoNegado` e não chama o serviço; com a política devolve `CatalogoVM` com `Total`, `LidoEm` e `Origem`; `EnderecoVM.EhLink` verdadeiro para `http://` e `HTTPS://` e falso para `N/A`, `AgenteSIGA.EXE (diversos agendamentos)`, `javascript:alert(1)` e `\\servidor\pasta`; `Exibicao` sem o esquema; `SistemaVM.Ambientes` sempre com as quatro entradas na ordem DSV, HML, PRD, CONT; `TextoDeBusca` contém nome, observação, endereços completos, máquinas e caminhos de todas as instâncias
- [X] T022 [P] [US1] Criar `src/Tcmrj.Siat.Test/Componentes/CelulaAmbienteTests.cs` (bUnit) para [contracts/rotas-ui.md](./contracts/rotas-ui.md#componente-celulaambiente): lista vazia → `.vazio-cel` com "não configurado" para leitor de tela; endereço `EhLink` → `<a>` com `target="_blank"`, `rel="noopener noreferrer"` e "(abre em nova aba)" em `.so-leitor`; endereço sem `EhLink` → texto sem `<a>`; "TCM2016WEB1, TCM2016WEB2" → duas máquinas, cada uma com seu botão de cópia de nome acessível "Copiar máquina TCM2016WEB1 de PRD do SGQ"; caminho com botão "Copiar caminho de PRD do SGQ"; duas instâncias → dois `div.instancia`; valor com `<b>` aparece codificado; `<wbr>` depois de `/`, `\`, `_`, `.` e `-`

### Implementação da História 1

- [X] T023 [US1] Criar `src/Tcmrj.Siat.Infra/Catalogo/LeitorSistemasDb.cs`: `ParaJson(string)` → `Result<string, Erro>` que percorre o texto caractere a caractere conforme [research.md R2](./research.md#r2-interpretação-do-sistemasdbjs) e [contracts/origem-sistemasdb.md](./contracts/origem-sistemasdb.md#sintaxe-aceita-além-do-json), devolvendo `Erros.Catalogo.FormatoInvalido` com linha e coluna (contadas no arquivo original) para construção não aceita
- [X] T024 [US1] Criar `src/Tcmrj.Siat.Infra/Catalogo/MontadorCatalogo.cs`: recebe o JSON do T023, lê com `JsonDocument` (`AllowTrailingCommas = true`, `CommentHandling = JsonCommentHandling.Skip`) e monta `CatalogoLido` com as regras do T018; cada item ignorado gera `ILogger.LogWarning` com a posição e o motivo
- [X] T025 [US1] Criar `src/Tcmrj.Siat.Infra/Repositories/CatalogoSistemasRepository.cs` e registrá-lo em `AddCatalogoSistemas` (`src/Tcmrj.Siat.Infra/Configurations/CatalogoSistemasConfiguration.cs`) com `AddHttpClient<ICatalogoSistemasRepository, CatalogoSistemasRepository>` e `Timeout` de `TempoLimiteSegundos` (`HttpClient` tipado, `IOptionsMonitor<CatalogoSistemasOptions>`, `TimeProvider`, `ILogger`): valida as opções antes de qualquer chamada; monta a URL da API com `Uri.EscapeDataString` em projeto e arquivo; envia `PRIVATE-TOKEN`; lê o corpo como UTF-8; passa por `LeitorSistemasDb` e `MontadorCatalogo`; nesta história, qualquer resposta não 200 ou exceção de rede vira `Erros.Catalogo.Indisponivel` (o mapeamento detalhado é o T041); nunca registra o token
- [X] T026 [P] [US1] Criar em `src/Tcmrj.Siat.App/Web/ViewModels/Sistemas/` `CatalogoVM.cs`, `SistemaVM.cs`, `InstanciaVM.cs` (com `EnderecoVM`) e `AmbienteVM` (sigla, nome, `DataAmb`, instâncias), conforme [data-model.md](./data-model.md#view-models-viewmodelssistemas), com os construtores `De(...)` a partir do domínio, `EhLink` por `StartsWith("http://"/"https://", OrdinalIgnoreCase)` e `TextoDeBusca` montado uma vez; `CatalogoVM` já prevê `Ignorados` e `Falha` (preenchidos na História 3)
- [X] T027 [US1] Criar `src/Tcmrj.Siat.App/Web/Services/Interfaces/ICatalogoSistemasService.cs` e `src/Tcmrj.Siat.App/Web/Services/CatalogoSistemasService.cs` (singleton, `ICatalogoSistemasRepository` resolvido por `IServiceScopeFactory`, `IOptionsMonitor<CatalogoSistemasOptions>`, `TimeProvider`): `ObterAsync(bool atualizar, CancellationToken)` devolve a última leitura válida enquanto `agora - LidoEm < MinutosDeCache` e lê de novo depois disso; nesta história, sem leitura única concorrente nem preservação em falha (T040)
- [X] T028 [US1] Criar `src/Tcmrj.Siat.App/Web/Features/Sistemas/ObterCatalogo.cs`: `Query(bool Atualizar)` → `Result<CatalogoVM, Erro>`; autoriza com `IAutorizacaoConsultaService.PodeAcessarModuloAsync(Modulo.Sistemas)` antes de chamar o serviço e devolve `Erros.Catalogo.AcessoNegado` sem ele (FR-003)
- [X] T029 [US1] Registrar em `src/Tcmrj.Siat.Web/Extensions/ServiceCollectionExtensions.cs`: `services.AddCatalogoSistemas(builder.Configuration)` e `services.AddSingleton<ICatalogoSistemasService, CatalogoSistemasService>()`
- [X] T030 [P] [US1] Criar `src/Tcmrj.Siat.Web/Components/Shared/CelulaAmbiente.razor` (parâmetros `Sistema`, `Ambiente` `AmbienteVM`) conforme o T022 e [contracts/rotas-ui.md](./contracts/rotas-ui.md#componente-celulaambiente): instâncias em `div.instancia > dl` com `dt` URL/Máquina/Caminho, máquinas e caminho em `.mono`, `BotaoCopiar` com `Rotulo` "máquina {M} de {SIGLA} do {Sistema}" e "caminho de {SIGLA} do {Sistema}", quebra com `<wbr />` intercalado por segmentos (sem `MarkupString` com dado da origem)
- [X] T031 [US1] Criar `src/Tcmrj.Siat.Web/Components/Pages/Catalogo/Sistemas.razor` (`@page "/catalogo/sistemas"`, `[Authorize]`): no `OnInitializedAsync`, sem `PodeAcessarModuloAsync(Modulo.Sistemas)` mostra `AcessoNegado` com `Recurso="Sistemas.CanSelect"` e não chama a feature; com acesso, chama `ObterCatalogo` mostrando `EstadoCarregando` enquanto lê e `EstadoErro` com a mensagem em caso de erro; `CabecalhoPagina` "Sistemas" com a descrição de [contracts/rotas-ui.md](./contracts/rotas-ui.md#tela-catalogosistemasrazor); `FiltroLocal<SistemaVM>` com `TextoDe = s => s.TextoDeBusca`, `Unidade = "sistemas"`, `Rotulo = "Buscar sistemas"`, `Id = "busca-sistemas"`; tabela `.tabela.tabela-sistemas` dentro de `.tabela-rolagem` com cabeçalhos `scope="col"` Sistema, DSV/HML/PRD/CONT (`data-amb` e `span.amb` com a sigla, mais o nome) e Observação; uma linha por sistema com `td.nome`, quatro `CelulaAmbiente` e a observação (`.vazio-cel` quando nula)
- [X] T032 [P] [US1] Em `src/Tcmrj.Siat.Web/wwwroot/css/siat.css`, acrescentar o que falta da tela de `design/sistemas.html`: `.tabela-sistemas` (`table-layout: fixed`, `min-width: 1080px`, coluna Sistema 15%, Observação 14%), `.tabela-sistemas th .amb { margin-right: 6px; }`, cabeçalho fixo ao rolar e `.tabela-sistemas .obs { white-space: pre-line; }`; conferir nos temas claro e escuro

**Ponto de controle**: T017 a T022 passam; a tela lista os sistemas do GitLab com busca, links e cópia (quickstart, seções 1 e 2).

---

## Fase 4: História 2 — Compartilhar e navegar pelo teclado (Prioridade: P2)

**Objetivo**: busca na URL, estado vazio e uso completo por teclado.

**Teste independente**: buscar, copiar a URL e abri-la em outra janela; repetir só com teclado (quickstart, seção 3).

### Testes da História 2 ⚠️

- [X] T033 [P] [US2] Em `src/Tcmrj.Siat.Test/Componentes/FiltroLocalComponentTests.cs`, testar o novo parâmetro `Placeholder`: quando informado, vai para o `placeholder` do campo e o `Rotulo` continua no texto para leitor de tela; quando omitido, o `placeholder` continua sendo o `Rotulo` (comportamento atual)

### Implementação da História 2

- [X] T034 [US2] Em `src/Tcmrj.Siat.Web/Components/Shared/FiltroLocal.razor`, acrescentar o parâmetro opcional `Placeholder` (padrão: `Rotulo`), sem mudar o uso nas telas de Consultas
- [X] T035 [US2] Em `src/Tcmrj.Siat.Web/Components/Pages/Catalogo/Sistemas.razor`: ler a busca com `[SupplyParameterFromQuery(Name = Navegacao.ParametroBusca)]` e aplicá-la ao abrir; usar `@bind-Texto` do `FiltroLocal` e, a cada mudança, `Navigation.NavigateTo(Navegacao.Sistemas(texto), replace: true)` (o `Esc` do componente limpa o texto e, com isso, a URL); `Placeholder = "Sistema, máquina, URL ou caminho"`
- [X] T036 [US2] Em `src/Tcmrj.Siat.Web/Components/Pages/Catalogo/Sistemas.razor`, quando nenhum sistema casar com a busca, esconder a tabela e mostrar `EstadoVazio` com o título "Nenhum sistema encontrado", o texto "A busca procura todos os termos em nome, URL, máquina, caminho e observação." e a ação "Limpar busca", que limpa o texto e a URL e devolve o foco ao campo `busca-sistemas` (se o `siat.js` não tiver função para focar por id, acrescentar `focar(id)` em `src/Tcmrj.Siat.Web/wwwroot/js/siat.js` e exportá-la)
- [X] T037 [US2] Revisar `src/Tcmrj.Siat.Web/Components/Pages/Catalogo/Sistemas.razor` e `src/Tcmrj.Siat.Web/Components/Shared/CelulaAmbiente.razor` contra a WCAG 2.2 AA: ordem de tabulação busca → links e botões de cópia linha a linha; foco visível; nomes acessíveis dos botões com sistema e ambiente; contador anunciado (`aria-live` do `FiltroLocal`); contraste das etiquetas de ambiente nos dois temas; todos os textos, nomes acessíveis e mensagens em português do Brasil, sem travessões (FR-028, `DESIGN.md`); corrigir o que faltar

**Ponto de controle**: T033 passa; a busca sobrevive ao link compartilhado e a tela é usável só por teclado.

---

## Fase 5: História 3 — Ver o catálogo sempre atual e saber de onde ele vem (Prioridade: P2) 🎯 MVP

**Objetivo**: "Atualizar agora", Cancelar com interrupção na origem, leitura única concorrente, leitura anterior preservada em falha, mensagens de erro por causa, avisos e rodapé.

**Teste independente**: alterar um dado na origem e atualizar; tornar a origem indisponível e conferir a mensagem e os outros módulos (quickstart, seção 4).

### Testes da História 3 ⚠️

- [X] T038 [P] [US3] Ampliar `src/Tcmrj.Siat.Test/Unidade/Catalogo/CatalogoSistemasServiceTests.cs`: `atualizar = true` lê de novo mesmo dentro da validade; dois pedidos simultâneos (repositório falso preso num `TaskCompletionSource`) fazem uma única leitura e recebem o mesmo resultado; falha com leitura anterior devolve a anterior com `Falha` (erro e horário); o pedido seguinte a essa falha tenta ler de novo mesmo dentro da validade; sucesso depois de falha limpa `Falha`; cancelar o `CancellationToken` de um dos dois pedidos devolve `Erros.Catalogo.Cancelada` só para ele e não cancela a leitura para o outro; quando o único pedido (ou o último que restava) cancela, o `CancellationToken` recebido pelo repositório falso é cancelado (a leitura é interrompida na origem); leitura cancelada não muda `ultimaValida` nem cria `Falha`, e o pedido seguinte inicia uma leitura nova
- [X] T039 [P] [US3] Ampliar `src/Tcmrj.Siat.Test/Unidade/Catalogo/CatalogoSistemasRepositoryTests.cs` com o mapeamento de [research.md R6](./research.md#r6-erros-e-mensagens): 401 → `AcessoRecusado`; 403 e 404 → `ArquivoNaoEncontrado`; 500 e `HttpRequestException` → `Indisponivel`; `TaskCanceledException` por tempo limite (sem cancelamento pedido) → `TempoEsgotado` com os segundos configurados; cancelamento pedido pelo `CancellationToken` do chamador → `Cancelada`, sem exceção; arquivo malformado → `FormatoInvalido` com linha e coluna; em todos, a mensagem não contém o token configurado
- [X] T040 [P] [US3] Ampliar `src/Tcmrj.Siat.Test/Unidade/Catalogo/ObterCatalogoHandlerTests.cs`: `Query(Atualizar: true)` chega ao serviço como releitura forçada; `Ignorados` e `Falha` (mensagem e horário) chegam ao `CatalogoVM`

### Implementação da História 3

- [X] T041 [US3] Em `src/Tcmrj.Siat.Infra/Repositories/CatalogoSistemasRepository.cs`, implementar o mapeamento de erros do T039, distinguindo o tempo limite do `HttpClient` de um cancelamento pedido pelo chamador: no padrão do `MailQueueRepository` (`catch (Exception ex) when (cancellationToken.IsCancellationRequested && ex is OperationCanceledException)`), o cancelamento pedido devolve `Erros.Catalogo.Cancelada` em vez de deixar a exceção subir; `TaskCanceledException` sem cancelamento pedido é o tempo limite e devolve `TempoEsgotado`
- [X] T042 [US3] Em `src/Tcmrj.Siat.App/Web/Services/CatalogoSistemasService.cs`, implementar a máquina de estados de [data-model.md](./data-model.md#estado-do-catalogosistemasservice-singleton): `SemaphoreSlim` guardando a criação de uma única `Task` de leitura compartilhada, executada com o token de um `CancellationTokenSource` próprio da leitura; contagem de quem espera: cada pedido entra na contagem e espera com `WaitAsync(cancellationToken)`; ao ser cancelado, sai da contagem e, se ela chegar a zero, cancela o `CancellationTokenSource` da leitura (interrompendo a requisição ao GitLab, Princípio VII) e devolve `Erros.Catalogo.Cancelada`; leitura cancelada não muda `ultimaValida` nem `ultimaFalha` ([research.md R4](./research.md#r4-cache-e-atualizar-agora)); `ultimaValida` preservada em falha com `ultimaFalha` (erro e horário do `TimeProvider`); nova tentativa no pedido seguinte a uma falha; `ultimaFalha` limpa no sucesso
- [X] T043 [US3] Em `src/Tcmrj.Siat.App/Web/Features/Sistemas/ObterCatalogo.cs` e nos view models, levar `Atualizar`, `Ignorados` e `Falha` ao `CatalogoVM`
- [X] T044 [US3] Em `src/Tcmrj.Siat.Web/Components/Pages/Catalogo/Sistemas.razor`: nas ações do `CabecalhoPagina`, "Catálogo lido às HH:mm" (horário local, pt-BR) e o botão "Atualizar agora" (`Query(Atualizar: true)`, desabilitado durante a leitura, chamando `IInteracao.Interagir()` como as consultas); `Aviso` de alerta com "Não foi possível atualizar o catálogo: {mensagem}. Mostrando os dados lidos às HH:mm." quando houver `Falha`; `Aviso` "1 item do sistemasDb.js foi ignorado por estar incompleto." ou "N itens do sistemasDb.js foram ignorados por estarem incompletos." quando `Ignorados > 0`; `EstadoErro` com a ação "Tentar de novo" (releitura forçada); durante qualquer leitura, `EstadoCarregando` com `Inicio` e `AoCancelar` (como em `Consultas/AuditLog.razor`: esqueleto de 6 linhas sem catálogo, nenhum numa releitura com a tabela anterior visível), em que Cancelar cancela o `CancellationTokenSource` da página e volta ao catálogo anterior ou, sem ele, ao `EstadoErro` com "Tentar de novo"; o `CancellationTokenSource` também é cancelado no `Dispose` da página; rodapé "Fonte: sistemasDb.js no TCMRJDoc (GitLab). Para corrigir um dado, edite esse arquivo." com link para `Origem` em nova aba

**Ponto de controle**: T038 a T040 passam; a seção 4 do quickstart funciona, inclusive com token inválido e com o GitLab fora.

---

## Fase 6: História 4 — Acesso controlado ao catálogo (Prioridade: P3)

**Objetivo**: página inicial, acesso negado e garantia de que nada do catálogo sai sem o recurso.

**Teste independente**: com um usuário sem `Sistemas.CanSelect`, conferir o menu e a URL direta (quickstart, seção 5).

### Testes da História 4 ⚠️

- [X] T045 [P] [US4] Criar `src/Tcmrj.Siat.Test/Componentes/HomeTests.cs` (bUnit, `IAutorizacaoConsultaService` com Moq): com `Sistemas` permitido navega para `catalogo/sistemas` com `replace`; só com `MailQueue` navega para `consultas/mailqueue`; sem nenhum módulo mostra o texto de sem acesso citando o catálogo e as consultas
- [X] T046 [P] [US4] Criar `src/Tcmrj.Siat.Test/Componentes/SistemasPaginaTests.cs` (bUnit): sem `Sistemas.CanSelect`, a página mostra o acesso negado com `Sistemas.CanSelect`, o `IMediator` falso nunca é chamado e nenhum nome de sistema aparece no markup, inclusive com `?busca=etcm` na URL

### Implementação da História 4

- [X] T047 [US4] Em `src/Tcmrj.Siat.Web/Components/Pages/Home.razor`, pôr `(Modulo.Sistemas, Navegacao.RotaSistemas)` em primeiro lugar em `Ferramentas`, atualizar o comentário e trocar o texto de sem acesso para "Para usar o SIAT, peça acesso no SIA ao grupo Catalogo (catálogo de sistemas) ou ao grupo Consultas (MailQueue e AuditLog). Depois de liberado, recarregue a página."
- [X] T048 [US4] Em `src/Tcmrj.Siat.Web/Components/Pages/AcessoNegado.razor`, trocar o texto padrão (sem `Recurso`) para não citar só o grupo Consultas ("Peça acesso ao SIAT no SIA.") e atualizar o comentário do componente

**Ponto de controle**: T045 e T046 passam; a seção 5 do quickstart confere.

---

## Fase 7: Acabamento e pontos transversais

- [X] T049 [P] Atualizar `README.md` com o módulo Catálogo › Sistemas: origem (`sistemasDb.js` do TCMRJDoc), configuração `CatalogoSistemas`, onde gravar o token (`CatalogoSistemas:GitLab:Token` no `appsettings`, em desenvolvimento e no IIS), renovação anual do token e o script `003` do `dbchange`
- [X] T050 [P] Atualizar `PRD.md`: SI-04 passa a dizer que o catálogo é lido do `sistemasDb.js` do TCMRJDoc no GitLab enquanto o `sistemas.html` estiver em uso, e que a migração para `sistemas.json` fica para a aposentadoria dele; e, por causa da emenda 3.0.0 da constituição, trocar "recursos separados para PRD" na mitigação do risco da conta de serviço e "usuário com o recurso PRD" na métrica de acesso indevido pelo recurso do módulo
- [X] T051 Rodar `dotnet build src/Siat.slnx` sem avisos novos e `dotnet test --solution src/Siat.slnx` com todos os testes verdes
- [ ] T052 Executar as seções 1 a 5 do [quickstart.md](./quickstart.md) em desenvolvimento, com o token real, medindo a abertura da tela com cache quente (até 2 s, SC-003); registrar o resultado no fim do quickstart
- [X] T053 Comparar no navegador o SIAT com `design/sistemas.html` nos temas claro e escuro e em largura abaixo de 768px (rolagem horizontal da tabela, cabeçalho fixo) e corrigir divergências em `src/Tcmrj.Siat.Web/wwwroot/css/siat.css`

---

## Dependências e ordem de execução

### Entre fases

- **Preparação (Fase 1)**: começa logo.
- **Base (Fase 2)**: depende da Fase 1 e **bloqueia todas as histórias**.
- **História 1 (Fase 3)**: depende só da Base. Faz parte do MVP.
- **História 2 (Fase 4)**: depende da página da História 1 (T031); acrescenta URL, estado vazio e revisão de acessibilidade. T033–T035 (busca na URL) entram no MVP por exigência do Princípio VI.
- **História 3 (Fase 5)**: depende do repositório, do serviço e da página da História 1 (T025, T027, T031); faz parte do MVP ("Atualizar agora" é exigência do Princípio I, e o Cancelar, do Princípio VII); pode correr junto com a História 2, mas as duas editam `Sistemas.razor` (T035, T036, T044): quem chegar depois integra.
- **História 4 (Fase 6)**: os testes e o `Home`/`AcessoNegado` dependem só da Base; o T046 depende da página (T031). T045 e T047 (página inicial) entram no MVP.
- **Acabamento (Fase 7)**: depende das histórias entregues.

### Dentro da Base

- T009–T013 (Domain) antes de T014 (Infra).
- T016 depende de T009 e T015.

### Dentro de cada história

- Testes primeiro (devem falhar), depois Infra (leitor, montador, repositório), App (view models, serviço, feature), registro, componentes e página.
- Na História 1: T023 → T024 → T025; T026 antes de T028; T027 antes de T028; T028 e T030 antes de T031.

---

## Exemplos de paralelismo

### Preparação e Base

```text
T002 appsettings | T003 dbchange | T004 amostra
T005 Permissoes.feature | T006 Navegacao | T007 Menu | T008 Options
T009 Modulo | T010 AmbienteDoCatalogo | T011 modelos | T012 Erros | T013 repositório (interface) | T015 Navegacao
```

### História 1

```text
Testes:  T017 leitor | T018 montador | T019 repositório | T020 serviço | T021 feature | T022 CelulaAmbiente
Depois:  T023 → T024 → T025 (Infra)  ||  T026 view models  ||  T030 CelulaAmbiente  ||  T032 CSS
Por fim: T027 → T028 → T029 → T031
```

### História 3

```text
T038 serviço | T039 repositório | T040 feature
T041 repositório  ||  T042 serviço  →  T043  →  T044
```

### História 4

```text
T045 Home | T046 página   →   T047 Home | T048 AcessoNegado
```

---

## Estratégia de implementação

### MVP primeiro (Histórias 1 e 3)

1. Fases 1 e 2.
2. Fase 3 (História 1).
3. Fase 5 (História 3): "Atualizar agora", Cancelar, falhas, avisos e rodapé.
4. Da Fase 4, só T033–T035 (busca na URL): sem elas, o MVP descumpriria o Princípio VI ("Filtros e seleção DEVEM ser refletidos na URL").
5. Da Fase 6, T045 e T047 (página inicial): sem elas, quem está só no grupo `Catalogo` veria o menu com Catálogo e, na página inicial, "Você não tem acesso às ferramentas do SIAT".
6. **Parar e validar**: quickstart seções 1, 2 e 4, a primeira linha da seção 3 (link compartilhado com a busca) e a primeira linha da seção 5 (página inicial), com o `sistemas.html` lado a lado.
7. Publicar em DSV para o suporte experimentar.

### Entrega incremental

1. Base + Histórias 1 e 3 + busca na URL (T033–T035) + página inicial (T045, T047) → MVP.
2. Resto da História 2 (T036, T037) → estado vazio e revisão de acessibilidade.
3. Resto da História 4 (T046, T048) → teste de acesso direto pela URL e texto do acesso negado.
4. Acabamento, quickstart completo e publicação.

### Em equipe

- Uma pessoa no Infra (T023–T025, T041), outra no App (T026–T028, T042), outra no Web (T030–T032).
- Na Fase 5, combinar quem integra `Sistemas.razor` com a Fase 4.

---

## Observações

- Fora do SIAT e desta lista: criar o token de projeto no TCMRJDoc (Reporter, `read_repository`), conferir que os servidores do IIS alcançam `https://git.tcmnet.tcm` (o endereço já foi confirmado de uma estação) e sugerir ao TCMRJDoc o commit que corrige as barras perdidas ([contracts/origem-sistemasdb.md](./contracts/origem-sistemasdb.md#valores-do-arquivo-atual-que-perdem-a-barra)).
- O `sistemas.html` continua em uso até a validação; nenhuma tarefa altera o `sistemasDb.js`.
- Não copiar o `sistemasDb.js` inteiro para o repositório do SIAT: a amostra do T004 basta para os testes, e a conferência completa é manual (quickstart, seção 2).
- Commit ao fim de cada tarefa ou grupo lógico, no branch `features/<tarefa>_catalogo_sistemas`.
