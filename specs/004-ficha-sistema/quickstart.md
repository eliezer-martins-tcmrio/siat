# Roteiro de validação: Ficha do Sistema

Roteiro manual dos fluxos da feature (Princípio V). Regras e componentes têm testes automatizados; este roteiro cobre o que só se vê com o SIAT rodando, o catálogo real no GitLab, a tabela de endereços e os compartilhamentos. Referências: [spec](./spec.md), [rotas e tela](./contracts/rotas-ui.md), [campo Versoes](./contracts/campo-versoes.md), [modelo de dados](./data-model.md).

## Pré-requisitos

1. Um branch de teste do TCMRJDoc (por exemplo, `teste-ficha-siat`) com o campo `Versoes` em quatro itens do `sistemasDb.js` ([contracts/campo-versoes.md](./contracts/campo-versoes.md)):
   - **SIGARH**: dois nomes que existem em `Apps_VersionAddresses` de `TCMBD3\DEV` e um que não existe (por exemplo, `"SIGARH Teste Inexistente"`); se houver, um nome que exista em `TCMBD3\DEV` e não em `TCMBD4\HML`;
   - um item com um nome escrito em minúsculas e sem acento (por exemplo, `"etcm impressao"` para "eTCM Impressão");
   - um item com `"Versoes": "SCP"` (texto solto, formato inválido);
   - um item sem o campo.
2. No `src/Tcmrj.Siat.Web/appsettings.json` local (sem commitar), `CatalogoSistemas:GitLab:Ref` apontando para o branch de teste e `Versoes:ServidorPadrao` em `tcmbd3-dev`, para que nenhuma leitura vá ao servidor de produção a partir da máquina de desenvolvimento ([database.instructions.md](../../.github/instructions/database.instructions.md)). Neste roteiro, o "padrão" é `TCMBD3\DEV`, e o outro servidor, `TCMBD4\HML`.
3. Usuários de teste: **A** no grupo `Sistemas` (catálogo e versões); **B** sem esse grupo. As combinações com só um dos recursos estão cobertas pelos testes automatizados (Reqnroll e bUnit); para conferir à mão, use um grupo de teste no `CSLIB_UserManager` de DSV com só `Catalogo_Sistemas` ou só `Versoes_Sistemas`.
4. Testes automatizados verdes:

   ```powershell
   dotnet test --solution src/Siat.slnx
   ```

5. SIAT rodando localmente com uma conta que leia os compartilhamentos:

   ```powershell
   dotnet run --project src/Tcmrj.Siat.Web
   ```

## 1. Ficha aberta pelo Catálogo (História 1)

| Passo | Esperado |
|---|---|
| Entrar com A e abrir **Sistemas › Catálogo** | Nomes dos sistemas como links; colunas e busca iguais às de antes |
| Buscar "versoes" ou o nome de um componente vinculado (por exemplo, "agendador") | Nenhum resultado por causa do vínculo: a busca não procura no campo `Versoes` (FR-011) |
| Acionar **SIGARH** | Abre `sistemas/catalogo/ficha?sistema=SIGARH`; trilha **Sistemas › Catálogo › SIGARH**; item **Catálogo** marcado no menu; título da aba "SIGARH · SIAT" |
| Olhar as seções | DSV, HML, PRD e Contingência, nessa ordem, com sigla e cor; instâncias iguais às da linha do SIGARH no Catálogo |
| Olhar a tabela de versões de cada ambiente | Uma linha por nome vinculado encontrado, na ordem do campo; Máquina 1 e Máquina 2 iguais às da tela de Versões em `TCMBD3\DEV` (SC-004); embaixo, o caminho consultado com Copiar |
| Copiar um caminho | O valor exato vai para a área de transferência; o botão confirma por 1,5 s |
| Olhar Contingência | Só as instâncias e "Não há endereços de versão para Contingência." |
| Olhar o topo | Pílulas da situação (por exemplo, "Com divergência"), "Catálogo lido às HH:mm · Versões geradas às HH:mm em N,N s" e o aviso "SIGARH Teste Inexistente não está entre os registros ativos de TCMBD3\DEV." |
| Medir de **Catálogo** até a versão de PRD do SIGARH na tela, com cache das versões | Menos de 20 s (SC-001) |

## 2. Ficha aberta por Versões e compartilhada (História 2)

| Passo | Esperado |
|---|---|
| Abrir **Versões**, escolher `TCMBD4\HML` e esperar a leitura | Nomes dos sistemas vinculados como links; os demais como texto |
| Acionar o nome **SIGARH** | A ficha abre com `servidor=tcmbd4-hml`, seletor e faixa em `TCMBD4\HML`, **sem nova leitura** (usa o resultado da tela de Versões: o horário de geração é o mesmo) |
| Trocar o servidor da ficha para `TCMBD3\DEV` | A URL perde o `servidor` (é o padrão); as versões passam a ser as de `TCMBD3\DEV` |
| Copiar a URL da ficha e abrir em outra janela com outro usuário do grupo `Sistemas` | Mesmo sistema e mesmo servidor |
| Voltar a **Versões** e escolher **Sem ficha** | Só os sistemas que nenhum item vincula, com a quantidade no botão; combina com a busca; `filtro=semficha` na URL |
| Abrir `sistemas/versoes?filtro=semficha` direto | A tabela aparece logo; **Sem ficha** fica selecionado assim que as fichas chegam |
| Percorrer Catálogo, Versões e ficha só com teclado e com leitor de tela | Links "Ficha do SIGARH", seletor e botões de cópia alcançáveis, com foco visível; o clique no link de Versões não abre a linha (SC-007) |

## 3. Vínculo e avisos (História 3)

| Passo | Esperado |
|---|---|
| Abrir a ficha do item com nome em minúsculas e sem acento | Versões encontradas normalmente (FR-008) |
| Abrir a ficha do item com `"Versoes": "SCP"` | Catálogo normal e aviso de vínculo inválido; o item continua no Catálogo e na busca |
| Abrir a ficha do item sem o campo | Catálogo e a nota "Este sistema não tem versões vinculadas..." com link para o `sistemasDb.js` |
| Abrir a ficha do SIGARH com `TCMBD3\DEV` e com `TCMBD4\HML` | O aviso de nome não encontrado aparece só no servidor que não tem o registro |
| Abrir `sistemas.html` do branch de teste | Mostra o mesmo de antes do campo `Versoes` |

## 4. Rápida, atual e sem travar (História 4)

| Passo | Esperado |
|---|---|
| Esperar mais de 5 minutos sem abrir Versões e abrir a ficha do SIGARH | Instâncias na hora; no bloco de versões, "Lendo endereços em TCMBD3\DEV", depois "Lendo N arquivos de versão" (N pequeno, só os do SIGARH), tempo decorrido e Cancelar; completo em até 15 s (SC-003) |
| Abrir a ficha de novo em menos de 5 minutos | Versões na hora, com o mesmo horário de geração |
| Abrir **Versões** em `TCMBD3\DEV` logo depois | A tela de Versões faz a sua própria leitura completa (a da ficha não entra no cache dela) |
| Iniciar uma leitura da ficha e acionar **Cancelar** | O bloco volta ao resultado anterior ou a "Tentar de novo"; as instâncias continuam visíveis |
| Esperar mais de 10 minutos sem abrir o Catálogo, abrir a ficha e acionar **Cancelar** durante "Lendo o catálogo no GitLab…" | Se a leitura passar de 1 s, tempo decorrido e Cancelar no lugar das seções; Cancelar volta à ficha anterior ou a "Tentar de novo" (Princípio VII) |
| Publicar uma versão em DSV de um componente do SIGARH e acionar **Atualizar agora** na ficha com `TCMBD3\DEV` | Versão nova e horário novo; "Catálogo lido às" também muda |
| Servidor de SQL inacessível (desligar a rede ou usar um servidor de teste fora do ar na lista) | Instâncias continuam; o bloco de versões mostra o erro com o motivo e "Tentar de novo"; com resultado anterior, continua com ele e o aviso da falha |
| `Ref` do catálogo apontando para um branch inexistente, sem cache | Estado de erro do catálogo, como na tela de Catálogo; MailQueue e AuditLog continuam funcionando |
| Medir a abertura da ficha com cache | Instâncias e versões em até 2 s (SC-002) |

## 5. Acesso (História 5)

| Passo | Esperado |
|---|---|
| Entrar com B e abrir `sistemas/catalogo/ficha?sistema=SIGARH&servidor=tcmbd4-hml` | Acesso negado com o recurso `Catalogo_Sistemas.CanSelect`; nenhuma leitura no log do SIAT (SC-006) |
| (Grupo de teste só com `Catalogo_Sistemas`) abrir a mesma URL | Só o catálogo: sem seletor, versões, avisos de vínculo nem nota de como vincular; o `servidor` da URL é ignorado |
| (Grupo de teste só com `Versoes_Sistemas`) abrir **Versões** | Nomes sem link e sem **Sem ficha**; nenhuma leitura do catálogo no log do SIAT |
| Qualquer usuário | Nenhum item novo no menu |

## 6. Casos extremos

| Passo | Esperado |
|---|---|
| Abrir `sistemas/catalogo/ficha?sistema=Inexistente` | "Sistema não encontrado no catálogo" e "Procurar no Catálogo", que abre o Catálogo com a busca "Inexistente" |
| Abrir a ficha de um item com espaço, hífen e acento no nome pelo link e pela URL copiada | Abre a ficha certa nos dois casos |
| `servidor=xyz` na URL da ficha | Ficha com `TCMBD3\DEV` e o aviso "O servidor xyz não está na lista; mostrando TCMBD3\DEV." |
| Dois itens com o mesmo nome no branch de teste | A ficha mostra o primeiro do arquivo e o aviso de repetição |
| O mesmo nome da tabela no `Versoes` de dois itens | Aparece nas duas fichas; em Versões, o link leva ao primeiro em ordem alfabética |

## Encerramento

- Voltar `CatalogoSistemas:GitLab:Ref` para `master` e `Versoes:ServidorPadrao` para `tcmbd-prd` no `appsettings.json` local.
- Registrar o resultado em `specs/004-ficha-sistema/validacao.md`, como na 003.
