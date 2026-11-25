# Pesquisa: Versões Publicadas

**Feature**: [spec.md](./spec.md) | **Plano**: [plan.md](./plan.md) | **Data**: 2026-11-24

As decisões abaixo resolvem os pontos em aberto do contexto técnico. A análise da tabela foi feita em `TCMBD3\DEV` (`CSLIB_UserManager.dbo.Apps_VersionAddresses`, 42 registros, todos ativos) em 2026-11-24; o comportamento de referência é o do `Geral/Ferramentas/VersaoDeSistemasPublicados.ps1` na mesma data.

## R1. Leitura da tabela de endereços

- **Decisão**: `SELECT` parametrizável sem parâmetros de filtro (só `Ativo = 1`), embutido em `Sql/versoes.sql`, na conexão da `FabricaDeConexao` com o servidor escolhido e o banco `Servidor.BancoUserManager` (novo, padrão `CSLIB_UserManager`, como `BancoMailQueue` e `BancoAuditoria`). Colunas lidas: `Id`, `Sistema` e as 12 de endereço ([contracts/origem-enderecos.md](./contracts/origem-enderecos.md)). `NULL` e texto só com espaços valem "não configurado". O `Id` identifica a linha quando dois registros têm o mesmo nome.
- **Tempo limite e erros**: `CommandTimeout` e `ConnectTimeout` das `ConsultasOptions`, com o mesmo mapeamento de erros SQL dos repositórios das Consultas (servidor indisponível, tempo esgotado, sem permissão ou tabela inexistente), em `Erros.Versoes`.
- **Por que o servidor escolhido**: esclarecimento de 2026-11-24 (seletor como no MailQueue e no AuditLog, padrão PRD). A tabela de DSV e HML pode ter endereços diferentes da de PRD (por exemplo, sistemas ainda não cadastrados em PRD).
- **Alternativas consideradas**: conexão `UserManagerConnection` da aplicação (lista do ambiente da publicação, sem escolha; recusada no esclarecimento); conexão fixa em PRD (consultaria produção a partir do desenvolvimento local).

## R2. Interpretação do conteúdo (regras do script)

O script faz, em `BuildInfo`:

1. Com sistema raiz: lê o `.INI` do sistema raiz; se o texto tiver mais de 12 caracteres, extrai `VERSAO=(\d+\.\d+\.\d+)`; põe o resultado no lugar de `{0}` do endereço (`[string]::Format`). Sistema raiz ilegível derruba o script inteiro (o `Get-Content` está fora do `try`, com `ErrorActionPreference = Stop`).
2. Endereço `http...`: o conteúdo da resposta é a versão, sem mais tratamento.
3. Arquivo: `Get-Content` devolve as linhas, que viram um texto só (linhas unidas por espaço); com mais de 12 caracteres, extrai `VERSAO=`; senão, o texto é a versão. Sem `VERSAO=`, a versão é vazia.
4. Versão vazia é "não configurado"; exceção vira `ERROR: mensagem`.

- **Decisão**: `InterpretadorDeVersao` (Domain, puro) com:
  - `Interpretar(texto)`: une as linhas por espaço, como o `Get-Content`; remove espaços nas pontas; se o resultado tiver mais de 12 caracteres, devolve o primeiro `VERSAO=(\d+\.\d+\.\d+)` (regex compilada por `[GeneratedRegex]`, sem diferenciar maiúsculas, como o `-match` do PowerShell) ou vazio; senão, devolve o texto. Vazio é "não configurado" (esclarecimento de 2026-11-24).
  - O mesmo para `http`/`https` (FR-006): diferença deliberada e pequena em relação ao script, que mostra a resposta crua; as respostas atuais são curtas (`5.11.3` e quebra de linha), e tirar a quebra de linha evita divergência falsa.
  - `MontarEndereco(endereco, versaoRaiz)`: troca `{0}` pela versão do raiz (texto simples, sem `string.Format`, para não falhar com outras chaves no caminho). Raiz sem `VERSAO=` monta com vazio (`v.`), como o script; raiz ilegível é erro da célula (mensagem "Versão do sistema raiz não obtida: ...", não um `Erro` da tela), diferença aceita na spec (SC-003).
- **Comparação para divergência**: igualdade ordinal das versões já interpretadas; só entre duas células com versão (nem erro nem vazio), como `Divergem` do script.

## R3. Endereços aceitos e segurança da leitura

A tabela tem 3 formas hoje: `\\maquina\compartilhamento\...\VERSAO.TXT` (maioria), `\\unity\...\SCP.INI`/`SIRHU.INI` e `https://.../versao.txt` (5 sistemas em PRD).

- **Decisão**: `ClassificadorDeEndereco` (Domain, puro) aceita só:
  - caminho UNC absoluto (`Path.IsPathFullyQualified` e começa por `\\`, sem `\\?\` nem `\\.\`);
  - URI absoluta `http`/`https`.
  Qualquer outra forma (letra de unidade, caminho relativo, `file:`, caracteres inválidos) é erro da célula ("endereço não suportado"), sem tentar ler.
- **Por quê**: a tabela é editável por quem administra o UserManager de cada ambiente (inclusive DSV). Sem a restrição, um endereço como `C:\inetpub\...\appsettings.json` faria o servidor do SIAT ler arquivo local dele mesmo e mostrar até 12 caracteres ou um `VERSAO=`. Unidades mapeadas também não existem na sessão do pool do IIS, então não funcionariam de qualquer forma.
- **Limites**: até 64 KB lidos por arquivo ou resposta (o resto é ignorado; `VERSAO.TXT` e os `.INI` atuais têm poucos KB); `HttpClient` com no máximo 3 redirecionamentos e sem credenciais; leitura de arquivo com `FileAccess.Read` e `FileShare.ReadWrite | FileShare.Delete`, para não travar publicação em andamento.
- **Risco aceito (endereços `http`/`https`)**: o servidor do SIAT faz `GET` em qualquer `http`/`https` cadastrado na tabela, inclusive em nomes ou IPs internos, e quem administra o `Apps_VersionAddresses` de um ambiente (inclusive DSV) pode cadastrar esses endereços. O risco foi avaliado e aceito em 2026-11-24 pelo dono do projeto: a requisição é só `GET`, sem credenciais (`UseDefaultCredentials = false`, nenhum cabeçalho de autenticação), com no máximo 3 redirecionamentos e 64 KB lidos, e a tela só mostra até 12 caracteres da resposta, um `VERSAO=x.y.z` ou o código HTTP de erro. Não há restrição de destino; se o uso mudar, a primeira medida é recusar IP literal e `localhost`.
- **Alternativas consideradas**: lista fixa de máquinas permitidas na configuração (mais um cadastro para manter em sincronia com a tabela); aceitar qualquer caminho, como o script (o script roda com a credencial de quem o executa; o SIAT, com a conta de serviço para todos).

## R4. Paralelismo, tempo limite e deduplicação

- **Decisão**: `LeitorDeVersoes` (App) recebe os endereços e:
  1. Monta o conjunto de endereços distintos (sem diferenciar maiúsculas, como o Windows), primeiro os de sistema raiz, depois os demais; cada endereço é lido uma vez (FR-009) por leitura geral.
  2. Lê com `Parallel.ForEachAsync` e `MaxDegreeOfParallelism` = `LeiturasSimultaneas` (padrão 32).
  3. Cada leitura recebe um `CancellationTokenSource` ligado ao da leitura geral, com `CancelAfter(TempoLimiteSegundos)` (padrão 5 s); a espera usa `WaitAsync(token)`, para soltar a vaga mesmo quando o sistema operacional demora a desistir de abrir o caminho de rede (a abertura de arquivo UNC é síncrona no Windows e não responde ao token). A leitura abandonada termina sozinha, sem efeito.
  4. Células com raiz esperam a leitura do raiz e depois leem o endereço montado (também deduplicado).
- **Por quê 32**: com 150 endereços e uma máquina inteira fora do ar (por exemplo, `\\unity`, com cerca de 15 endereços), todas as leituras que esgotam o tempo cabem numa rodada; o caso realista fica em uns 10 s (5 s do raiz + 5 s do endereço montado). Com todas as máquinas fora do ar, são cerca de 5 rodadas de 5 s, mais a etapa do sistema raiz: até 30 s, a meta do SC-002 (revista em 2026-11-24 depois do `/speckit-analyze`). A tela mostra o tempo decorrido e permite cancelar.
- **Cancelamento e a abertura de arquivo de rede**: a abertura (`CreateFile` por SMB) é síncrona e não aceita `CancellationToken`. O cancelamento solta a espera (`WaitAsync`) e a vaga do paralelismo; a abertura abandonada termina sozinha e o resultado dela é descartado. A leitura do conteúdo (`ReadAsync` com o token) é interrompida de fato. A exceção ao Princípio VII está justificada no plano ("Acompanhamento de complexidade").
- **Alternativas consideradas**: leitura sequencial, como o script (minutos com máquinas fora do ar); sem limite de paralelismo (pode ocupar dezenas de threads do pool presas na abertura SMB); tempo limite por máquina em vez de por arquivo (agrupar por máquina complica sem ganho com 32 vagas).

## R5. Cache, leitura única e cancelamento

O `CatalogoSistemasService` (002) já resolve: validade, "Atualizar agora", uma leitura por vez para pedidos simultâneos, resultado anterior preservado com aviso de falha, cancelamento por contagem de quem espera (o último a sair cancela a leitura na origem).

- **Decisão**: extrair essa lógica para `LeituraEmCache<TChave, TValor>` (App/Services), com uma entrada por chave (`Dictionary` sob uma única `Lock`) e a função de leitura recebida no construtor. O catálogo passa a usar uma chave única; as versões usam a chave do servidor. Os testes atuais do serviço do catálogo continuam passando e os casos genéricos vão para `LeituraEmCacheTests`, com um caso novo: chaves diferentes não compartilham leitura nem validade.
- **Validade**: 5 minutos (`Versoes:MinutosDeCache`, 1 a 60), contada do fim da leitura.
- **Falha**: só a falha da tabela de endereços é falha da leitura geral (com resultado anterior do mesmo servidor, a tela continua com ele e mostra o aviso). Erro de arquivo é resultado normal, dentro da célula.
- **Alternativas consideradas**: copiar o `CatalogoSistemasService` e adaptar (duas cópias de código concorrente sutil para manter); `IMemoryCache` com `GetOrCreateAsync` (não faz leitura única nem cancelamento por contagem; recusado pelo mesmo motivo na 002).

## R6. Autorização e registro

- **Decisão**: handler `ObterVersoes` chama `IAutorizacaoConsultaService.AutorizarAsync(Modulo.Versoes_Sistemas, chaveServidor)`, que resolve o servidor pela chave na lista fechada e verifica `Versoes_Sistemas.CanSelect`; sem ela, nenhuma leitura é iniciada (FR-003). Menu, `Home` e abertura da página usam `PodeAcessarModuloAsync`. `ListarPermitidos(Modulo.Versoes_Sistemas)` alimenta o seletor.
- **Servidor padrão**: `Versoes:ServidorPadrao` (chave do `servidores.json`): `tcmbd-prd` em todos os ambientes, inclusive no `appsettings` do repositório: a tela abre com produção (decisão do usuário em 24/11/2026; antes, o desenvolvimento usava `tcmbd3-dev`). Chave da URL desconhecida: usa o padrão e avisa (caso extremo da spec). Chave padrão ausente da lista: a tela pede para escolher o servidor.
- **Sem DBLog**: versões publicadas e caminhos de publicação não são dados sensíveis (spec, Premissas); o DBLog é para MailQueue, AuditLog e escritas (Princípio III). O `ILogger` registra, em nível Information, servidor, quantidade de endereços, erros e duração de cada leitura geral, para diagnóstico de operação.

## R7. Tela

- **Decisão**: página `Components/Pages/Sistemas/Versoes.razor`, rota `sistemas/versoes`, com:
  - `SeletorServidor` e `FaixaContexto` (o servidor de onde vieram os endereços);
  - resumo (`dl`) com total, sem pendências, com divergência e com erro;
  - filtro segmentado (`role="group"`, botões com `aria-pressed`) Todos / Com divergência / Com erro, com a quantidade;
  - `FiltroLocal` para a busca pelo nome (sem acento e maiúsculas, `/` e `Esc`), sobre o nome apenas, como na tela de exemplo;
  - tabela com cabeçalho em duas linhas (ambiente; Máquina 1/Máquina 2), cada linha expansível (`ListaExpansivel` ou botão `aria-expanded`) com o detalhe por máquina e `BotaoCopiar` do caminho;
  - `CelulaVersao` (componente com lógica: versão, divergente, erro, não configurado; texto para leitor de tela);
  - legenda e rodapé com a origem e o tempo de cache.
- Parâmetros de URL: `servidor`, `busca` (já existe `ParametroBusca`) e `filtro` (`div` ou `erro`; ausente = Todos). A tela de exemplo usa `q`; o SIAT mantém `busca`, como o Catálogo.

## R8. Ordenação e textos

- Ordem por nome com a cultura pt-BR (`CompareInfo` de `pt-BR`), como o Catálogo; empate por `Id`. Nomes vêm em `varchar` (Latin1): a conversão é do driver; o quickstart confere nomes acentuados ("eTCM Impressão", "Portal da Segurança").
- Mensagens de erro de leitura em português, sem expor detalhes internos além do necessário: "Arquivo não encontrado", "Caminho não encontrado", "Acesso negado à conta do SIAT", "Tempo limite de 5 s esgotado", "O servidor respondeu 404", "Endereço não suportado", "Versão do sistema raiz não obtida". Exceção inesperada vira "Falha ao ler: {mensagem}" e vai para o `ILogger`.

## R9. Pacotes

- **Decisão**: nenhum pacote novo. `Microsoft.Data.SqlClient`, `Microsoft.Extensions.Http` (via ASP.NET Core) e o `System.IO` do .NET bastam.

## R10. Andamento da leitura

A spec (FR-022) pede que a tela mostre o que está lendo: o servidor, enquanto lê a tabela, e depois a quantidade de arquivos.

- **Decisão**: o `LeitorDeVersoes` recebe um `Action<int>`, chamado uma vez, logo depois de montar o conjunto de endereços distintos, com a quantidade. O `VersoesPublicadasService` guarda esse número por chave de servidor enquanto a leitura daquele servidor está em andamento (um `ConcurrentDictionary<string, int>`, limpo ao fim, com sucesso, falha ou cancelamento) e o expõe em `QuantidadeEmLeitura(string chaveServidor)` → `int?` (nulo enquanto a tabela é lida ou sem leitura). A feature `ObterAndamento` (Query com a chave do servidor) autoriza como a `ObterVersoes` e devolve esse número. A página consulta o andamento a cada atualização do tempo decorrido (uma vez por segundo, o mesmo temporizador do `EstadoCarregando`) e troca "Lendo endereços em {servidor}" por "Lendo {N} arquivos de versão".
- **Por quê**: o resultado da `LeituraEmCache` é único e compartilhado por quem espera; um `IProgress` por pedido não chegaria a quem entrou numa leitura já em andamento. Consultar um número guardado no serviço serve a todos os que esperam, sem mudar o `LeituraEmCache` nem o Catálogo.
- **Alternativas consideradas**: evento ou `IObservable` no serviço, assinado pela página (mais código de assinatura e de descarte no circuito Blazor para um número que muda uma vez); mudar a FR-022 para mostrar só o servidor (recusada: o dono do projeto escolheu manter a quantidade).
