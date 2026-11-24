# Plano de implementação: Catálogo de Sistemas

**Branch**: `features/<tarefa>_catalogo_sistemas` (pasta da spec: `002-catalogo-sistemas`) | **Data**: 2026-11-24 (revisado no mesmo dia após o `/speckit-analyze`: constituição v3.0.0, cancelamento, MVP) | **Spec**: [spec.md](./spec.md)

**Entrada**: especificação da feature em `specs/002-catalogo-sistemas/spec.md`

## Resumo

Novo módulo **Catálogo › Sistemas**, que substitui o `TCMRJDoc/sistemas.html`. O SIAT lê o próprio `sistemasDb.js` do repositório TCMRJDoc pela API de arquivos do GitLab, com um token de projeto só de leitura guardado no `appsettings`, e o interpreta com um leitor próprio que aceita o subconjunto de JavaScript do arquivo e segue a regra de escapes do JavaScript, para mostrar exatamente o que a página antiga mostra. O catálogo fica em cache em memória por 10 minutos, com "Atualizar agora", preservação da última leitura válida quando o GitLab falha e cancelamento da leitura (interrompida no GitLab quando ninguém mais espera por ela). A tela reaproveita a base da feature 001 (autorização por recurso no circuito, componentes do `DESIGN.md`, o componente `FiltroLocal` com busca, contador, `/` e `Esc`, `BotaoCopiar`) e acrescenta o recurso `Sistemas.CanSelect`, o grupo de menu **Catálogo** e um componente de célula por ambiente.

## Contexto técnico

**Linguagem/versão**: C# 14, .NET 10 (último patch da 10.0), como a feature 001.

**Dependências principais**: as da feature 001, sem pacote novo ([research.md R8](./research.md#r8-pacotes)). Novas no código: `HttpClient` tipado (`AddHttpClient`) e `System.Text.Json`.

**Armazenamento**: nenhum próprio. Origem: `sistemasDb.js` no branch `master` do projeto `asi/TCMRJDoc` do GitLab. Cache só em memória (Princípio I).

**Testes**: xUnit v3, AwesomeAssertions, Moq, Reqnroll (permissões), bUnit (componentes). Fluxos das histórias no roteiro manual do [quickstart](./quickstart.md).

**Plataforma**: IIS no servidor do UserManager, como a 001. Novo acesso de rede de saída: do servidor do SIAT para o GitLab interno, por HTTPS.

**Tipo de projeto**: aplicação web (Blazor Server) na solução de camadas do projeto-modelo.

**Metas de desempenho**: tela em até 2 s (SC-003): com cache quente, a lista; com cache frio, o estado de carregamento com tempo decorrido e Cancelar; leitura do GitLab limitada por tempo limite de 10 s; filtro a cada tecla sem atraso perceptível com 63 sistemas.

**Restrições**: só leitura no GitLab; token no `appsettings` e nunca em mensagens; nenhuma marcação da origem interpretada; link só para `http`/`https`; WCAG 2.2 AA; português do Brasil; falha do GitLab não afeta os outros módulos.

**Escala/escopo**: 63 sistemas e 155 instâncias hoje (dezenas de KB); dezenas de usuários simultâneos; 1 tela.

## Verificação da constituição (v3.0.0)

*Gate: antes da Fase 0 e de novo depois da Fase 1.*

| Princípio | Como o plano atende | Situação |
|---|---|---|
| I. Sem estado próprio | Fonte de verdade é o `sistemasDb.js` no GitLab (configuração versionada no GitLab, como o princípio prevê para o catálogo); cache só em memória, com validade de 10 min e "Atualizar agora" (R4), ambos no MVP; nada em disco | Atende |
| II. Consulta por padrão | Só leitura no GitLab, com `GET` na API de arquivos; nenhuma escrita. Corrigir o catálogo continua no GitLab, fora do SIAT | Atende |
| III. Segurança e menor privilégio | Recurso `Sistemas.CanSelect` verificado no servidor no menu, na página e na feature antes de devolver dados (R5); token de projeto só de leitura (`read_repository`, papel Reporter), no `appsettings`, como o `ClientSecret` (exceção da constituição 3.1.0), nunca em mensagem nem log (R1, R6); texto da origem exibido sem interpretar marcação e link só para `http`/`https` (FR-009, FR-012); nenhum código da origem é executado (leitor próprio em vez de motor JavaScript, R2). Recurso único para todos os ambientes, permitido desde a emenda 3.0.0, que retirou a exigência de recurso próprio para dados de produção; leitura não registrada no DBLog porque o catálogo não é dado sensível (R5) | Atende |
| IV. Módulos independentes | Módulo próprio (página, feature, serviço, repositório); falha do GitLab vira estado de erro só nesta tela; configuração ausente não impede a aplicação de subir (R6); o `sistemas.html` continua funcionando com o mesmo arquivo | Atende |
| V. Testes | Unidade: `LeitorSistemasDb` (cada particularidade de R2, escapes do JavaScript, erros com linha e coluna, amostra representativa do arquivo real), validação e montagem dos itens (R3), `CatalogoSistemasService` (validade, releitura forçada, leitura única concorrente, leitura anterior preservada), repositório com handler HTTP falso (códigos 401/404, tempo esgotado), `Navegacao.Sistemas`. Reqnroll: cenários de `Sistemas.CanSelect` em `Permissoes.feature`. bUnit: `CelulaAmbiente`, `Menu` com o grupo Catálogo. Fluxos das histórias 1 a 4 no quickstart | Atende |
| VI. Experiência consistente | Contrato visual `design/sistemas.html`; componentes do SIAT reaproveitados (`FiltroLocal` ganha só o parâmetro opcional `Placeholder`) e `CelulaAmbiente` novo em `Components/Shared`; temas; atalho `/` e `Esc` pelo `FiltroLocal`; busca na URL (`busca`) já no MVP; etiqueta de ambiente com sigla e cor; pt-BR | Atende |
| VII. Desempenho e resiliência | Tempo limite de 10 s no `HttpClient`; uma leitura por vez para pedidos simultâneos; durante a leitura, `EstadoCarregando` com tempo decorrido e Cancelar; o cancelamento solta a espera de quem cancelou e interrompe a requisição ao GitLab quando ninguém mais espera por ela (contagem de quem espera, R4); tela em até 2 s, com a lista (cache quente) ou com o carregamento (cache frio) | Atende |
| VIII. Simplicidade | Sem pacote novo; um campo no singleton em vez de `IMemoryCache` (R4); leitor de cerca de cem linhas em vez de motor JavaScript ou biblioteca JSON5 (R2); `FiltroLocal`, `BotaoCopiar` e a autorização da 001 reaproveitados. Código próprio novo, com motivo: `LeitorSistemasDb` (nenhuma biblioteca interna lê o formato), `CatalogoSistemasService` (cache e leitura única), `CelulaAmbiente` (lógica de célula testável) | Atende |
| Restrições técnicas | .NET 10, Blazor Interactive Server, instância única, estrutura do projeto-modelo | Atende |
| Dependências | Nenhum pacote novo; versões da 001 mantidas | Atende |
| Fluxo de trabalho no Git | O branch de trabalho segue o padrão `features/<tarefa>_catalogo_sistemas` (ou `features/catalogo_sistemas` sem número); `002-catalogo-sistemas` é só o nome da pasta da spec | Atende |

Reavaliação depois da Fase 1: o modelo de dados, os contratos e o quickstart não introduzem estado persistente, escrita nem dependência nova. O único item novo de infraestrutura é o token do GitLab, no `appsettings` como o `ClientSecret` (constituição 3.1.0). **Gate aprovado.**

Reavaliação depois do `/speckit-analyze` (2026-11-24): o recurso único deixou de depender de interpretação (emenda 3.0.0 retirou a regra de recurso próprio para produção); a leitura passou a ser cancelável com interrupção na origem (Princípio VII); o MVP inclui "Atualizar agora" e a busca na URL (Princípios I e VI). **Gate aprovado.**

## Estrutura do projeto

### Documentação (esta feature)

```text
specs/002-catalogo-sistemas/
├── plan.md              # este arquivo
├── research.md          # Fase 0
├── data-model.md        # Fase 1
├── quickstart.md        # Fase 1
├── contracts/           # Fase 1
│   ├── rotas-ui.md
│   ├── origem-sistemasdb.md
│   ├── configuracao.md
│   └── usermanager.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Fase 2 (/speckit-tasks)
```

### Código-fonte (raiz do repositório)

Só o que esta feature cria (+) ou altera (~):

```text
dbchange/196.1.1/                        # pasta da sprint atual (confirmar se mudou)
└── + 003 - TCMRio - CSLIB_UserManager - Recurso Sistemas e grupo Catalogo do SIAT.sql

src/
├── Tcmrj.Siat.Domain/
│   ├── Enums/
│   │   ├── ~ Modulo.cs                  # + Sistemas
│   │   └── + AmbienteDoCatalogo.cs      # Dsv, Hml, Prd, Cont
│   ├── Extensions/
│   │   └── + AmbienteDoCatalogoExtensions.cs   # sigla, nome, chave no arquivo
│   ├── Models/
│   │   ├── ~ Erros.cs                   # + Erros.Catalogo
│   │   └── Catalogo/
│   │       ├── + Sistema.cs
│   │       ├── + Instancia.cs
│   │       └── + CatalogoLido.cs        # sistemas + quantidade ignorada
│   └── Repositories/
│       └── + ICatalogoSistemasRepository.cs
├── Tcmrj.Siat.Infra/
│   ├── Configurations/
│   │   ├── + CatalogoSistemasOptions.cs
│   │   └── + CatalogoSistemasConfiguration.cs   # AddCatalogoSistemas (options + HttpClient)
│   ├── Catalogo/
│   │   ├── + LeitorSistemasDb.cs        # JavaScript do arquivo → JSON estrito (R2)
│   │   └── + MontadorCatalogo.cs        # validação e montagem dos itens (R3)
│   └── Repositories/
│       └── + CatalogoSistemasRepository.cs      # GitLab → CatalogoLido
├── Tcmrj.Siat.App/
│   └── Web/
│       ├── ~ Navegacao.cs               # + RotaSistemas, ParametroBusca, Sistemas(busca)
│       ├── Features/Sistemas/
│       │   └── + ObterCatalogo.cs       # Query(Atualizar) → CatalogoVM; autoriza
│       ├── Services/
│       │   ├── + CatalogoSistemasService.cs     # singleton: cache, leitura única, anterior preservada
│       │   └── Interfaces/+ ICatalogoSistemasService.cs
│       └── ViewModels/Sistemas/
│           ├── + CatalogoVM.cs
│           ├── + SistemaVM.cs
│           └── + InstanciaVM.cs         # + EnderecoVM
├── Tcmrj.Siat.Web/
│   ├── ~ Extensions/ServiceCollectionExtensions.cs  # AddCatalogoSistemas, serviço singleton
│   ├── ~ appsettings.json               # CatalogoSistemas (sem token)
│   ├── Components/
│   │   ├── Layout/~ Menu.razor          # grupo Catálogo antes de Consultas
│   │   ├── Pages/
│   │   │   ├── ~ Home.razor             # Sistemas primeiro; texto de sem acesso
│   │   │   ├── ~ AcessoNegado.razor     # texto sem citar só o grupo Consultas
│   │   │   └── Catalogo/+ Sistemas.razor
│   │   └── Shared/
│   │       ├── + CelulaAmbiente.razor
│   │       └── ~ FiltroLocal.razor      # + parâmetro opcional Placeholder
│   └── wwwroot/
│       ├── css/~ siat.css               # tabela do catálogo (se faltar no siat.css)
│       └── js/~ siat.js                 # focar(id), se ainda não existir
└── Tcmrj.Siat.Test/
    ├── Features/~ Permissoes.feature    # + cenários de Sistemas.CanSelect
    ├── Componentes/
    │   ├── + CelulaAmbienteTests.cs
    │   ├── + HomeTests.cs
    │   ├── + SistemasPaginaTests.cs
    │   ├── ~ FiltroLocalComponentTests.cs
    │   └── ~ MenuTests.cs
    └── Unidade/
        ├── Catalogo/
        │   ├── + CatalogoSistemasOptionsTests.cs   # inclui a subida do SIAT sem a seção
        │   ├── + LeitorSistemasDbTests.cs
        │   ├── + MontadorCatalogoTests.cs
        │   ├── + CatalogoSistemasRepositoryTests.cs
        │   ├── + CatalogoSistemasServiceTests.cs
        │   ├── + ObterCatalogoHandlerTests.cs
        │   └── Amostras/+ sistemasDb.amostra.js   # recorte com todas as particularidades (R2)
        └── ~ NavegacaoTests.cs
```

**Decisão de estrutura**: segue as camadas da feature 001. O formato do arquivo e o acesso ao GitLab ficam no `Infra` (detalhe da origem); o cache e a autorização, no `App`; a tela e os componentes, no `Web`. O enum `AmbienteDoCatalogo` é separado de `Ambiente` porque o catálogo tem Contingência e a lista fechada de servidores não: acrescentar `Cont` a `Ambiente` abriria esse valor no `servidores.json`, no seletor de servidor e na faixa de contexto, que não o tratam.

## Acompanhamento de complexidade

Nenhuma violação da constituição a justificar.
