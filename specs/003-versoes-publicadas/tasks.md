---

description: "Lista de tarefas da feature 003 — Versões Publicadas"
---

# Tarefas: Versões Publicadas

**Entrada**: documentos de design em `specs/003-versoes-publicadas/`

**Pré-requisitos**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Testes**: incluídos. A constituição (Princípio V) exige testes automatizados das regras ("interpretação de versões" é citada nominalmente) e testes de componente para componentes com lógica; os fluxos das histórias ficam no roteiro manual do [quickstart](./quickstart.md). Os testes de cada história são escritos antes da implementação e devem falhar primeiro.

**Organização**: tarefas agrupadas por história da [spec.md](./spec.md), para que cada uma seja implementada, testada e entregue de forma independente.

## Formato: `[ID] [P?] [História] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem depender de tarefa incompleta)
- **[US1]…[US4]**: história da spec a que a tarefa pertence
- Todo caminho é relativo à raiz do repositório (`D:\Projetos\siat`)

## Convenções de caminho

Solução em `src/Siat.slnx`, com `Tcmrj.Siat.Domain`, `Tcmrj.Siat.Infra`, `Tcmrj.Siat.App`, `Tcmrj.Siat.Web` e `Tcmrj.Siat.Test`. Siga os padrões existentes: `Result<T, Erro>` (`Tcmrj.CSharpFunctionalExtensions`); features com `Query` + `Handler` do `NetDevPack.SimpleMediator` que autorizam pelo servidor (exemplo: `src/Tcmrj.Siat.App/Web/Features/AuditLog/ListarAplicacoes.cs`); repositório SQL com SQL embutido e mapeamento de erros (exemplo: `src/Tcmrj.Siat.Infra/Repositories/AuditLogRepository.cs`); cache e cancelamento (exemplo: `src/Tcmrj.Siat.App/Web/Services/CatalogoSistemasService.cs`); página com seletor de servidor e parâmetros na URL (exemplo: `src/Tcmrj.Siat.Web/Components/Pages/Consultas/AuditLog.razor`); página com cache, "Atualizar agora" e Cancelar (exemplo: `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Catalogo.razor`); testes bUnit herdando de `BunitContext` (`src/Tcmrj.Siat.Test/Componentes/CatalogoPaginaTests.cs`).

Referências: contrato visual `design/versoes.html`, tokens e classes em `src/Tcmrj.Siat.Web/wwwroot/css/siat.css`, comportamento de referência `D:\Projetos\Geral\Ferramentas\VersaoDeSistemasPublicados.ps1`, tabela real em `TCMBD3\DEV` (só leitura, [database.instructions.md](../../.github/instructions/database.instructions.md)).

---

## Fase 1: Preparação

**Objetivo**: configuração e conferência do recurso no UserManager.

- [X] T001 Conferir em `CSLIB_UserManager` de `TCMBD3\DEV` que a aplicação `SIAT` tem o recurso `Id = 40`, `ResourceKey = 'Versoes_Sistemas'` e o acesso `CanSelect = 1` do grupo `Sistemas` (script `dbchange/196.1.1/001 - TCMRio - CSLIB_UserManager - Criação de app, recursos e grupos do SIAT.sql`, [contracts/usermanager.md](./contracts/usermanager.md)); se faltar, executar o script 001 (idempotente), sem criar script novo
- [X] T002 [P] Acrescentar a seção `Versoes` a `src/Tcmrj.Siat.Web/appsettings.json`: `ServidorPadrao` `tcmbd3-dev`, `TempoLimiteSegundos` 5, `LeiturasSimultaneas` 32, `MinutosDeCache` 5 ([contracts/configuracao.md](./contracts/configuracao.md))
- [X] T003 [P] Conferir com `sqlcmd` (só leitura) que a conta de desenvolvimento tem `SELECT` em `dbo.Apps_VersionAddresses` de `TCMBD3\DEV` e anotar no PR quantos registros ativos existem (42 em 2026-11-24)

---

## Fase 2: Base (bloqueia todas as histórias)

**Objetivo**: módulo, rota, menu, erros, opções, modelo do domínio e o cache genérico extraído do catálogo.

**⚠️ Nenhuma história começa antes desta fase.**

### Testes da Base ⚠️

- [X] T004 [P] Acrescentar a linha `| Versoes_Sistemas | Versoes_Sistemas.CanSelect | Versoes_Sistemas |` aos exemplos de `src/Tcmrj.Siat.Test/Features/Permissoes.feature` e citar "Versões" no texto da funcionalidade
- [X] T005 [P] Em `src/Tcmrj.Siat.Test/Unidade/NavegacaoTests.cs`, testar `Navegacao.Versoes()` → `sistemas/versoes`; `Navegacao.Versoes("tcmbd4-hml", "etcm", FiltroDeSituacao.ComDivergencia)` → `sistemas/versoes?servidor=tcmbd4-hml&busca=etcm&filtro=div`; `FiltroDeSituacao.ComErro` → `filtro=erro`; `FiltroDeSituacao.Todos`, busca nula/vazia/só espaços e servidor nulo ficam fora da URL; e `Navegacao.LerFiltro` com `div`, `erro`, nulo e `xyz` (→ `Todos`)
- [X] T006 [P] Em `src/Tcmrj.Siat.Test/Componentes/MenuTests.cs`, testar: com `Versoes_Sistemas.CanSelect` o grupo "Sistemas" mostra o item "Versões" apontando para `sistemas/versoes`, depois de "Catálogo" quando os dois aparecem; só com `Versoes_Sistemas.CanSelect` o grupo "Sistemas" aparece só com "Versões"; sem os dois recursos o grupo não aparece; os testes existentes continuam passando
- [X] T007 [P] Criar `src/Tcmrj.Siat.Test/Unidade/LeituraEmCacheTests.cs` para `LeituraEmCache<TChave, TValor>` ([research.md R5](./research.md#r5-cache-leitura-única-e-cancelamento), estados em [data-model.md](./data-model.md#estado-do-cache-app-leituraemcachestring-versoeslidas)) com `FakeTimeProvider` e função de leitura falsa controlada por `TaskCompletionSource`: primeiro pedido lê; outro pedido dentro da validade não lê; depois da validade lê; "atualizar" lê mesmo dentro da validade; dois pedidos simultâneos geram uma leitura só; falha sem anterior devolve o erro; falha com anterior devolve o anterior com a falha (erro e horário) e o pedido seguinte tenta de novo; sucesso limpa a falha; pedido cancelado recebe o erro de cancelamento e, sendo o único, cancela o token da leitura; com dois esperando, cancelar um não cancela a leitura; leitura cancelada não muda o estado; **chaves diferentes não compartilham leitura, validade nem falha**
- [X] T008 [P] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/VersoesOptionsTests.cs`: válido com os valores do T002; inválido, com motivo em português, para `TempoLimiteSegundos` fora de "1 a 60", `LeiturasSimultaneas` fora de "1 a 128", `MinutosDeCache` fora de "1 a 60" e `ServidorPadrao` vazio; e que `builder.AddTcmrjSiat()` sem a seção `Versoes` monta o `ServiceProvider` e resolve `IVersoesPublicadasService` com os padrões (`tcmbd-prd`, 5, 32, 5) — o SIAT sobe sem a seção (Princípio IV)

### Implementação da Base

- [X] T009 [P] Acrescentar `Versoes_Sistemas` a `src/Tcmrj.Siat.Domain/Enums/Modulo.cs` (depois de `Catalogo_Sistemas`); `ModuloExtensions.Politica()` já produz `Versoes_Sistemas.CanSelect`
- [X] T010 [P] Acrescentar `Erros.Versoes` a `src/Tcmrj.Siat.Domain/Models/Erros.cs`, com constantes `Codigo*` no padrão `versoes.*` e as mensagens de [data-model.md](./data-model.md#erros-errosversoes) e [contracts/origem-enderecos.md](./contracts/origem-enderecos.md#tabela-cslib_usermanagerdboapps_versionaddresses): `AcessoNegado` ("Você não tem acesso às versões publicadas."), `TabelaIndisponivel(servidor, motivo)`, `TempoEsgotado(servidor)`, `SemSistemasAtivos(servidor)` e `Cancelada` ("A leitura das versões foi cancelada.")
- [X] T011 [P] Acrescentar a `src/Tcmrj.Siat.Domain/Models/Servidor.cs` `BancoUserManagerPadrao = "CSLIB_UserManager"` e a propriedade `BancoUserManager` (padrão `BancoUserManagerPadrao`), como `BancoMailQueue` e `BancoAuditoria`; conferir que `ServidoresOptions` aceita a chave opcional no `servidores.json` sem exigi-la
- [X] T012 [P] Criar em `src/Tcmrj.Siat.Domain/Models/Versoes/`: `Maquina.cs` (enum `Maquina1`, `Maquina2`), `SituacaoDaCelula.cs` (enum `Versao`, `Erro`, `NaoConfigurado`), `EnderecoDeVersao.cs` (`Id` int, `Sistema` "sem espaços nas pontas", `Enderecos` mapa `(Ambiente, Maquina)` → `ParDeEndereco(Versao, SistemaRaiz)` "seis entradas, sempre presentes", com `NULL` ou só espaços → `null`), `LeituraDeCelula.cs` (`Ambiente`, `Maquina`, `Situacao`, `Versao` "só com `Situacao = Versao`", `Caminho`, `CaminhoDoRaiz`, `Mensagem` "só com `Situacao = Erro`", com fábricas `ComVersao`, `ComErro`, `NaoConfigurada`), `SistemaVersionado.cs` e `VersoesLidas.cs` conforme [data-model.md](./data-model.md) (as regras de divergência entram na História 1)
- [X] T013 [P] Criar `src/Tcmrj.Siat.Domain/Repositories/IEnderecosDeVersaoRepository.cs` (`Task<Result<IReadOnlyList<EnderecoDeVersao>, Erro>> ListarAtivosAsync(Servidor servidor, CancellationToken cancellationToken)`) e `src/Tcmrj.Siat.Domain/Repositories/ILeitorDeArquivoDeVersao.cs` (`Task<Result<string, string>> LerAsync(string endereco, CancellationToken cancellationToken)`, em que o erro é a mensagem da célula)
- [X] T014 Criar `src/Tcmrj.Siat.Infra/Configurations/VersoesOptions.cs` (seção `Versoes`; `ServidorPadrao` padrão `tcmbd-prd`, `TempoLimiteSegundos` 5, `LeiturasSimultaneas` 32, `MinutosDeCache` 5; `Validar()` com os motivos do T008) e `src/Tcmrj.Siat.Infra/Configurations/VersoesConfiguration.cs` com `AddVersoes(IConfiguration)`, que por ora só faz `Configure<VersoesOptions>` (repositório e `HttpClient` entram na História 1). No App, criar `src/Tcmrj.Siat.App/Web/Services/CacheDeVersoesOptions.cs` (mesma seção, só `MinutosDeCache` e `ServidorPadrao`, como `CacheDoCatalogoOptions`, porque o App não depende do Infra)
- [X] T015 Extrair de `src/Tcmrj.Siat.App/Web/Services/CatalogoSistemasService.cs` a classe `src/Tcmrj.Siat.App/Web/Services/LeituraEmCache.cs` (`LeituraEmCache<TChave, TValor> where TChave : notnull`): construtor com a função `Func<TChave, CancellationToken, Task<Result<TValor, Erro>>>`, a função que dá o horário de um valor (`Func<TValor, DateTimeOffset>`), a validade (`Func<TimeSpan>`), o erro de cancelamento, o código de cancelamento, `TimeProvider` e `ILogger`; uma entrada por chave num `Dictionary` sob uma única `Lock`; método `ObterAsync(TChave chave, bool atualizar, CancellationToken)` → `Result<(TValor Valor, FalhaDeLeitura? Falha), Erro>`, com a mesma lógica de contagem de quem espera, cancelamento e preservação do anterior. Comentário XML citando a 002 (R4) e esta feature (R5). T007 passa
- [X] T016 Reescrever `src/Tcmrj.Siat.App/Web/Services/CatalogoSistemasService.cs` para usar `LeituraEmCache<Unit, CatalogoLido>` (chave única), mantendo a interface `ICatalogoSistemasService` e o comportamento; os testes existentes em `src/Tcmrj.Siat.Test/Unidade/Catalogo/CatalogoSistemasServiceTests.cs` e `src/Tcmrj.Siat.Test/Componentes/CatalogoPaginaTests.cs` continuam passando sem alteração de comportamento (só ajuste de construção, se preciso); casos que passaram a ser genéricos podem ser removidos daqui por já estarem no T007
- [X] T017 [P] Em `src/Tcmrj.Siat.App/Web/Navegacao.cs`, acrescentar `RotaVersoes = "sistemas/versoes"`, `ParametroFiltro = "filtro"`, o enum `FiltroDeSituacao` (`Todos`, `ComDivergencia`, `ComErro`; no App, por ser da tela), `Versoes(string? servidor = null, string? busca = null, FiltroDeSituacao filtro = FiltroDeSituacao.Todos)` e `LerFiltro(string?)` (`div`, `erro`, qualquer outro → `Todos`), usando o `Montar` existente. T005 passa
- [X] T018 Em `src/Tcmrj.Siat.Web/Components/Layout/Menu.razor`, acrescentar ao grupo "Sistemas", depois de "Catálogo", o item `new(Modulo.Versoes_Sistemas, "Versões", Navegacao.RotaVersoes, "git-compare")` e atualizar o comentário do topo. T006 passa

**Ponto de controle**: T004–T008 passam (a parte de subida do T008 só passa depois do T035), os testes do Catálogo continuam verdes e o menu mostra **Sistemas › Versões** para quem tem o recurso (a rota ainda não existe).

---

## Fase 3: História 1 — Saber qual versão está publicada em cada ambiente (Prioridade: P1) 🎯 MVP

**Objetivo**: ler a tabela do servidor padrão e os arquivos de versão, e mostrar a tabela com as quatro situações de célula, a linha expansível com o caminho consultado e a cópia.

**Teste independente**: com um usuário que tem só `Versoes_Sistemas.CanSelect`, abrir **Sistemas › Versões** e conferir as versões com o script rodado contra o mesmo servidor (quickstart, seções 1 e 2).

### Testes da História 1 ⚠️

- [X] T019 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/InterpretadorDeVersaoTests.cs` para `InterpretadorDeVersao` ([research.md R2](./research.md#r2-interpretação-do-conteúdo-regras-do-script)), um caso por linha de `[Theory]`: `"1.4.2"` → `1.4.2`; `"1.4.2\r\n"` e `"  1.4.2  "` → `1.4.2`; `""`, `"   "`, `"\r\n"` → vazio; texto de até 12 caracteres sem `VERSAO=` → o próprio texto (`"versão nova"`); `.INI` com várias linhas e `VERSAO=5.11.3` → `5.11.3`; `versao=5.11.3` (minúsculo) → `5.11.3`; primeiro `VERSAO=` quando há dois; texto com mais de 12 caracteres sem `VERSAO=` → vazio; `VERSAO=5.11` (só dois números) → vazio; linhas unidas por espaço antes da contagem de 12 caracteres (`"1.4\n2.0.0.0.0.0"` tem 14). `MontarEndereco(@"\\unity\x\v.{0}\A\VERSAO.TXT", "5.11.3")` → `\\unity\x\v.5.11.3\A\VERSAO.TXT`; com raiz vazia → `v.`; endereço sem `{0}` não muda; endereço com `{1}` ou chaves soltas não lança exceção
- [X] T020 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/SistemaVersionadoTests.cs`: `DivergeEm` verdadeiro só com as duas células em `Versao` e versões diferentes (ordinal); falso com uma em `Erro` ou `NaoConfigurado`, com uma só máquina configurada e com versões iguais; `TemDivergencia`, `TemErro`, `SemPendencias`; sistema com divergência em HML e erro em PRD conta nas duas situações; sistema com as seis células `NaoConfigurado` é `SemPendencias`
- [X] T021 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/ClassificadorDeEnderecoTests.cs` ([research.md R3](./research.md#r3-endereços-aceitos-e-segurança-da-leitura)): aceita `\\tcm2016web3\TcmRoot\DSV\Sites\sgq_dsv\VERSAO.TXT`, `\\unity\producao\prod\producao\VBSCP\SCP.INI`, `https://etcm.tcm.rj.gov.br/versao.txt` e `http://x/versao.txt`; recusa como "Endereço não suportado" `C:\inetpub\appsettings.json`, `G:\x\VERSAO.TXT`, `VERSAO.TXT`, `..\x`, `file://servidor/x`, `ftp://x`, `\\?\C:\x`, `\\.\pipe\x` e texto com caractere inválido de caminho
- [X] T022 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/LeitorDeArquivoDeVersaoTests.cs` para o leitor do Infra. Arquivo: o leitor separa a classificação (já testada no T021) da leitura num método `internal` que recebe o caminho já aceito (com `InternalsVisibleTo` para o projeto de teste), testado com uma pasta temporária local (`Path.GetTempPath()`), sem compartilhamento administrativo nem privilégio de administrador: arquivo existente → texto; inexistente → "Arquivo não encontrado."; pasta inexistente → "Caminho não encontrado."; arquivo maior que 64 KB → só os primeiros 64 KB; arquivo aberto para escrita por outro `FileStream` (`FileShare.ReadWrite`) ainda é lido; token já cancelado durante `ReadAsync` → `OperationCanceledException`. Com `HttpMessageHandler` falso (classe de teste): 200 → corpo; 404 → "O servidor respondeu 404."; `HttpRequestException` → "Não foi possível acessar o endereço: ..."; corpo maior que 64 KB truncado; a requisição não leva cabeçalho de autenticação
- [X] T023 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/EnderecosDeVersaoRepositoryTests.cs`: o SQL embutido `versoes.sql` existe, contém `WHERE Ativo = 1` e as 14 colunas de [contracts/origem-enderecos.md](./contracts/origem-enderecos.md); o mapeamento de um `DbDataReader` falso (ou `DataTable.CreateDataReader()`) põe cada coluna no par `(Ambiente, Maquina)` certo (`VersaoDSV` → DSV/Máquina 1, `SistemaRaizIniPRD2` → PRD/Máquina 2), com `DBNull` e só espaços → `null` e `Sistema` sem espaços nas pontas; e o mapeamento de `SqlException` por número (`-2` → `TempoEsgotado`; 208, 229, 230, 916, 927 → `TabelaIndisponivel` com "tabela inexistente ou sem permissão para a conta do SIAT"), reaproveitando o padrão de teste dos repositórios das Consultas
- [X] T024 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/LeitorDeVersoesTests.cs` para `LeitorDeVersoes` (App) com `ILeitorDeArquivoDeVersao` falso e `FakeTimeProvider`: seis células montadas na ordem DSV-1, DSV-2, HML-1, HML-2, PRD-1, PRD-2; endereço nulo → `NaoConfigurado` sem leitura; conteúdo vazio ou sem `VERSAO=` → `NaoConfigurado` com `Caminho` preenchido (esclarecimento de 2026-11-24); endereço repetido em várias células (ou com maiúsculas diferentes) lido uma vez; sistema raiz lido antes e uma vez para várias células, e o endereço lido é o montado com a versão do raiz; raiz com erro → célula `Erro` com "Versão do sistema raiz não obtida: ..." e `CaminhoDoRaiz`; endereço não suportado → `Erro` sem chamar o leitor; leitor que nunca responde → `Erro` "Tempo limite de 5 s esgotado ao ler o arquivo." depois de `TempoLimiteSegundos` e as outras células concluídas normalmente; nunca mais que `LeiturasSimultaneas` leituras ao mesmo tempo; cancelamento da leitura geral cancela o token passado ao leitor; o `Action<int>` de andamento é chamado uma vez, antes da primeira leitura de arquivo, com a quantidade de endereços distintos; `VersoesLidas` com `QuantidadeDeEnderecos` distintos, `Duracao` pelo relógio e sistemas em ordem pt-BR ("eTCM Impressão" antes de "INTERnet"), empate por `Id`
- [X] T025 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/ObterVersoesHandlerTests.cs`: sem `Versoes_Sistemas.CanSelect` → `Erros.Versoes.AcessoNegado` e o serviço não é chamado; chave de servidor nula → usa `ServidorPadrao`; chave inexistente → `Erros.Consultas.ServidorNaoEncontrado`; autorizado → `VersoesVM` com resumo (`Total`, `SemPendencias`, `ComDivergencia`, `ComErro`) coerente com os sistemas; `Atualizar` repassado ao serviço; cancelamento → `Erros.Versoes.Cancelada`
- [X] T026 [P] [US1] Criar `src/Tcmrj.Siat.Test/Componentes/CelulaVersaoTests.cs`: as quatro situações de [contracts/rotas-ui.md](./contracts/rotas-ui.md#celulaversao) com a classe do selo (`ok`, `divergente`, `erro`) e o texto para leitor de tela ("difere da outra máquina", "Erro ao ler a versão", "não configurado"); a versão é exibida como texto (`<b>1</b>` não vira marcação); a célula da máquina 1 tem a classe `divisa`
- [X] T027 [P] [US1] Criar `src/Tcmrj.Siat.Test/Componentes/VersoesPaginaTests.cs` (parte da História 1), no padrão de `CatalogoPaginaTests.cs`, com `IMediator` falso: tabela com cabeçalho de duas linhas (DSV, HML, PRD com `colspan=2`; Máquina 1 e Máquina 2); uma linha por sistema com seis `CelulaVersao`; expansor com `aria-expanded="false"` e `aria-label` "Caminhos consultados de {sistema}"; ao abrir, o detalhe mostra "{AMB} Máquina {n}", o caminho, o `BotaoCopiar` com o rótulo "Copiar caminho de {AMB}, máquina {n}, do {sistema}", o caminho do sistema raiz quando houver e a mensagem de erro; células sem endereço não aparecem no detalhe; caminho e mensagem de erro com `<b>x</b>` aparecem como texto, sem virar marcação (FR-021); status "Gerado às HH:mm em N,N s"; rodapé com o servidor e o tempo de cache
- [X] T028 [P] [US1] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/VersoesPublicadasServiceTests.cs` com repositório e leitor falsos e `FakeTimeProvider`: servidores diferentes têm resultados e validades separados; dentro de `MinutosDeCache` não relê; "Atualizar" relê; dois pedidos simultâneos para o mesmo servidor geram uma consulta à tabela; falha da tabela com resultado anterior devolve o anterior com a falha; erro de arquivo não é falha da leitura geral; cancelamento do último que espera cancela o token passado ao repositório e ao leitor; `QuantidadeEmLeitura(chave)` é nulo antes da consulta à tabela, vale a quantidade informada pelo `LeitorDeVersoes` durante a leitura daquele servidor (e só daquele) e volta a nulo ao fim, com sucesso, falha ou cancelamento

### Implementação da História 1

- [X] T029 [P] [US1] Criar `src/Tcmrj.Siat.Domain/Models/Versoes/InterpretadorDeVersao.cs` (estático e puro): `Interpretar(string? texto)` e `MontarEndereco(string endereco, string versaoRaiz)` com a regex `VERSAO=(\d+\.\d+\.\d+)` por `[GeneratedRegex]` sem diferenciar maiúsculas; comentário XML com as regras do script e as diferenças aceitas. T019 passa
- [X] T030 [P] [US1] Completar `src/Tcmrj.Siat.Domain/Models/Versoes/SistemaVersionado.cs` com `DivergeEm(Ambiente)`, `TemDivergencia`, `TemErro` e `SemPendencias`. T020 passa
- [X] T031 [P] [US1] Criar `src/Tcmrj.Siat.Domain/Models/Versoes/ClassificadorDeEndereco.cs` (puro, no Domain para o `LeitorDeVersoes` do App usá-lo sem depender do Infra; `Classificar(string)` → `Unc`, `Http` ou `NaoSuportado`). T021 passa
- [X] T032 [US1] Criar `src/Tcmrj.Siat.Infra/Versoes/LeitorDeArquivoDeVersao.cs` (`ILeitorDeArquivoDeVersao`): UNC com `FileStream` (`FileMode.Open`, `FileAccess.Read`, `FileShare.ReadWrite | FileShare.Delete`, `FileOptions.Asynchronous`), no máximo 64 KB, decodificação com detecção de BOM e UTF-8 como padrão; HTTP com o `HttpClient` tipado (sem credenciais, `MaxAutomaticRedirections = 3`), sucesso só em 2xx, no máximo 64 KB do corpo; mensagens da tabela de [contracts/origem-enderecos.md](./contracts/origem-enderecos.md#leitura-de-um-endereço-ileitordearquivodeversao); `OperationCanceledException` propagada (o tempo limite é aplicado pelo chamador). T022 passa
- [X] T033 [P] [US1] Criar `src/Tcmrj.Siat.Infra/Sql/versoes.sql` com o SQL de [contracts/origem-enderecos.md](./contracts/origem-enderecos.md) e marcá-lo como `EmbeddedResource` (conferir o padrão em `src/Tcmrj.Siat.Infra/Tcmrj.Siat.Infra.csproj`)
- [X] T034 [US1] Criar `src/Tcmrj.Siat.Infra/Repositories/EnderecosDeVersaoRepository.cs` (`IEnderecosDeVersaoRepository`): conexão pela `IFabricaDeConexao` com `servidor.BancoUserManager`, `CommandTimeout` das `ConsultasOptions`, `CancellationToken` repassado ao `ExecuteReaderAsync`, mapeamento das colunas e dos erros como no T023, no padrão de `AuditLogRepository`. T023 passa
- [X] T035 [US1] Completar `src/Tcmrj.Siat.Infra/Configurations/VersoesConfiguration.cs`: `AddHttpClient<ILeitorDeArquivoDeVersao, LeitorDeArquivoDeVersao>()` (handler com `AllowAutoRedirect`, `MaxAutomaticRedirections = 3`, `UseDefaultCredentials = false`, `Timeout = Timeout.InfiniteTimeSpan`, porque o tempo limite por arquivo é do `LeitorDeVersoes`) e `AddScoped<IEnderecosDeVersaoRepository, EnderecosDeVersaoRepository>()`; registrar `AddVersoes` em `src/Tcmrj.Siat.Web/Extensions/ServiceCollectionExtensions.cs`, junto de `AddCatalogoSistemas`, com `Configure<CacheDeVersoesOptions>`
- [X] T036 [US1] Criar `src/Tcmrj.Siat.App/Web/Services/LeitorDeVersoes.cs` ([research.md R4](./research.md#r4-paralelismo-tempo-limite-e-deduplicação)): recebe os `EnderecoDeVersao`, o `ILeitorDeArquivoDeVersao`, as opções e o `TimeProvider`; classifica cada endereço com o `ClassificadorDeEndereco` do Domain; deduplica sem diferenciar maiúsculas; lê os raízes e depois os demais com `Parallel.ForEachAsync` (`MaxDegreeOfParallelism = LeiturasSimultaneas`); cada leitura com `CancellationTokenSource.CreateLinkedTokenSource` + `CancelAfter` e `WaitAsync`; chama o `Action<int>` de andamento com a quantidade de endereços distintos antes da primeira leitura ([research.md R10](./research.md#r10-andamento-da-leitura)); monta `LeituraDeCelula` com o `InterpretadorDeVersao`; devolve `VersoesLidas` ordenado. Registra no `ILogger` (Information) servidor, endereços, erros e duração. T024 passa
- [X] T037 [US1] Criar `src/Tcmrj.Siat.App/Web/Services/Interfaces/IVersoesPublicadasService.cs` (`ObterAsync(Servidor servidor, bool atualizar, CancellationToken)` → `Result<VersoesObtidas, Erro>`, com `VersoesObtidas(VersoesLidas, FalhaDeLeitura?)`) e `src/Tcmrj.Siat.App/Web/Services/VersoesPublicadasService.cs` (singleton): `LeituraEmCache<string, VersoesLidas>` pela chave do servidor, cuja leitura abre um escopo, lista os endereços pelo repositório e chama o `LeitorDeVersoes`; validade `MinutosDeCache`; `QuantidadeEmLeitura(string chaveServidor)` → `int?` com um `ConcurrentDictionary<string, int>` preenchido pelo `Action<int>` do `LeitorDeVersoes` e limpo ao fim da leitura (R10); registrar o singleton em `ServiceCollectionExtensions.cs`. T028 e T008 passam por completo
- [X] T038 [P] [US1] Criar `src/Tcmrj.Siat.App/Web/ViewModels/Versoes/CelulaVersaoVM.cs`, `SistemaVersoesVM.cs` (com `NomeNormalizado` pela mesma normalização do `FiltroLocal`) e `VersoesVM.cs` com `De(VersoesObtidas, ServidorVM)` conforme [data-model.md](./data-model.md#view-models-app), incluindo o `RotuloAcessivel` da célula ("PRD, máquina 2: 7.23.9, difere da outra máquina")
- [X] T039 [US1] Criar `src/Tcmrj.Siat.App/Web/Features/Versoes/ObterVersoes.cs` (`Query(string? Servidor, bool Atualizar)` → `Result<VersoesVM, Erro>`): servidor nulo → `CacheDeVersoesOptions.ServidorPadrao`; `IAutorizacaoConsultaService.AutorizarAsync(Modulo.Versoes_Sistemas, chave)` antes de qualquer leitura; `OperationCanceledException` → `Erros.Versoes.Cancelada`; sem registro no DBLog (comentário citando [research.md R6](./research.md#r6-autorização-e-registro)). T025 passa
- [X] T040 [P] [US1] Criar `src/Tcmrj.Siat.Web/Components/Shared/CelulaVersao.razor` (parâmetros `Celula` e `Divergente`; `<td>` com selo, texto oculto para leitor de tela e classe `divisa` na máquina 1). T026 passa
- [X] T041 [US1] Criar `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Versoes.razor` (rota `/sistemas/versoes`, `[Authorize]`, `PageTitle` "Versões publicadas · SIAT"): verifica `PodeAcessarModuloAsync(Modulo.Versoes_Sistemas)` no `OnInitializedAsync` e, sem ele, mostra `AcessoNegado`; obtém `ObterVersoes` com o servidor padrão; `CabecalhoPagina`, `FaixaContexto`, tabela com cabeçalho fixo de duas linhas, linhas expansíveis com o detalhe e `BotaoCopiar`, legenda e rodapé de [contracts/rotas-ui.md](./contracts/rotas-ui.md#tela-sistemasversoesrazor); `EstadoCarregando` simples enquanto lê (o tempo decorrido e o Cancelar entram na História 3). T027 passa
- [X] T042 [P] [US1] Acrescentar a `src/Tcmrj.Siat.Web/wwwroot/css/siat.css` o que faltar para o contrato visual `design/versoes.html` (selos `ok`/`divergente`/`erro`, `divisa`, cabeçalho de duas linhas fixo, borda de cor por ambiente, detalhe da linha), reaproveitando os tokens existentes e conferindo contraste AA nos temas claro e escuro

**Ponto de controle**: T019–T028 passam; com o SIAT rodando, a tela mostra as versões de `TCMBD3\DEV` e confere com o script (quickstart, seções 1 e 2).

---

## Fase 4: História 2 — Encontrar pendências e compartilhar o resultado (Prioridade: P2)

**Objetivo**: resumo, filtros por situação, busca pelo nome, seletor de servidor, tudo na URL, e uso completo pelo teclado.

**Teste independente**: aplicar servidor, filtro e busca, copiar a URL, abri-la com outro usuário autorizado e ver o mesmo resultado; repetir só com teclado (quickstart, seção 3).

### Testes da História 2 ⚠️

- [X] T043 [P] [US2] Em `src/Tcmrj.Siat.Test/Componentes/VersoesPaginaTests.cs`, acrescentar: resumo `dl` com Sistemas, Sem pendências, Com divergência e Com erro; grupo "Filtrar por situação" com Todos, Com divergência e Com erro, cada um com a quantidade e `aria-pressed`; escolher "Com divergência" mostra só os sistemas com divergência e navega com `filtro=div` (`replace: true`); busca `etcm` (e `ETCM`, e `impressao` achando "eTCM Impressão") combinada com o filtro; sem resultado → "Nenhum sistema neste filtro" com o texto conforme filtro e busca e "Ver todos", que limpa os dois; abrir com `?busca=etcm&filtro=erro` restaura os dois; `?filtro=xyz` → Todos
- [X] T044 [P] [US2] Em `src/Tcmrj.Siat.Test/Componentes/VersoesPaginaTests.cs`, acrescentar o seletor: `SeletorServidor` com os grupos de `ListarPermitidos(Modulo.Versoes_Sistemas)`; sem `servidor` na URL, o selecionado é o padrão; escolher outro servidor envia `ObterVersoes` com a nova chave e navega com `servidor=...`; `?servidor=xyz` → `Aviso` "O servidor xyz não está na lista; mostrando {padrão}" e leitura do padrão; `ServidorPadrao` ausente da lista → estado pedindo para escolher o servidor, sem leitura

### Implementação da História 2

- [X] T045 [US2] Em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Versoes.razor`, acrescentar o resumo e o grupo segmentado de filtros (`role="group"`, `aria-label="Filtrar por situação"`, botões com `aria-pressed` e a quantidade), o `FiltroLocal` sobre `NomeNormalizado` (rótulo "Filtrar por sistema", `/` e `Esc` já vêm do componente), a combinação busca + filtro e o estado vazio com "Ver todos"; parâmetros `busca` e `filtro` lidos com `[SupplyParameterFromQuery]` e gravados com `NavigateTo(Navegacao.Versoes(...), replace: true)`. T043 passa
- [X] T046 [US2] Em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Versoes.razor`, acrescentar o `SeletorServidor` (grupos de `ListarPermitidos.Query(Modulo.Versoes_Sistemas)`), o parâmetro `servidor` na URL, a troca de servidor (nova obtenção, do cache se válido), o `Aviso` de servidor fora da lista e o estado sem servidor padrão. T044 passa
- [X] T047 [US2] Revisar acessibilidade da tela com o leitor de tela e só teclado: ordem de foco (seletor, busca, filtros, Atualizar, expansores, botões de cópia), foco visível, anúncio do resultado do filtro numa região `aria-live="polite"` ("N de T sistemas"), cabeçalhos de tabela com `scope`; ajustar `Versoes.razor` e `siat.css`

**Ponto de controle**: T043 e T044 passam; quickstart, seção 3.

---

## Fase 5: História 3 — Resultado rápido, atual e que não trava (Prioridade: P2)

**Objetivo**: cache por servidor na tela, "Atualizar agora", andamento com tempo decorrido e Cancelar, e os estados de falha da tabela.

**Teste independente**: com uma máquina inacessível, conferir o tempo total e o erro só na célula; "Atualizar agora" depois de publicar; cancelar durante a leitura; tabela indisponível com e sem resultado anterior (quickstart, seção 4).

### Testes da História 3 ⚠️

- [X] T048 [P] [US3] Em `src/Tcmrj.Siat.Test/Componentes/VersoesPaginaTests.cs`, acrescentar: "Atualizar agora" envia `ObterVersoes` com `Atualizar = true` e atualiza o status; durante a leitura, `EstadoCarregando` com "Lendo endereços em {servidor}" e Cancelar, trocado por "Lendo {N} arquivos de versão" quando `ObterAndamento` passa a devolver N (com o temporizador do tempo decorrido avançado no teste); Cancelar com resultado anterior volta a ele, sem resultado mostra "Tentar de novo"; erro sem resultado → `EstadoErro` com servidor, motivo e "Tentar de novo"; resultado com falha → `Aviso` com o motivo e "Dados gerados às HH:mm"; tabela sem sistemas ativos → "Nenhum sistema cadastrado em {servidor}"
- [X] T049 [P] [US3] Criar `src/Tcmrj.Siat.Test/Unidade/Versoes/ObterAndamentoHandlerTests.cs`: sem `Versoes_Sistemas.CanSelect` → `Erros.Versoes.AcessoNegado` e o serviço não é consultado; chave nula → `ServidorPadrao`; chave inexistente → `Erros.Consultas.ServidorNaoEncontrado`; autorizado → a `QuantidadeEmLeitura` do serviço para a chave (nula ou N)

### Implementação da História 3

- [X] T050 [US3] Criar `src/Tcmrj.Siat.App/Web/Features/Versoes/ObterAndamento.cs` (`Query(string? Servidor)` → `Result<int?, Erro>`), autorizando como a `ObterVersoes` (`AutorizarAsync(Modulo.Versoes_Sistemas, chave)`) e devolvendo `IVersoesPublicadasService.QuantidadeEmLeitura` ([research.md R10](./research.md#r10-andamento-da-leitura)). T049 passa
- [X] T051 [US3] Em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Versoes.razor`, acrescentar "Atualizar agora", o `EstadoCarregando` com tempo decorrido e Cancelar, consultando `ObterAndamento` a cada segundo para trocar "Lendo endereços em {servidor}" por "Lendo {N} arquivos de versão" (um `CancellationTokenSource` por obtenção, cancelado também no `Dispose` da página e ao trocar de servidor), o retorno ao resultado anterior no cancelamento, o `EstadoErro` com "Tentar de novo", o `Aviso` de falha com o horário dos dados e o estado de nenhum sistema cadastrado, no padrão de `Catalogo.razor`. T048 passa
- [X] T052 [US3] Conferir no `VersoesPublicadasService` e no `LeitorDeVersoes` que o cancelamento chega ao `SqlCommand` (token no `ExecuteReaderAsync`), ao `HttpClient` e às esperas das leituras de arquivo, e que uma leitura cancelada não fica registrada como resultado nem como falha. T028 passa

**Ponto de controle**: T048 e T049 passam; quickstart, seção 4 (incluindo leitura total em cerca de 10 s com uma máquina fora do ar e em até 30 s no pior caso).

---

## Fase 6: História 4 — Acesso controlado às versões (Prioridade: P3)

**Objetivo**: página inicial e mensagens de acesso considerando Versões.

**Teste independente**: com um usuário sem `Versoes_Sistemas.CanSelect`, conferir a ausência do item no menu e a negação pela URL direta; com um usuário só com Versões, a página inicial leva a Versões (quickstart, seção 5).

### Testes da História 4 ⚠️

- [X] T053 [P] [US4] Em `src/Tcmrj.Siat.Test/Componentes/HomeTests.cs`, testar a ordem Catálogo, Versões, MailQueue, AuditLog: só com `Versoes_Sistemas.CanSelect` vai para `sistemas/versoes`; com Catálogo e Versões vai para `sistemas/catalogo`; sem nenhum recurso, o texto cita o grupo **Sistemas** como "catálogo e versões de sistemas"
- [X] T054 [P] [US4] Em `src/Tcmrj.Siat.Test/Componentes/VersoesPaginaTests.cs`, testar que sem `Versoes_Sistemas.CanSelect` a página (com e sem `servidor`, `busca` e `filtro` na URL) mostra o acesso negado citando `Versoes_Sistemas.CanSelect`, não chama `ObterVersoes` nem `ListarPermitidos` e não renderiza versão, caminho nem mensagem de erro

### Implementação da História 4

- [X] T055 [US4] Em `src/Tcmrj.Siat.Web/Components/Pages/Home.razor`, acrescentar `(Modulo.Versoes_Sistemas, Navegacao.RotaVersoes)` depois do Catálogo em `Ferramentas`, atualizar o comentário e o texto de "sem acesso" ("grupo **Sistemas** (catálogo e versões de sistemas)"). T053 passa
- [X] T056 [US4] Conferir em `src/Tcmrj.Siat.Web/Components/Pages/Sistemas/Versoes.razor` que a verificação de acesso acontece antes de ler parâmetros e de chamar qualquer feature, e que `src/Tcmrj.Siat.Web/Components/Pages/AcessoNegado.razor` recebe o recurso `Versoes_Sistemas.CanSelect` no texto. T054 passa

**Ponto de controle**: T053 e T054 passam; quickstart, seção 5.

---

## Fase 7: Acabamento

**Objetivo**: documentação, verificação completa e publicação.

- [X] T057 [P] Atualizar `README.md` (incluindo, na parte de publicação, que a seção `Versoes` vai com `ServidorPadrao = tcmbd-prd` e que o `appsettings.json` do repositório traz `tcmbd3-dev`), `DESIGN.md` (se a tela introduzir padrão novo, como o resumo ou o filtro segmentado) e `.github/copilot-instructions.md` (lista de módulos: **Sistemas › Catálogo e Versões**); no `PRD.md`, marcar VE-01 a VE-05 como atendidos pela feature 003 e registrar que o servidor dos endereços é escolhido na lista fechada, com PRD como padrão
- [X] T058 [P] Rodar `dotnet build src/Siat.slnx` sem avisos novos e `dotnet test --solution src/Siat.slnx` com todos os testes verdes, incluindo os do Catálogo depois da extração do `LeituraEmCache`
- [ ] T059 Executar o [quickstart.md](./quickstart.md) inteiro em desenvolvimento, com o script lado a lado (SC-003), e anotar no PR os resultados e as diferenças encontradas
- [ ] T060 Antes de publicar, conferir os itens de operação de [contracts/configuracao.md](./contracts/configuracao.md) (`SELECT` na tabela nos três servidores; leitura nos compartilhamentos; HTTPS de saída) e `ServidorPadrao = tcmbd-prd` na configuração publicada; executar o quickstart, seção 6

---

## Dependências e ordem de execução

### Entre fases

- **Preparação (Fase 1)**: sem dependências.
- **Base (Fase 2)**: depende da Preparação; bloqueia todas as histórias. T015 antes de T016.
- **História 1 (Fase 3)**: depende da Base. É o MVP.
- **História 2 (Fase 4)**: depende da História 1 (mesma página e view models).
- **História 3 (Fase 5)**: depende da História 1; pode correr em paralelo com a 2 se uma pessoa cuidar do andamento e do cancelamento (T049, T050, T052) e a integração na página (T051) for combinada com T045/T046.
- **História 4 (Fase 6)**: depende só da Base para `Home.razor` (T053, T055) e da História 1 para a página (T054, T056).
- **Acabamento (Fase 7)**: depois das histórias desejadas.

### Dentro da História 1

- Testes T019–T028 antes da implementação, e falhando.
- Domain (T029, T030, T031) e Infra (T032; T033 → T034 → T035) em paralelo.
- T036 depende de T029 e T031; T037 depende de T015, T034 e T036; T039 depende de T037 e T038; T041 depende de T039 e T040.

## Paralelismo

### Preparação e Base

```text
T002 appsettings | T003 conferência SQL
T004 Permissoes.feature | T005 Navegacao | T006 Menu | T007 LeituraEmCache | T008 Options
T009 Modulo | T010 Erros | T011 Servidor | T012 modelos | T013 interfaces | T017 Navegacao
Depois: T014 opções  ||  T015 → T016 (cache)  ||  T018 menu
```

### História 1

```text
Testes:  T019 interpretador | T020 sistema | T021 classificador | T022 leitor de arquivo | T023 repositório | T024 leitor de versões | T025 feature | T026 célula | T027 página | T028 serviço
Depois:  T029 | T030 | T031 | T032 | T033 → T034 → T035 | T038 | T040 | T042
Por fim: T036 → T037 → T039 → T041
```

### História 2 e História 3

```text
T043 | T044 | T048 | T049
T045 → T046 → T047   ||   T050 | T052   →   T051
```

### História 4

```text
T053 | T054   →   T055 | T056
```

---

## Estratégia de implementação

### MVP primeiro (História 1)

1. Fases 1 e 2.
2. Fase 3 (História 1): a tela já lê do servidor padrão, com cache de 5 minutos (vem do `LeituraEmCache`), versões, divergências, erros e caminhos.
3. Da Fase 4, T046 (seletor de servidor): o esclarecimento de 2026-11-24 faz da escolha do servidor parte da consulta, e o Princípio VI exige a seleção na URL.
4. Da Fase 6, T055 (página inicial): sem ela, quem está só com Versões cairia em "sem acesso".
5. **Parar e validar**: quickstart seções 1 e 2, com o script lado a lado.
6. Publicar em DSV para o desenvolvimento experimentar.

### Entrega incremental

1. Base + História 1 + seletor (T046) + página inicial (T055) → MVP.
2. Resto da História 2 (T045, T047) → resumo, filtros, busca, acessibilidade.
3. História 3 → "Atualizar agora", Cancelar, falhas e avisos.
4. Resto da História 4 (T056) → teste de acesso pela URL.
5. Acabamento, quickstart completo e publicação.

### Em equipe

- Uma pessoa no Domain e no Infra (T029–T035), outra no App (T015–T016, T036–T039), outra no Web (T040–T042).
- Nas Fases 4 e 5, uma pessoa integra `Versoes.razor` de cada vez.

---

## Observações

- Fora do SIAT e desta lista: dar à conta do pool do IIS `SELECT` em `Apps_VersionAddresses` e leitura nos compartilhamentos ([contracts/configuracao.md](./contracts/configuracao.md)); manter a tabela continua sendo feito fora do SIAT.
- O `VersaoDeSistemasPublicados.ps1` continua em uso até a validação (Princípio IV); nenhuma tarefa o altera.
- Não consultar `TCMBD` (PRD) a partir da máquina de desenvolvimento: os testes usam fontes falsas e o quickstart usa `TCMBD3\DEV` e `TCMBD4\HML`.
- Commit ao fim de cada tarefa ou grupo lógico, no branch `features/<tarefa>_versoes_publicadas`.
