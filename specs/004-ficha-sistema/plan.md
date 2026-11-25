# Plano de implementação: Ficha do Sistema

**Branch**: `features/<tarefa>_ficha_sistema` (pasta da spec: `004-ficha-sistema`) | **Data**: 2026-11-25 | **Spec**: [spec.md](./spec.md)

**Entrada**: especificação da feature em `specs/004-ficha-sistema/spec.md`

## Resumo

Nova página **ficha do sistema** (`sistemas/catalogo/ficha?sistema=...&servidor=...`), aberta pelo nome do sistema no Catálogo e em Versões, que reúne por ambiente as instâncias do catálogo e a versão publicada em cada máquina dos registros de `Apps_VersionAddresses` vinculados ao item (PRD VE-06). O vínculo é explícito: um campo novo e opcional `Versoes` (lista de nomes) em cada item do `sistemasDb.js`, que o `sistemas.html` e o leitor atual do SIAT já ignoram. Não há origem nova nem configuração nova: o catálogo vem do cache da 002 e as versões, do cache da 003 quando ele é válido; senão, o `VersoesPublicadasService` lê só os endereços daquele item (mesmo SQL e mesmo `LeitorDeVersoes`, filtrando em memória) num segundo `LeituraEmCache`, por servidor e vínculo, que nunca mexe no cache da tela de Versões. A tela de Versões ganha links para as fichas e o filtro **Sem ficha**, a partir de um mapa nome → item lido do catálogo em paralelo, sem atrasar a tabela; a do Catálogo ganha o link no nome. Contrato visual: `design/ficha.html`.

## Contexto técnico

**Linguagem/versão**: C# 14, .NET 10 (último patch da 10.0), como as features 001 a 003.

**Dependências principais**: as da 001 a 003, sem pacote novo ([research.md R10](./research.md#r10-pacotes-e-configuração)).

**Armazenamento**: nenhum próprio. Origens já usadas: `sistemasDb.js` do TCMRJDoc no GitLab (002), `CSLIB_UserManager.dbo.Apps_VersionAddresses` do servidor escolhido e os arquivos de versão (003). Caches só em memória: o do catálogo (10 min) e o das versões por servidor (5 min), existentes, e um novo para as versões da ficha, por servidor e vínculo (5 min) (Princípio I).

**Testes**: xUnit v3, AwesomeAssertions, Moq, Reqnroll (permissões), bUnit (componentes). Fluxos das histórias no roteiro manual do [quickstart](./quickstart.md).

**Plataforma**: IIS no servidor do UserManager, como a 001. Nenhum acesso de rede novo: os mesmos do Catálogo (GitLab) e das Versões (SQL, SMB, HTTPS).

**Tipo de projeto**: aplicação web (Blazor Server) na solução de camadas do projeto-modelo.

**Metas de desempenho**: ficha em até 2 s com catálogo e versões em cache (SC-002); sem versões válidas, o bloco de versões completo em até 15 s mesmo com todas as máquinas do sistema fora do ar: consulta à tabela, sistema raiz (5 s) e arquivo (5 s), em paralelo dentro de cada passo ([R5](./research.md#r5-versões-da-ficha-cache-e-leitura-só-do-sistema)). A tela de Versões não espera o catálogo para mostrar a tabela.

**Restrições**: só leitura; o vínculo é só o campo `Versoes` (nenhuma dedução por nome); o resultado parcial da ficha não entra no cache da tela de Versões; a busca do Catálogo não muda; texto da origem exibido sem interpretar marcação; WCAG 2.2 AA; português do Brasil; falha do catálogo ou das versões afeta só a parte correspondente.

**Escala/escopo**: 63 itens no catálogo; 42 registros ativos na tabela de DSV; de 1 a 3 nomes vinculados por item, no caso comum; dezenas de usuários; 3 servidores; 1 tela nova e 2 alteradas.

## Verificação da constituição (v3.1.0)

*Gate: antes da Fase 0 e de novo depois da Fase 1.*

| Princípio | Como o plano atende | Situação |
|---|---|---|
| I. Sem estado próprio | Vínculo no próprio `sistemasDb.js` (configuração versionada no GitLab), sem cadastro no SIAT; caches só em memória com expiração e "Atualizar agora": os da 002 e 003 e um novo por servidor e vínculo (R5) | Atende |
| II. Consulta por padrão | Nenhuma escrita: catálogo pela API do GitLab só com `GET`; tabela só com `SELECT`; arquivos só leitura, como na 003. Editar o vínculo é editar o arquivo no GitLab, fora do SIAT | Atende |
| III. Segurança e menor privilégio | Sem recurso novo: `Catalogo_Sistemas.CanSelect` na página e em `ObterFicha`/`ListarFichas`; `Versoes_Sistemas.CanSelect` mais o servidor da lista fechada em `ObterVersoesDaFicha` (que exige também o do catálogo) e `ObterAndamento`, todos verificados no servidor antes de qualquer leitura (R7); sem o recurso de versões, a `FichaVM` não leva vínculo nem avisos dele. Conta de serviço, SQL parametrizado e restrição de endereços (UNC, `http`/`https`) herdados da 003 sem mudança. Texto do catálogo, da tabela e dos arquivos exibido como texto. Sem DBLog: catálogo e versões não são dados sensíveis (002 R5, 003 R6) | Atende |
| IV. Módulos independentes | A ficha usa os módulos de Catálogo e Versões sem mudar o que eles mostram (a tela de Versões só ganha links e um filtro); falha do catálogo vira estado de erro da ficha ou aviso em Versões; falha das versões fica no bloco de versões da ficha; falha de um arquivo fica na célula | Atende |
| V. Testes | Unidade: `NomeDeSistema`, `VinculoDeVersoes`, `MontadorCatalogo` (campo `Versoes`, `Posicao`), `CatalogoLido.Localizar`/`FichasPorVersao`, `VersoesDoSistema`, `LeituraEmCache.Valido`, `VersoesPublicadasService.ObterDoSistemaAsync` (cache da tela, cache da ficha, o mais recente, leitura só do vínculo, cache da tela intocado, andamento), handlers `ObterFicha`, `ObterVersoesDaFicha`, `ListarFichas` e `ObterAndamento` (matriz de recursos), `Navegacao.Ficha` e `semficha`. Reqnroll: cenários da ficha em `Permissoes.feature`. bUnit: página `Ficha` (seções, versões, avisos, estados, sem o recurso de versões), `ListaDeInstancias`/`CelulaAmbiente` (HTML igual), `CabecalhoPagina` com trilha, link e filtro nas páginas de Catálogo e Versões. Fluxos das histórias 1 a 5 no quickstart | Atende |
| VI. Experiência consistente | Contrato visual `design/ficha.html`, no padrão de `sistemas.html` e `versoes.html`; componentes reaproveitados (`CabecalhoPagina` com `Trilha` opcional, `SeletorServidor`, `FaixaContexto`, `CelulaVersao`, `BotaoCopiar`, `Aviso`, estados) e `ListaDeInstancias` extraído de `CelulaAmbiente`; ambiente por sigla e cor; sistema e servidor na URL; temas; pt-BR; link com nome acessível que contém o texto visível | Atende |
| VII. Desempenho e resiliência | Tempos limite herdados (SQL das Consultas, 5 s por arquivo, tempo limite da leitura do GitLab); leitura só do sistema, em paralelo; tempo decorrido e Cancelar na leitura do catálogo, como na tela de Catálogo, e andamento, tempo decorrido e Cancelar no bloco de versões, ambos com cancelamento por contagem de quem espera (`LeituraEmCache`); página em até 2 s com cache; Versões não espera o catálogo. A abertura de arquivo de rede já em curso continua sem interrupção, exceção já justificada no plano da 003 e herdada aqui sem código novo | Atende (exceção herdada da 003) |
| VIII. Simplicidade | Sem pacote, configuração, SQL, script de banco ou origem nova; o cache e o leitor da 003 são reaproveitados no mesmo serviço em vez de um serviço novo (R5); filtro por nome em memória em vez de SQL novo; código próprio novo, com motivo: `VinculoDeVersoes` e `VersoesDoSistema` (regra do vínculo, testável sem rede), `LeituraEmCache.Valido` (espiar o cache sem ler), `ListaDeInstancias` (extração para usar fora de tabela) | Atende |
| Restrições técnicas | .NET 10, Blazor Interactive Server, instância única (os caches valem para todos), estrutura do projeto-modelo | Atende |
| Dependências | Nenhum pacote novo; versões da 001 a 003 mantidas | Atende |
| Fluxo de trabalho no Git | Branch no padrão `features/<tarefa>_ficha_sistema` (ou `features/ficha_sistema` sem número); `004-ficha-sistema` é só o nome da pasta da spec | Atende |

Reavaliação depois da Fase 1: o modelo de dados, os contratos e o quickstart não introduzem estado persistente, escrita, segredo, origem, configuração nem dependência nova. O único item de operação é editorial: preencher o campo `Versoes` no `sistemasDb.js` do TCMRJDoc ([contracts/campo-versoes.md](./contracts/campo-versoes.md)), o que pode ser feito antes da publicação. **Gate aprovado.**

## Estrutura do projeto

### Documentação (esta feature)

```text
specs/004-ficha-sistema/
├── plan.md              # este arquivo
├── research.md          # Fase 0
├── data-model.md        # Fase 1
├── quickstart.md        # Fase 1
├── contracts/           # Fase 1
│   ├── rotas-ui.md
│   └── campo-versoes.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Fase 2 (/speckit-tasks)

design/
└── + ficha.html         # contrato visual da ficha (Fase 1)
```

### Código-fonte (raiz do repositório)

Só o que esta feature cria (+) ou altera (~):

```text
src/
├── Tcmrj.Siat.Domain/
│   ├── Models/
│   │   ├── ~ Erros.cs                       # + Erros.Catalogo.SistemaNaoEncontrado
│   │   ├── + NomeDeSistema.cs               # Normalizar (R2), puro
│   │   ├── Catalogo/
│   │   │   ├── ~ Sistema.cs                 # + Versoes (VinculoDeVersoes), + Posicao
│   │   │   ├── ~ CatalogoLido.cs            # + Localizar, + FichasPorVersao (R4)
│   │   │   └── + VinculoDeVersoes.cs        # nomes, inválido, chave, Contem
│   │   └── Versoes/
│   │       └── + VersoesDoSistema.cs        # componentes, não encontrados, situação (R5), puro
├── Tcmrj.Siat.Infra/
│   └── Catalogo/
│       └── ~ MontadorCatalogo.cs            # lê Versoes (R1) e a posição do item
├── Tcmrj.Siat.App/
│   └── Web/
│       ├── ~ Navegacao.cs                   # + RotaFicha, ParametroSistema, Ficha(...), ValorFiltroSemFicha
│       ├── ~ FiltroDeSituacao.cs            # + SemFicha
│       ├── Features/
│       │   ├── Catalogo/
│       │   │   ├── + ObterFicha.cs          # Query(Sistema, Atualizar) → FichaVM; Catalogo_Sistemas (+ Versoes_Sistemas para o vínculo)
│       │   │   └── + ListarFichas.cs        # Query() → FichasVM; Catalogo_Sistemas
│       │   └── Versoes/
│       │       ├── + ObterVersoesDaFicha.cs # Query(Servidor, Vinculo, Atualizar) → VersoesDaFichaVM; os dois recursos
│       │       └── ~ ObterAndamento.cs      # + Vinculo opcional (R6)
│       ├── Services/
│       │   ├── ~ LeituraEmCache.cs          # + Valido(chave) (R5)
│       │   ├── ~ VersoesPublicadasService.cs # + ObterDoSistemaAsync, cache da ficha, andamento por ChaveDaFicha
│       │   ├── + ChaveDaFicha.cs
│       │   └── Interfaces/~ IVersoesPublicadasService.cs
│       └── ViewModels/
│           ├── Catalogo/
│           │   ├── + FichaVM.cs
│           │   └── + FichasVM.cs
│           └── Versoes/
│               ├── ~ SistemaVersoesVM.cs    # Normalizar delega a NomeDeSistema
│               ├── + VersoesDaFichaVM.cs
│               └── + ComponenteVM.cs
├── Tcmrj.Siat.Web/
│   ├── Components/
│   │   ├── Pages/Sistemas/
│   │   │   ├── + Ficha.razor                # rota /sistemas/catalogo/ficha
│   │   │   ├── ~ Catalogo.razor             # nome como link para a ficha
│   │   │   └── ~ Versoes.razor              # ListarFichas, links, filtro Sem ficha, aviso
│   │   └── Shared/
│   │       ├── + ListaDeInstancias.razor    # extraído de CelulaAmbiente
│   │       ├── ~ CelulaAmbiente.razor       # <td> + ListaDeInstancias
│   │       └── ~ CabecalhoPagina.razor      # + Trilha
│   └── wwwroot/css/~ siat.css               # trilha, seções por ambiente, tabela de versões da ficha
└── Tcmrj.Siat.Test/
    ├── Features/~ Permissoes.feature        # + cenários da ficha e do filtro Sem ficha
    ├── Features/Steps/+ PermissoesDaFichaSteps.cs  # passos dos cenários da ficha (handlers com fontes falsas)
    ├── Componentes/
    │   ├── + FichaPaginaTests.cs
    │   ├── + ListaDeInstanciasTests.cs
    │   ├── ~ CelulaAmbienteTests.cs         # HTML igual ao de antes
    │   ├── ~ CatalogoPaginaTests.cs         # link da ficha
    │   ├── ~ MenuTests.cs                   # Catálogo marcado como atual na ficha
    │   └── ~ VersoesPaginaTests.cs          # links, Sem ficha, catálogo indisponível
    └── Unidade/
        ├── + NomeDeSistemaTests.cs
        ├── ~ LeituraEmCacheTests.cs         # Valido
        ├── ~ NavegacaoTests.cs              # Ficha, semficha
        ├── Catalogo/
        │   ├── Amostras/~ sistemasDb.amostra.js  # itens com Versoes (válido, inválido, sem)
        │   ├── ~ MontadorCatalogoTests.cs   # campo Versoes, Posicao
        │   ├── + VinculoDeVersoesTests.cs
        │   ├── + CatalogoLidoTests.cs       # Localizar, FichasPorVersao
        │   ├── + ObterFichaHandlerTests.cs
        │   └── + ListarFichasHandlerTests.cs
        └── Versoes/
            ├── + VersoesDoSistemaTests.cs
            ├── ~ VersoesPublicadasServiceTests.cs  # ObterDoSistemaAsync
            ├── + ObterVersoesDaFichaHandlerTests.cs
            └── ~ ObterAndamentoHandlerTests.cs     # com vínculo
```

Nenhum script novo em `dbchange/` e nenhuma chave nova no `appsettings.json` ([research.md R10](./research.md#r10-pacotes-e-configuração)).

**Decisão de estrutura**: segue as camadas da 001 a 003. As regras do vínculo (forma do campo, comparação de nomes, item repetido, mapa de fichas, agrupamento das versões por nome) ficam no `Domain`, puras; a leitura do campo, no `Infra`, junto do resto do `sistemasDb.js`; os caches, a autorização e a decisão entre cache da tela e leitura própria, no `App`; a tela, no `Web`. A leitura das versões da ficha fica no `VersoesPublicadasService` porque é ele que guarda o cache por servidor que a ficha precisa espiar e o andamento que ela precisa mostrar (R5).

## Acompanhamento de complexidade

Nenhuma exceção nova à constituição. A exceção do Princípio VII (abertura de arquivo de rede já iniciada não é interrompida pelo cancelamento) foi justificada no [plano da 003](../003-versoes-publicadas/plan.md#acompanhamento-de-complexidade) e vale para as leituras da ficha, que usam o mesmo `LeitorDeVersoes`.
