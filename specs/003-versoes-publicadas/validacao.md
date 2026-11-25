# Validação do quickstart: Versões Publicadas

Execução de 24/11/2026, SIAT local (`https://localhost:7271`, build da branch com o commit `59532c2`), usuário do grupo `Sistemas`. Roteiro: [quickstart.md](./quickstart.md).

## Pré-requisitos

| Item | Resultado |
|---|---|
| Recurso `Versoes_Sistemas` no `CSLIB_UserManager` de DSV | OK (Id 40) |
| `ServidorPadrao = tcmbd-prd` no `appsettings.json` | OK; a tela abre com `TCMBD` e a faixa "Produção" |
| Testes automatizados | OK, 670 de 670 |
| Script `VersaoDeSistemasPublicados.ps1` à mão | OK |

## 1. Ver a versão de cada ambiente

| Passo | Resultado |
|---|---|
| Menu **Sistemas** com **Catálogo** e **Versões** | OK |
| Abrir pelo menu | OK, abre em `TCMBD` (PRD) |
| Andamento, "Gerado às HH:mm em N,N s", rodapé com o servidor | OK (DSV lido em 0,3 a 0,6 s; HML em 1,2 s; PRD em 4,5 s) |
| Ordem alfabética pt-BR, acentos | OK ("eTCM Impressão", "Portal da Segurança") |
| SICATI | OK, seis células com 196.1.1, sem divergência |
| INTRAnet - Editoração | OK, DSV e HML "não configurado", PRD 1.81.7 nas duas máquinas |
| eTCM Assinador | OK, caminho com `v.{versão do SCP}` e `SCP.INI` do sistema raiz no detalhe |
| SCP e SIGA-RH | OK, versão do `VERSAO=` do `.INI` |
| eTCM PORTAL em PRD | OK, `https://etcm.tcm.rj.gov.br/versao.txt` → 196.1.3 |
| Copiar um caminho | OK, caminho exato copiado; confirmação por cerca de 1,5 s |

## 2. Conferência com o script (SC-003)

Script e SIAT ("Atualizar agora") contra `TCMBD3\DEV` no mesmo momento. O script rodou numa cópia que troca só a saída HTML por JSON, mantendo as mesmas funções de leitura.

- 42 sistemas nos dois, na mesma ordem.
- 252 células iguais: mesma versão, mesmo "Erro", mesmo "não configurado", nenhuma divergência nos dois.
- Única pendência real nos dados: STP em PRD, máquinas 1 e 2, "Erro". O script diz que o caminho não existe; o SIAT, "Arquivo não encontrado." (`\\tcm2016web1` e `\\tcm2016web2\TcmRoot\Sites\stp\VERSAO.TXT`).

Nenhuma diferença encontrada.

## 3. Pendências, filtros e URL

| Passo | Resultado |
|---|---|
| Resumo | OK, 42 = 41 sem pendências + 1 com erro, 0 com divergência |
| **Com erro** | OK, só STP; `filtro=erro`; botão com `aria-pressed=true` |
| **Com divergência** | OK, "Nenhum sistema neste filtro"; `filtro=div` |
| Busca `etcm` | OK, os 8 sistemas eTCM; `busca=etcm` |
| Abrir a URL com servidor, busca e filtro | OK, estado restaurado |
| Busca `zzz` e "Ver todos" | OK, limpa busca e filtro (42 linhas, URL só com o servidor) |
| `/` e `Esc` | OK com eventos de teclado simulados (o painel de teste não recebia teclas reais) |
| Expansor | OK, "Caminhos consultados de SICATI", `aria-expanded` |
| `TCMBD4\HML` no seletor | OK, `servidor=tcmbd4-hml`, rodapé com `TCMBD4\HML`; voltar ao DSV usou o cache (mesmo horário) |
| `?servidor=xyz&filtro=abc` | OK, "O servidor xyz não está na lista; mostrando TCMBD.", filtro Todos |

## 4. Rapidez, cache e falhas

| Passo | Resultado |
|---|---|
| Cache: recarregar dentro de 5 min | OK, mesmo horário de geração |
| Cancelar durante a leitura | Não exercitado: a leitura de DSV termina em menos de 0,5 s. Coberto pelos testes de `VersoesPaginaTests` |
| Outra pessoa, duas janelas, máquina inacessível, servidor inexistente, falha da tabela com cache | Não executado nesta rodada |

## 5. Acesso

Não executado nesta rodada (precisa do usuário B, sem o grupo `Sistemas`). Coberto pelos testes de acesso de `VersoesPaginaTests` e `HomeTests`.

## Pendente

- Seção 4: cenários de concorrência, máquina inacessível (tempo limite de 5 s), servidor inexistente e falha da tabela com cache; conferir no log da aplicação.
- Seção 5 com o usuário B.
- Seção 6 (T060), na publicação.
