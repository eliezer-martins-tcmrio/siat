# Roteiro de validação: Página Início

Roteiro manual dos fluxos da página Início (constituição, Princípio V). As regras (quais ferramentas aparecem, em
que grupo e em que ordem) e a página têm testes automatizados (`FerramentasTests`, `HomeTests`, `MenuTests`); este
roteiro cobre o que só se vê com o SIAT rodando. Referência: [plan.md](./plan.md).

## Pré-requisitos

1. Usuários de teste: **A** nos grupos `Sistemas` e `Consultas` do SIA; **B** só em `Consultas` (ou só com
   `MailQueue.CanSelect`); **C** sem nenhum dos dois.
2. Testes automatizados verdes:

   ```powershell
   dotnet test --solution src/Siat.slnx
   ```

3. SIAT rodando localmente:

   ```powershell
   dotnet run --project src/Tcmrj.Siat.Web
   ```

## 1. Entrar no SIAT e ver os atalhos (critérios 1 a 4)

| Passo | Esperado |
|---|---|
| Entrar com A em `https://localhost:7271/` | Fica em `/` (não vai para o Catálogo); título "Início", aba "Início · SIAT", descrição "Sistema Integrado de Apoio Técnico do TCMRio." |
| Olhar os atalhos | Grupo **Sistemas** com Catálogo e Versões; grupo **Consultas** com MailQueue e AuditLog; cada atalho com o ícone do menu e uma linha de descrição |
| Entrar com B | Só o grupo **Consultas**, com as ferramentas que B pode usar; nenhuma menção a Sistemas, Catálogo ou Versões |

## 2. Usar um atalho (critério 5)

| Passo | Esperado |
|---|---|
| Com A, clicar em **Versões** | Abre `sistemas/versoes` na mesma aba; o item **Versões** fica marcado no menu |
| Voltar (botão do navegador) | Volta para o Início |
| Com o teclado, Tab até **MailQueue** e Enter | O atalho mostra o contorno de foco; abre `consultas/mailqueue` |

## 3. Voltar ao Início pela marca

| Passo | Esperado |
|---|---|
| Numa ferramenta qualquer, clicar na marca **SIAT** no topo do menu | Abre o Início com os atalhos; o menu continua igual, sem item "Início" (critério 9) |

## 4. Entrar sem nenhuma permissão (critério 6)

| Passo | Esperado |
|---|---|
| Entrar com C | Título "Início" e a mensagem "Você não tem acesso às ferramentas do SIAT", citando os grupos **Sistemas** e **Consultas**; nenhum atalho |
| No modo `UsuarioDeRede`, entrar com uma conta de rede sem matrícula no UserManager | O aviso de vínculo de rede no lugar da mensagem |

## 5. Larguras (critério 10)

| Passo | Esperado |
|---|---|
| Com A, janela de 1280 px | Atalhos em grade, sem rolagem horizontal |
| Com A, janela de 375 px | Menu como barra superior; cada atalho ocupa a largura do conteúdo, sem rolagem horizontal |
