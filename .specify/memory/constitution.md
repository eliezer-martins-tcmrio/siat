# Constituição do SIAT

O SIAT (Sistema Integrado de Apoio Técnico) é a aplicação web interna do TCMRio que reúne
ferramentas de consulta para suporte e desenvolvedores: catálogo de sistemas, versões publicadas,
MailQueue, AuditLog, documentação no GitLab, gestão do Copilot e builds do Jenkins.

Nesta constituição, DEVE e NÃO DEVE indicam regras obrigatórias.

## Princípios Fundamentais

### I. Sem estado próprio

- O SIAT NÃO DEVE ter banco de dados próprio nem persistir dados de negócio.
- As fontes de verdade são externas: SQL Server, compartilhamentos de rede, GitLab, Jenkins,
  GitHub e UserManager.
- A configuração (lista de servidores, catálogo de sistemas, repositórios de documentação) DEVE
  ficar em arquivos versionados no GitLab ou na configuração da aplicação.
- Cache DEVE existir apenas em memória, com expiração definida e com a opção "atualizar agora".

**Justificativa**: sem estado próprio não há migração, backup nem divergência entre o SIAT e a
origem; o que a tela mostra é sempre o dado da fonte, e a aplicação pode ser republicada ou
reiniciada sem perda.

### II. Consulta por padrão

- O SIAT NÃO DEVE executar ações em produção.
- As únicas operações de escrita permitidas são:
  - edição de documentação, pela API do GitLab;
  - budgets do Copilot, pela API do GitHub.
- Toda escrita DEVE ser feita com o token do próprio usuário, nunca com uma conta de serviço.
- Qualquer nova operação de escrita exige emenda a esta constituição.

**Justificativa**: uma ferramenta de apoio que só lê não pode causar incidente em produção. Nas
duas exceções, o token do usuário garante que a autoria registrada no GitLab e no GitHub seja a
de quem fez a alteração.

### III. Segurança e menor privilégio (INEGOCIÁVEL)

- A autenticação DEVE ocorrer pelo SSO do UserManager de PRD (SIA), por OpenID Connect (fluxo
  authorization code com PKCE), com o cliente OIDC cadastrado no `OpenIddictApplications` do
  UserManager pelo script do `dbchange`. É o modo padrão.
- Modo experimental temporário, enquanto a coordenação avalia a entrada pelo usuário de rede: em
  qualquer publicação do SIAT (DSV, HML e produção), o modo `UsuarioDeRede` de
  `SiatConfiguration:Autenticacao` PODE substituir o OIDC.
  - Nele, o IIS autentica o usuário do Windows do domínio do Tribunal (Negotiate), e o SIAT o liga
    ao usuário interno (`Id` abaixo de 1.000.000) e habilitado do UserManager com o mesmo `Login`
    (`dbo.Users`, o cadastro em que o SIA autentica). Sem esse usuário, ele fica sem matrícula e sem
    nenhuma permissão: usuário desabilitado ou externo NÃO DEVE entrar.
  - A autorização DEVE ser a mesma do OIDC: recursos do UserManager pela claim `matricula`.
  - Ao fim da avaliação, nova emenda DEVE torná-lo definitivo, se a coordenação aprovar, ou
    retirar o modo e o seu código, se recusar.
- A leitura das claims DEVE usar `Tcmrj.Autenticacao.AspNetCore` e a autorização por recurso DEVE
  usar `Tcmrj.Autorizacao.App`, como no SiCATI.
- Toda página que exibe dados e toda operação DEVEM exigir um recurso do UserManager, verificado
  no servidor; esconder o item na interface não é controle de acesso. Páginas que não exibem dados
  (a página inicial que só encaminha ou informa a falta de acesso, e o logout) exigem apenas
  usuário autenticado. São anônimos só o login, os arquivos estáticos e a verificação de
  disponibilidade (`/saude`), que não lê nenhuma fonte de dados.
- Dados de produção DEVEM ter recursos próprios, separados dos recursos de DSV/HML.
- O acesso a SQL Server e a compartilhamentos de rede DEVE usar uma conta de serviço somente de
  leitura.
- O usuário DEVE escolher o servidor apenas de uma lista fechada, em que cada servidor tem seu
  ambiente; NÃO DEVE existir campo de servidor livre.
- Todo SQL DEVE ser parametrizado (`SqlParameter`), e a connection string DEVE ficar no
  `appsettings`.
- Conteúdo externo potencialmente perigoso (corpo de e-mail, HTML, Markdown) DEVE ser exibido
  isolado ou sanitizado.
- Segredos (tokens, senhas, chaves de API) NÃO DEVEM ir para o repositório. Exceção única: o
  `ClientSecret` do cliente OIDC do SIAT no SIA (`TcmrjOidc:ClientSecret`) DEVE ficar no
  `appsettings`, junto do `ClientId`, como no SiCATI. No banco do SIA vai só o hash.
- Toda consulta a dados sensíveis e toda escrita DEVEM ser registradas no log
  (`Tcmrj.CoreLib.DBLog`) com usuário, servidor e filtros.

**Justificativa**: o SIAT concentra em um só lugar o acesso a dados de produção de vários
sistemas. Um único ponto de entrada mal protegido exporia todos eles; por isso este princípio não
admite exceção, e o registro em log permite auditar quem consultou o quê. O modo `UsuarioDeRede`
troca o ponto de entrada, mas não a proteção dos dados: só entra quem tem usuário interno e
habilitado no UserManager, e a autorização por recurso, inclusive a dos dados de produção, é a
mesma do OIDC. Por isso ele pode valer em todas as publicações, inclusive a de produção, até a
decisão da coordenação.

### IV. Módulos independentes e entrega incremental

- Cada ferramenta DEVE ser um módulo que pode ser publicado, usado e testado sozinho, sem
  depender de módulos futuros.
- A falha de uma fonte de dados DEVE afetar apenas o módulo ou o item afetado, nunca a aplicação
  inteira.
- As ferramentas antigas DEVEM continuar disponíveis até o módulo substituto ser validado.

**Justificativa**: entregar módulo a módulo gera valor cedo e limita o risco de cada publicação.
Como as fontes são muitas e externas, a indisponibilidade de alguma delas é esperada e não pode
derrubar as demais ferramentas.

### V. Testes como requisito de entrega

- Regras de negócio (validação de parâmetros, permissões, interpretação de versões, regras do
  Copilot) DEVEM ter testes automatizados escritos antes ou junto com o código.
- Componentes com lógica DEVEM ter testes de componente.
- Os fluxos principais de cada módulo DEVEM estar no roteiro de validação manual da feature
  (`quickstart.md`).
- Código sem teste das suas regras NÃO DEVE ser considerado pronto.

**Justificativa**: o SIAT é consultado para diagnosticar problemas; uma regra errada de permissão
ou de interpretação de versão leva a uma decisão errada do suporte. Os testes são a única
garantia repetível de que essas regras continuam corretas a cada entrega.

### VI. Experiência consistente e acessível

- Toda a interface, as mensagens, os identificadores e os comentários DEVEM estar em português do
  Brasil.
- O visual DEVE ser único, baseado na biblioteca Blazor Blueprint com versão fixada.
- Os componentes recorrentes (tabela de resultados, cabeçalho de página, seletor de servidor,
  estados vazio/erro/carregando) DEVEM ser encapsulados em componentes próprios do SIAT.
- A aplicação DEVE oferecer os temas claro, escuro e do sistema.
- A interface DEVE atender à WCAG 2.2 nível AA, permitir navegação completa por teclado e
  oferecer o atalho "/" para busca.
- Filtros e seleção DEVEM ser refletidos na URL, para que links possam ser compartilhados.

**Justificativa**: as ferramentas são usadas em sequência durante um atendimento; comportamento
igual em todas reduz erro e tempo de aprendizado. Componentes próprios isolam o SIAT de mudanças
da biblioteca visual, a acessibilidade garante o uso por todos os servidores do Tribunal, e a URL
compartilhável permite repassar um caso sem refazer a consulta.

### VII. Desempenho e resiliência

- Toda chamada externa DEVE ter tempo limite.
- Consultas longas DEVEM mostrar o tempo decorrido e poder ser canceladas, interrompendo o
  comando na origem.
- Leituras independentes DEVEM rodar em paralelo.
- As páginas DEVEM abrir em até 2 segundos.

**Justificativa**: o SIAT depende de fontes que podem estar lentas ou fora do ar. Sem tempo
limite e cancelamento na origem, uma consulta presa consome recursos do servidor consultado e
deixa o usuário sem resposta.

### VIII. Simplicidade

- DEVE ser preferida a solução mais simples que atende ao requisito.
- NÃO DEVEM existir camadas, abstrações ou dependências que não resolvam um problema presente.
- As bibliotecas internas do Tribunal (`Tcmrj.Autenticacao.AspNetCore`, `Tcmrj.Autorizacao.App`,
  `Tcmrj.CoreLib.*`) DEVEM ser reaproveitadas em vez de reimplementadas; só se escreve código
  próprio para o que elas não cobrem, com o motivo registrado no plano.

**Justificativa**: cada camada, abstração ou dependência a mais é código para entender, testar e
manter atualizado. Reaproveitar as bibliotecas internas mantém o SIAT alinhado aos demais
sistemas do Tribunal e evita reimplementar o que já está em uso.

## Restrições Técnicas

- **Plataforma**: .NET 10, Blazor Web App em modo Interactive Server.
- **Hospedagem**: IIS no servidor do UserManager, com HTTPS e WebSockets habilitados.
- **Implantação**: instância única, sem separação por ambiente; o ambiente é atributo do servidor
  consultado.
- **Integrações**: GitLab, Jenkins e GitHub apenas pelas APIs HTTP oficiais.
- **Estrutura da solução**: segue o projeto-modelo do Tribunal (`Domain`, `Infra`, `App`, `Web`,
  `Test`, com features no padrão do modelo e scripts de banco em `dbchange`), exceto que o projeto
  `Web` é um Blazor Web App em vez de ASP.NET Core MVC.

## Dependências

- Os pacotes internos `Tcmrj.*` DEVEM estar na última versão estável publicada no feed interno
  (TCMRIO INTERNO) no momento da entrega.
- Os demais pacotes DEVEM estar na última versão estável compatível com a plataforma (.NET 10);
  pacotes do ASP.NET Core e do EF Core seguem o último patch da 10.0.
- Só são permitidos pacotes com licença gratuita para uso comercial (por exemplo MIT, Apache-2.0,
  BSD). Pacotes com licença paga ou restrita (como FluentAssertions 8 ou superior) NÃO DEVEM ser
  usados; quando houver, usa-se a alternativa gratuita (como AwesomeAssertions).
- Versões pré-lançamento (`-preview`, `-rc`, `-prerelease`) NÃO DEVEM ser usadas.
- Toda versão DEVE ser fixada no `.csproj`; a atualização é revista a cada entrega, e a troca de
  versão principal (MAJOR) de um pacote DEVE ser registrada no plano da feature.

## Fluxo de Trabalho no Git

O nome do branch identifica o propósito do trabalho.

- **Regra geral**: o nome do branch DEVE conter apenas letras sem acento, números e os caracteres
  `_` (sublinhado), `.` (ponto) e `-` (hífen), além da `/` que separa o prefixo. NÃO DEVE conter
  espaços nem acentos.
- **Prefixo obrigatório**: todo branch de trabalho DEVE começar por um dos prefixos `features/`,
  `issues/` ou `releases/`, escritos OBRIGATORIAMENTE com todas as letras em minúsculo.
- **`features/`**: para tarefa planejada ou extra do Planner. O nome DEVE começar pelo número da
  tarefa, seguido de uma descrição sucinta. Exemplo: a tarefa
  `CE146.19 - [#eTCM-API][#Portal TCMRio] Criação de projeto WEB API` vira
  `features/CE146.19_web_api`. Se a tarefa ainda não tiver número (por exemplo, antes de as
  tarefas serem importadas para o Planner do Sprint), o branch PODE omitir o número e usar só a
  descrição sucinta.
- **`issues/`**: para correção de bug cadastrado como issue no GitLab (Out Of Sprint). O nome DEVE
  começar pelo número da issue, seguido de uma descrição sucinta. Exemplo: a issue
  `37 - Problema ao carregar checklist` vira `issues/37_correcao_carregamento_checklist`.
- **`releases/`**: guarda versões publicadas em produção que precisam de correção de bug. São
  branches protegidos: só os Owners do grupo no GitLab podem enviar para eles, como no `main`.

**Justificativa**: o prefixo e o número ligam o branch à tarefa do Planner ou à issue do GitLab
sem precisar abrir o código. O prefixo em minúsculo é obrigatório porque o Windows não diferencia
maiúsculas de minúsculas em nomes de pasta, e prefixos com grafias diferentes (`Features/` e
`features/`) causam conflito entre o GitLab e as máquinas Windows.

## Governança

- Esta constituição prevalece sobre especificações e planos.
- Todo plano DEVE conter uma verificação explícita de conformidade com cada princípio.
- Exceções DEVEM ser justificadas no plano, com a alternativa mais simples que foi rejeitada e o
  motivo. O Princípio III não admite exceção.
- Emendas DEVEM atualizar a versão da constituição, por versionamento semântico, e registrar a
  data e o motivo no histórico abaixo:
  - **MAJOR**: remoção ou redefinição de princípio;
  - **MINOR**: acréscimo de princípio ou de seção;
  - **PATCH**: ajuste de redação, sem mudança de regra.

### Histórico de emendas

| Versão | Data       | Motivo              |
|--------|------------|---------------------|
| 1.0.0  | 2026-11-09 | Ratificação inicial |
| 1.1.0  | 2026-11-10 | Autenticação por OIDC no SIA (padrão do SiCATI); estrutura do projeto-modelo com Web em Blazor; nova seção Dependências (últimas versões, só licenças gratuitas) |
| 1.1.1  | 2026-11-17 | Princípio III: a exigência de recurso do UserManager vale para páginas que exibem dados e para toda operação; páginas sem dados (inicial, logout) exigem só autenticação e o login é anônimo. PATCH: explicita a intenção original, que a redação anterior tornava impossível de cumprir (o próprio login não pode exigir recurso) |
| 1.2.0  | 2026-11-17 | Princípio III: o `ClientSecret` do cliente OIDC do SIAT (`TcmrjOidc:ClientSecret`) passa a ficar no `appsettings`, junto do `ClientId`, no padrão do SiCATI; os demais segredos continuam fora do repositório. MINOR: muda uma regra sem remover nem redefinir o princípio |
| 1.3.0  | 2026-11-18 | Princípio V: os fluxos principais deixam de exigir teste de interface automatizado (navegador) e passam a constar do roteiro de validação manual da feature; as regras e os componentes continuam com testes automatizados. MINOR: muda uma regra sem remover nem redefinir o princípio |
| 1.4.0  | 2026-11-18 | Nova seção Fluxo de Trabalho no Git: padrão de nomes de branch (`features/`, `issues/`, `releases/` em minúsculo; só letras sem acento, números, `_`, `.` e `-`). MINOR: acréscimo de seção |
| 1.5.0  | 2026-11-19 | Princípio III: modo experimental temporário `UsuarioDeRede` (usuário do Windows ligado ao usuário interno e habilitado do UserManager com o mesmo login), escolhido por `SiatConfiguration:Autenticacao`, só nas publicações de DSV e HML, enquanto a coordenação avalia a entrada pelo usuário de rede; o OIDC no SIA continua o padrão e o único modo em produção, e a autorização é a mesma nos dois. Explicita também que arquivos estáticos e `/saude` são anônimos. MINOR: muda uma regra sem remover nem redefinir o princípio, como na 1.2.0; a adoção em produção exigirá emenda MAJOR |
| 2.0.0  | 2026-11-19 | Princípio III: o modo experimental `UsuarioDeRede` passa a ser permitido também na publicação de produção, além de DSV e HML; o OIDC no SIA continua o padrão, a autorização continua a mesma nos dois modos, e ao fim da avaliação da coordenação nova emenda torna o modo definitivo ou o retira. MAJOR: redefine a regra de autenticação do princípio, como a 1.5.0 previu para a adoção em produção |

**Versão**: 2.0.0 | **Ratificada em**: 2026-11-09 | **Última emenda**: 2026-11-19
