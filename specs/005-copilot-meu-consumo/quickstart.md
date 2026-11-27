# Roteiro de validação: Meu consumo do Copilot

**Feature**: [spec.md](./spec.md) | **Plano**: [plan.md](./plan.md)

Roteiro manual dos fluxos das histórias (Princípio V). As regras e os componentes têm testes automatizados: `dotnet test --solution src/Siat.slnx`.

## Pré-requisitos

1. **Token de leitura de billing** criado por um owner da organização ([contracts/github-api.md](./contracts/github-api.md#permissões-do-token-de-leitura)) e aprovado, se a organização exigir.
2. **OAuth App** de desenvolvimento com callback `https://localhost:7271/` ([contracts/conexao-github.md](./contracts/conexao-github.md#aplicação-oauth-no-github)).
3. Seção `Copilot` preenchida em `src/Tcmrj.Siat.Web/appsettings.json` ([contracts/configuracao.md](./contracts/configuracao.md)).
4. Script `002` do `dbchange` executado no `CSLIB_UserManager` de DSV ([contracts/usermanager.md](./contracts/usermanager.md)).
5. Usuários do SIAT:
   - **A**: no grupo `Copilot`, com conta do GitHub **membro** da organização e **com licença** Copilot;
   - **B**: no grupo `Copilot`, com conta do GitHub que **não é membro** da organização;
   - **C**: sem o recurso `CopilotProprio`.
6. O `gestao-copilot` rodando com o mesmo token, para comparar valores (SC-003).
7. Executar: `dotnet run --project src/Tcmrj.Siat.Web` e abrir `https://localhost:7271`. Repetir as seções 1 e 2 com `SiatConfiguration:Autenticacao` em `Oidc` e em `UsuarioDeRede` ([research.md R3](./research.md#r3-a-conexão-dentro-do-circuito-blazor)).

## 1. Conectar a conta (História 2)

| Passo | Esperado |
|---|---|
| Entrar com A, num navegador sem o cookie `.Siat.GitHub`, e abrir **Copilot › Meu consumo** | Explicação "Conecte sua conta do GitHub" e **Conectar ao GitHub**; nenhuma chamada ao GitHub no log da aplicação |
| Acionar **Conectar ao GitHub** | Tela de autorização do GitHub para a OAuth App, **sem permissões pedidas** além da identificação |
| Autorizar | Volta para `copilot/meu-consumo/{login de A}` com os dados; no DBLog, "Conexão da conta {login} do GitHub por {matrícula}"; cookie `.Siat.GitHub` `HttpOnly`, `Secure`, expiração em 30 dias |
| Medir do clique no menu até os dados, nesta primeira vez | Menos de 1 minuto (SC-001) |
| Sair do SIAT, entrar de novo com A no mesmo navegador | A página abre já conectada (esclarecimento de 2026-11-27) |
| Sair e entrar com C no mesmo navegador; depois com outro usuário do grupo `Copilot` | C: acesso negado. Outro usuário: explicação de conexão; a conta de A não vale para ele |
| Entrar com B e conectar | Mensagem "A conta {login} do GitHub não é membro ativo da organização {org}."; sem cookie; "Recusa" no DBLog |
| Conectar e acionar **Cancel** na tela do GitHub | "A conexão com o GitHub não foi feita." e **Conectar ao GitHub** de novo |
| Com A conectado, acionar **Desconectar do GitHub** | Explicação de conexão; cookie apagado; "Desconexão" no DBLog |
| Reciclar o pool do site no IIS (publicação de DSV) e abrir a página com A conectado | Continua conectado se o perfil do pool for carregado; senão, pede a conexão de novo, sem erro ([R2](./research.md#r2-conectar-ao-github-oauth)) |

## 2. Próprio consumo do mês (História 1)

| Passo | Esperado |
|---|---|
| Com A conectado, abrir **Copilot › Meu consumo** | Trilha **Copilot › Meu consumo**; período do dia 1 do mês até hoje (UTC); item **Meu consumo** marcado no menu |
| Olhar o cabeçalho | Nome e foto do GitHub, "Licença Copilot ativa", times com link (nova aba) e "via sub-time" onde houver |
| Olhar o resumo | Consumo bruto (créditos e período), valor líquido com "Não inclui o preço das licenças", limite vigente com a origem, uso do limite com faixa em texto e "US$ N abaixo do limite no relatório" |
| Comparar com a página de A no `gestao-copilot`, mesmo período | Consumo bruto, líquido, créditos e consumo por modelo iguais; limite igual ao que o owner vê (SC-003). Se A não tiver exceção individual, o SIAT mostra o valor do padrão (o `gestao-copilot` mostra a membros só "Padrão da organização") |
| Olhar a evolução | Uma coluna por dia; trocar a métrica (bruto, líquido, créditos) e a visão por modelo (cinco + "Outros") |
| Olhar a tabela de modelos | Ordem por créditos; participação somando 100%; "Modelo não informado" em texto diferenciado, se houver |
| Medir a abertura com cache vazio e de novo em menos de 10 minutos | Página em até 2 s; consumo do mês em até 10 s com cache vazio e em até 2 s com cache (SC-002) |

## 3. Outro período e link (História 3)

| Passo | Esperado |
|---|---|
| Escolher **Mês anterior** | `de` e `ate` no endereço; uso do limite "—" com "Disponível em períodos que incluem o mês atual" |
| Escolher **Últimos 7 dias**, **Últimos 30 dias**, **Hoje**, **Ontem** | Intervalos corretos em UTC |
| Informar `de` > `ate`, uma data futura, uma data de 25 meses atrás e um intervalo de 93 dias | Mensagem de período inválido; nada consultado |
| Acionar um dia na evolução (mouse e teclado) | Período passa a ser aquele dia |
| **Limpar período** | Volta ao padrão; `de` e `ate` saem do endereço |
| Copiar o endereço, abrir numa nova aba | Mesmo período |
| Trocar o login do endereço pelo de outra pessoa da organização | Volta para a própria página, com o mesmo período; nenhuma chamada ao GitHub com o login da outra pessoa no log da aplicação (SC-004) |

## 4. Rápida, que não trava e falhas como falhas (História 4)

| Passo | Esperado |
|---|---|
| Consultar 92 dias nunca consultados | "Lendo N de 92 dias", tempo decorrido e **Cancelar**; completo em cerca de 15 s |
| Repetir e acionar **Cancelar** no meio | Volta ao resultado anterior ou a "Tentar de novo"; não aparece erro |
| Voltar à página em menos de 10 minutos | Só a confirmação de membro é relida; nenhum relatório, licença, time ou limite; horário da consulta original |
| **Atualizar agora** | Novo horário; são relidos a confirmação de membro, o perfil, a licença, os times, o limite, os três últimos dias do período e o mês atual; os dias mais antigos não (log da aplicação, FR-030) |
| Trocar o token por um inválido e reiniciar | Mensagem de token recusado só na página do Copilot; Catálogo, Versões e Consultas normais (Princípio IV) |
| Apagar a seção `Copilot` e reiniciar | "O módulo Copilot não está configurado no servidor."; o SIAT sobe |
| Simular limite de requisições (handler falso nos testes; ou token esgotado) | "Limite de chamadas do GitHub atingido" com o tempo para tentar de novo; nada mostrado como zero (SC-005) |

## 5. Acesso (História 5)

| Passo | Esperado |
|---|---|
| Com C, abrir `copilot/meu-consumo`, `copilot/meu-consumo/{qualquer}` e `copilot/github/conectar` | Acesso negado com o recurso `CopilotProprio`; sem item no menu nem atalho no Início |
| Com A, abrir `copilot/meu-consumo/{LOGIN-EM-MAIÚSCULAS}` | Página de A |
| Retirar A da organização no GitHub (ou usar uma conta de teste) e atualizar a página | Ligação desfeita, mensagem de que a conta não é membro ativo; "Desconexão" com motivo `nao-membro` no DBLog |
| Percorrer conexão e página só com teclado e com leitor de tela | Filtro, atalhos, Conectar, Desconectar, Atualizar agora, links dos times e dias da evolução alcançáveis, com foco visível e nomes que dizem o que fazem |
| Temas claro, escuro e do sistema | Contraste AA em textos, faixas e gráfico |

## Operação

- **Token de billing**: anotar a validade; ao renovar, trocar `Copilot:GitHub:Token` no `appsettings` publicado e reciclar o pool.
- **Segredo da OAuth App**: ao gerar outro, trocar `Copilot:OAuth:ClientSecret` e reciclar.
- O `gestao-copilot` continua no ar até a validação desta página pelos membros (Princípio IV).
