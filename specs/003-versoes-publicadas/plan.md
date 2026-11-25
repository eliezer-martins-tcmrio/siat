# Plano de implementação: Versões Publicadas

**Branch**: `features/<tarefa>_versoes_publicadas` (pasta da spec: `003-versoes-publicadas`) | **Data**: 2026-11-24 | **Spec**: [spec.md](./spec.md)

**Entrada**: especificação da feature em `specs/003-versoes-publicadas/spec.md`

## Resumo

Novo módulo **Sistemas › Versões**, que substitui o `Geral/Ferramentas/VersaoDeSistemasPublicados.ps1`. O SIAT lê os endereços ativos de `CSLIB_UserManager.dbo.Apps_VersionAddresses` no servidor escolhido no seletor (lista fechada do `servidores.json`; padrão configurável, PRD nas publicações), lê em paralelo cada arquivo de versão (caminho de rede UNC ou endereço `http`/`https`) com tempo limite de 5 s por arquivo, aplica as regras do script (até 12 caracteres é a versão; acima disso, o primeiro `VERSAO=x.y.z`; marcador `{0}` preenchido com a versão do sistema raiz) e calcula divergência e erro por sistema. O resultado fica em cache em memória por 5 minutos, separado por servidor, com "Atualizar agora", leitura única para pedidos simultâneos, preservação do resultado anterior quando a tabela falha e cancelamento que interrompe as leituras quando ninguém mais espera. Para não duplicar a lógica delicada de cache e cancelamento da feature 002, ela é extraída do `CatalogoSistemasService` para uma classe genérica `LeituraEmCache<TChave, TValor>`, usada pelos dois módulos. A tela segue `design/versoes.html` e reaproveita os componentes da 001/002 (`SeletorServidor`, `FiltroLocal`, `BotaoCopiar`, `EstadoCarregando`, `ListaExpansivel`).

## Contexto técnico

**Linguagem/versão**: C# 14, .NET 10 (último patch da 10.0), como as features 001 e 002.

**Dependências principais**: as da 001 e 002, sem pacote novo ([research.md R9](./research.md#r9-pacotes)). Novas no código: `HttpClient` tipado para os endereços `http`/`https` e leitura de arquivo com `FileStream` assíncrono.

**Armazenamento**: nenhum próprio. Origens: `CSLIB_UserManager.dbo.Apps_VersionAddresses` do servidor escolhido (só `SELECT`), arquivos `VERSAO.TXT`/`.INI` em compartilhamentos de rede e endereços `versao.txt` por HTTPS. Cache só em memória (Princípio I).

**Testes**: xUnit v3, AwesomeAssertions, Moq, Reqnroll (permissões), bUnit (componentes). Fluxos das histórias no roteiro manual do [quickstart](./quickstart.md).

**Plataforma**: IIS no servidor do UserManager, como a 001. Acessos novos a partir do servidor do SIAT, com a identidade do pool (a conta de serviço somente de leitura): leitura SMB em `\\tcm2016web*\TcmRoot`, `\\tcm2016webc\TcmRoot` e `\\unity\...` (Corp2 e producao), e HTTPS de saída para `etcm`, `app`, `www` e `portal.tcm.rj.gov.br`.

**Tipo de projeto**: aplicação web (Blazor Server) na solução de camadas do projeto-modelo.

**Metas de desempenho**: tela em até 2 s (SC-004): com cache quente, a tabela; com cache frio, o andamento com tempo decorrido e Cancelar. Leitura completa em cerca de 10 s com uma máquina ou compartilhamento fora do ar e em até 30 s com todas fora do ar (SC-002): 5 s por arquivo, até 32 leituras simultâneas, cada arquivo lido uma vez por leitura geral; a busca e os filtros são locais, a cada tecla.

**Restrições**: só leitura (tabela, arquivos e endereços); endereço só UNC (`\\servidor\...`) ou `http`/`https`, nunca caminho local do servidor do SIAT; no máximo 64 KB lidos por arquivo; texto da origem exibido sem interpretar marcação; WCAG 2.2 AA; português do Brasil; falha de uma máquina afeta só a célula, e falha da tabela só esta tela.

**Escala/escopo**: 42 sistemas ativos em DSV hoje, até 6 células cada (cerca de 150 endereços distintos mais 3 do sistema raiz); dezenas de usuários; 3 servidores na lista; 1 tela.

## Verificação da constituição (v3.1.0)

*Gate: antes da Fase 0 e de novo depois da Fase 1.*

| Princípio | Como o plano atende | Situação |
|---|---|---|
| I. Sem estado próprio | Fontes de verdade externas: a tabela no UserManager e os próprios arquivos publicados; cache só em memória, 5 min por servidor, com "Atualizar agora" (R5); nada em disco | Atende |
| II. Consulta por padrão | Só `SELECT` na tabela, só leitura de arquivo (`FileAccess.Read`, `FileShare.ReadWrite`) e só `GET` nos endereços; nenhuma escrita em lugar nenhum | Atende |
| III. Segurança e menor privilégio | Recurso `Versoes_Sistemas.CanSelect` (já criado pelo script 001 do `dbchange`) verificado no servidor no menu, na página e na feature, junto com o servidor da lista fechada (`AutorizarAsync`), antes de qualquer leitura (R6); conta de serviço somente de leitura para SQL e SMB; SQL parametrizado e embutido em `Sql/versoes.sql`; servidor só pela chave do `servidores.json`; endereços vindos da tabela restritos a UNC e `http`/`https`, até 64 KB, sem caminho local do servidor (R3); texto exibido como texto. Recurso único para todos os ambientes (decisão por módulo permitida desde a 3.0.0): versões e caminhos de publicação não são dado sensível, por isso a leitura não vai para o DBLog (R6) | Atende |
| IV. Módulos independentes | Módulo próprio (página, feature, serviço, repositórios); tabela indisponível vira erro só nesta tela; máquina fora do ar vira erro só na célula; o script continua disponível até a validação | Atende |
| V. Testes | Unidade: `InterpretadorDeVersao` (12 caracteres, `VERSAO=`, espaços, vazio, `{0}`), `ClassificadorDeEndereco` (UNC, `http`, local, inválido), `SistemaVersionado` (divergência, erro, situação), `LeitorDeVersoes` (paralelo, deduplicação, sistema raiz, tempo limite, falha por célula) com leitor de arquivo falso, `EnderecosDeVersaoRepository` (mapeamento das 14 colunas, erros SQL), `LeituraEmCache` (os casos já cobertos pelo catálogo, mais a separação por chave), handler `ObterVersoes`, `Navegacao.Versoes`. Reqnroll: cenários de `Versoes_Sistemas.CanSelect` em `Permissoes.feature`. bUnit: `CelulaVersao`, página `Versoes` (resumo, filtros, linha expansível, estados), `Menu` e `Home`. Fluxos das histórias 1 a 4 no quickstart, incluindo a comparação célula a célula com o script (SC-003) | Atende |
| VI. Experiência consistente | Contrato visual `design/versoes.html`; componentes do SIAT reaproveitados (`SeletorServidor`, `FaixaContexto`, `FiltroLocal` com `/` e `Esc`, `BotaoCopiar`, `ListaExpansivel`, estados) e `CelulaVersao` novo; ambiente por sigla e cor; servidor, busca e filtro na URL; temas; pt-BR | Atende |
| VII. Desempenho e resiliência | Tempo limite por arquivo (5 s) e na consulta SQL (`CommandTimeout` das Consultas); leituras em paralelo com limite; `EstadoCarregando` com tempo decorrido e Cancelar; o cancelamento chega ao `SqlCommand`, ao `HttpClient` e à leitura dos arquivos já abertos, e só quando ninguém mais espera (R5); a abertura de um arquivo de rede já em curso não pode ser interrompida e é abandonada (exceção justificada em "Acompanhamento de complexidade"); durante a leitura, a tela mostra o servidor e depois a quantidade de arquivos (R10); tela em até 2 s | Atende, com exceção justificada |
| VIII. Simplicidade | Sem pacote novo; cache e cancelamento extraídos do catálogo em vez de copiados (R5); leitura de arquivo com a API do .NET; regras do script num interpretador puro de poucas linhas. Código próprio novo, com motivo: `LeitorDeVersoes` (nenhuma biblioteca interna lê arquivos de versão), `LeituraEmCache` (generalização do que já existe), `CelulaVersao` (lógica de célula testável) | Atende |
| Restrições técnicas | .NET 10, Blazor Interactive Server, instância única (o cache em memória vale para todos os usuários), estrutura do projeto-modelo | Atende |
| Dependências | Nenhum pacote novo; versões da 001/002 mantidas | Atende |
| Fluxo de trabalho no Git | Branch de trabalho no padrão `features/<tarefa>_versoes_publicadas` (ou `features/versoes_publicadas` sem número); `003-versoes-publicadas` é só o nome da pasta da spec | Atende |

Reavaliação depois da Fase 1: o modelo de dados, os contratos e o quickstart não introduzem estado persistente, escrita, segredo novo nem dependência nova. O único item novo de infraestrutura é de operação: dar à conta do pool do IIS `SELECT` em `Apps_VersionAddresses` nos três servidores e leitura nos compartilhamentos listados na tabela ([contracts/configuracao.md](./contracts/configuracao.md)). **Gate aprovado.**

## Estrutura do projeto

### Documentação (esta feature)

```text
specs/003-versoes-publicadas/
├── plan.md              # este arquivo
├── research.md          # Fase 0
├── data-model.md        # Fase 1
├── quickstart.md        # Fase 1
├── contracts/           # Fase 1
│   ├── rotas-ui.md
│   ├── origem-enderecos.md
│   ├── configuracao.md
│   └── usermanager.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Fase 2 (/speckit-tasks)
```

### Código-fonte (raiz do repositório)

Só o que esta feature cria (+) ou altera (~):

```text
src/
├── Tcmrj.Siat.Domain/
│   ├── Enums/
│   │   └── ~ Modulo.cs                     # + Versoes_Sistemas
│   ├── Models/
│   │   ├── ~ Erros.cs                      # + Erros.Versoes
│   │   ├── ~ Servidor.cs                   # + BancoUserManager (padrão CSLIB_UserManager)
│   │   └── Versoes/
│   │       ├── + EnderecoDeVersao.cs       # linha da tabela: sistema + 6 pares (versão, raiz)
│   │       ├── + Maquina.cs                # enum Maquina1, Maquina2
│   │       ├── + LeituraDeCelula.cs        # situação, versão, caminho, mensagem
│   │       ├── + SistemaVersionado.cs      # 6 células, divergência, erro, situação
│   │       ├── + ClassificadorDeEndereco.cs # UNC, http(s) ou não suportado (R3), puro
│   │       ├── + VersoesLidas.cs           # servidor, horário, duração, sistemas
│   │       └── + InterpretadorDeVersao.cs  # regras do script (R2), puro
│   └── Repositories/
│       ├── + IEnderecosDeVersaoRepository.cs
│       └── + ILeitorDeArquivoDeVersao.cs   # lê um endereço → texto ou erro
├── Tcmrj.Siat.Infra/
│   ├── Configurations/
│   │   ├── + VersoesOptions.cs
│   │   └── + VersoesConfiguration.cs       # AddVersoes (options + HttpClient)
│   ├── Sql/
│   │   └── + versoes.sql
│   ├── Versoes/
│   │   └── + LeitorDeArquivoDeVersao.cs    # FileStream/HttpClient, 64 KB, 5 s
│   └── Repositories/
│       └── + EnderecosDeVersaoRepository.cs
├── Tcmrj.Siat.App/
│   └── Web/
│       ├── ~ Navegacao.cs                  # + RotaVersoes, ParametroFiltro, Versoes(servidor, busca, filtro)
│       ├── Features/Versoes/
│       │   ├── + ObterVersoes.cs           # Query(Servidor, Atualizar) → VersoesVM; autoriza
│       │   └── + ObterAndamento.cs         # Query(Servidor) → quantidade de arquivos em leitura; autoriza (R10)
│       ├── Services/
│       │   ├── + LeituraEmCache.cs         # extraído do CatalogoSistemasService (R5)
│       │   ├── ~ CatalogoSistemasService.cs  # passa a usar LeituraEmCache<Unit, CatalogoLido>
│       │   ├── + LeitorDeVersoes.cs        # endereços → SistemaVersionado (paralelo, raiz, dedupe)
│       │   ├── + VersoesPublicadasService.cs # singleton: LeituraEmCache<string, VersoesLidas> + andamento (R10)
│       │   ├── + CacheDeVersoesOptions.cs  # MinutosDeCache e ServidorPadrao, lidos da seção Versoes
│       │   └── Interfaces/+ IVersoesPublicadasService.cs
│       └── ViewModels/Versoes/
│           ├── + VersoesVM.cs              # resumo, horário, duração, falha
│           ├── + SistemaVersoesVM.cs
│           └── + CelulaVersaoVM.cs
├── Tcmrj.Siat.Web/
│   ├── ~ Extensions/ServiceCollectionExtensions.cs  # AddVersoes, serviço singleton
│   ├── ~ appsettings.json                  # Versoes (padrão tcmbd-prd)
│   ├── Components/
│   │   ├── Layout/~ Menu.razor             # item Versões no grupo Sistemas
│   │   ├── Pages/
│   │   │   ├── ~ Home.razor                # Versões depois de Catálogo; texto de sem acesso
│   │   │   └── Sistemas/+ Versoes.razor
│   │   └── Shared/
│   │       └── + CelulaVersao.razor
│   └── wwwroot/css/~ siat.css              # resumo, células de versão (se faltar)
└── Tcmrj.Siat.Test/
    ├── Features/~ Permissoes.feature       # + cenários de Versoes_Sistemas.CanSelect
    ├── Componentes/
    │   ├── + CelulaVersaoTests.cs
    │   ├── + VersoesPaginaTests.cs
    │   ├── ~ HomeTests.cs
    │   └── ~ MenuTests.cs
    └── Unidade/
        ├── Versoes/
        │   ├── + InterpretadorDeVersaoTests.cs
        │   ├── + ClassificadorDeEnderecoTests.cs
        │   ├── + SistemaVersionadoTests.cs
        │   ├── + LeitorDeVersoesTests.cs
        │   ├── + LeitorDeArquivoDeVersaoTests.cs   # pasta temporária e handler HTTP falso
        │   ├── + EnderecosDeVersaoRepositoryTests.cs
        │   ├── + VersoesOptionsTests.cs
        │   ├── + VersoesPublicadasServiceTests.cs
        │   └── + ObterVersoesHandlerTests.cs
        ├── + LeituraEmCacheTests.cs        # inclui os casos que hoje estão nos testes do catálogo
        └── ~ NavegacaoTests.cs
```

Nenhum script novo em `dbchange/`: o recurso `Versoes_Sistemas` e o acesso do grupo **Sistemas** já estão no script `001` ([contracts/usermanager.md](./contracts/usermanager.md)).

**Decisão de estrutura**: segue as camadas da 001/002. As regras do script (interpretação do conteúdo, marcador do sistema raiz, divergência) ficam no `Domain`, puras e testáveis sem rede; o acesso a SQL, arquivos e HTTP fica no `Infra`; a orquestração da leitura (paralelismo, deduplicação, dependência do sistema raiz), o cache e a autorização ficam no `App`; a tela no `Web`. A coluna é **Máquina 1/Máquina 2**, como no script e na tela de exemplo, mesmo quando o endereço é de um compartilhamento ou site e não de uma máquina.

## Acompanhamento de complexidade

| Exceção | Princípio | Por que é necessária | Alternativa mais simples rejeitada |
|---|---|---|---|
| A abertura de um arquivo de rede (`\\servidor\...`) já iniciada não é interrompida pelo cancelamento: a espera por ela é solta (`WaitAsync`) e a abertura termina sozinha, sem efeito no resultado | VII ("interrompendo o comando na origem") | No Windows, abrir um arquivo de rede (`CreateFile` por SMB) é uma chamada síncrona que não aceita `CancellationToken`, e o .NET não oferece forma de abortá-la. A abertura dura no máximo o tempo de conexão SMB do sistema operacional e não executa nada na origem: só abre o arquivo para leitura. Tudo o que vem depois (leitura do conteúdo, consulta SQL e requisições HTTP) é interrompido de fato | Ler cada arquivo num processo separado e encerrá-lo no cancelamento (custo alto por arquivo, mais uma peça para operar); `CancelSynchronousIo` por P/Invoke na thread que abre o arquivo (código nativo frágil, dependente da thread, para ganhar poucos segundos) |
