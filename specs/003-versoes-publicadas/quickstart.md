# Roteiro de validação: Versões Publicadas

Roteiro manual dos fluxos da feature (Princípio V). Regras e componentes têm testes automatizados; este roteiro cobre o que só se vê com o SIAT rodando, a tabela real e os compartilhamentos. Referências: [spec](./spec.md), [rotas e tela](./contracts/rotas-ui.md), [origem](./contracts/origem-enderecos.md), [configuração](./contracts/configuracao.md).

## Pré-requisitos

1. Script `001` do `dbchange` executado no `CSLIB_UserManager` de DSV com o recurso `Versoes_Sistemas` ([contracts/usermanager.md](./contracts/usermanager.md)):

   ```powershell
   sqlcmd -S "TCMBD3\DEV" -d CSLIB_UserManager -E -W -Q "SELECT r.Id, r.ResourceKey FROM AppResources r JOIN Apps a ON a.Id = r.AppId WHERE a.Name = 'SIAT'"
   ```

2. Dois usuários de teste: **A** no grupo `Sistemas`; **B** sem esse grupo (pode estar em `Consultas`).
3. Seção `Versoes` no `src/Tcmrj.Siat.Web/appsettings.json` com `ServidorPadrao = tcmbd-prd` (a tela abre com `TCMBD`) ([contracts/configuracao.md](./contracts/configuracao.md)).
4. Testes automatizados verdes:

   ```powershell
   dotnet test --solution src/Siat.slnx
   ```

5. SIAT rodando localmente com uma conta que leia os compartilhamentos (em desenvolvimento, a do próprio desenvolvedor) e o script antigo à mão:

   ```powershell
   dotnet run --project src/Tcmrj.Siat.Web
   ```

## 1. Ver a versão de cada ambiente (História 1)

| Passo | Esperado |
|---|---|
| Entrar com A | Menu **Sistemas** com **Catálogo** e **Versões** |
| Abrir **Versões** sem cache | Andamento "Lendo endereços em TCMBD3\DEV", depois "Lendo N arquivos de versão", com tempo decorrido e Cancelar; em seguida a tabela |
| Olhar o cabeçalho e o rodapé | Servidor `TCMBD3\DEV` no seletor, na faixa de contexto e no rodapé; "Gerado às HH:mm em N,N s" |
| Conferir a ordem | Ordem alfabética pt-BR; "eTCM Impressão" e "Portal da Segurança" com acento correto |
| Olhar **SICATI** | Seis células com versão; nenhuma divergente se DSV-1 e DSV-2 tiverem a mesma |
| Olhar **INTRAnet - Editoração** | DSV e HML "não configurado", PRD com versões |
| Olhar **eTCM Assinador** | Versões lidas pelo caminho com `v.{versão do SCP}`; abrir a linha mostra o caminho montado e o `SCP.INI` do sistema raiz |
| Olhar **SCP** e **SIGA-RH** | Versão extraída do `VERSAO=` do `.INI` |
| Olhar **eTCM PORTAL** em PRD | Versão lida de `https://etcm.tcm.rj.gov.br/versao.txt` |
| Abrir uma linha e copiar um caminho | Caminho exato na área de transferência; o botão confirma por 1,5 s |

## 2. Conferência com o script (SC-003)

1. Rodar o script contra o mesmo servidor, no mesmo momento:

   ```powershell
   D:\Projetos\Geral\Ferramentas\VersaoDeSistemasPublicados.ps1 -Servidor "TCMBD3\DEV"
   ```

2. Comparar célula a célula com o SIAT ("Atualizar agora" antes): mesma versão, mesmo "Erro", mesmo "não configurado" e mesmos destaques de divergência.
3. Diferença esperada e aceita: sistema raiz ilegível (o script para; o SIAT mostra erro só na célula). Não há esse caso na tabela de DSV hoje; o teste automatizado do `LeitorDeVersoes` o cobre.

## 3. Pendências, filtros e URL (História 2)

| Passo | Esperado |
|---|---|
| Olhar o resumo | Total = linhas da tabela; Sem pendências + sistemas com divergência ou erro coerentes com os filtros |
| Clicar **Com divergência** | Só sistemas com selo de divergência; URL com `filtro=div` |
| Digitar `etcm` na busca | Só sistemas com "eTCM" no nome, dentro do filtro; URL com `busca=etcm` |
| Copiar a URL e abrir em outra janela com A | Mesmo servidor, busca e filtro |
| Buscar `zzz` | "Nenhum sistema neste filtro" e "Ver todos", que limpa busca e filtro |
| Teclar `/` fora da busca e `Esc` dentro | Foco na busca; busca limpa |
| Navegar só por teclado com leitor de tela | Filtros com estado anunciado, expansor "Caminhos consultados de ...", células anunciam "difere da outra máquina" e "não configurado" |
| Escolher `TCMBD4\HML` no seletor | Tabela com os endereços do UserManager de HML; URL com `servidor=tcmbd4-hml`; voltar ao DSV usa o cache |
| Abrir `sistemas/versoes?servidor=xyz&filtro=abc` | Aviso de servidor fora da lista, servidor padrão, filtro Todos |

## 4. Rapidez, cache e falhas (História 3)

| Passo | Esperado |
|---|---|
| Abrir a tela pelo menu, com e sem cache, e cronometrar | Em até 2 s aparece a tabela (com cache) ou o andamento com o tempo decorrido (sem cache) (SC-004) |
| Recarregar a página dentro de 5 min | Tabela na hora, mesmo horário de geração |
| Outra pessoa abre a tela dentro de 5 min | Mesmo resultado, sem nova leitura (log da aplicação sem nova leitura geral) |
| Duas janelas acionam "Atualizar agora" ao mesmo tempo | Uma leitura só (log); as duas mostram o mesmo horário |
| Com um endereço de máquina inacessível (por exemplo, um `\\tcm2016webX` desligado ou bloqueado por firewall) | Célula "Erro" com "Tempo limite de 5 s esgotado"; leitura total em cerca de 10 s (até 30 s no pior caso, SC-002) |
| Publicar (ou alterar numa pasta de teste cadastrada) um `VERSAO.TXT` de DSV e acionar "Atualizar agora" | Versão nova e novo horário |
| Acionar "Cancelar" durante a leitura | Volta ao resultado anterior ou ao estado com "Tentar de novo"; log sem leitura concluída |
| Configurar temporariamente um servidor inexistente no `servidores.json` de desenvolvimento e escolhê-lo | Estado de erro com o servidor e o motivo; Catálogo e Consultas continuam funcionando |
| Com resultado de DSV no cache, simular a falha da tabela (servidor parado ou sem permissão) e "Atualizar agora" | Tabela anterior mantida, aviso da falha com o horário dos dados |

## 5. Acesso (História 4)

| Passo | Esperado |
|---|---|
| Entrar com B | Sem o item **Versões**; sem o grupo **Sistemas** se B também não tiver o Catálogo |
| B abre `sistemas/versoes?servidor=tcmbd3-dev` | Acesso negado citando `Versoes_Sistemas.CanSelect`; log da aplicação sem leitura disparada |
| Usuário só com `Versoes_Sistemas` entra pela página inicial | Vai para **Versões** |

## 6. Publicação

1. Conferir os itens de operação de [contracts/configuracao.md](./contracts/configuracao.md) com a conta do pool do IIS: abrir a tela publicada e procurar células "Acesso negado à conta do SIAT" ou "Não foi possível acessar o endereço".
2. `ServidorPadrao = tcmbd-prd` na publicação; conferir que a tela abre com `TCMBD`.
3. Manter o script disponível até os usuários validarem o módulo (Princípio IV).
