# Checklist de qualidade da especificação: Ficha do Sistema

**Propósito**: validar a completude e a qualidade da especificação antes do planejamento
**Criado em**: 2026-11-25
**Feature**: [spec.md](../spec.md)

## Qualidade do conteúdo

- [x] Sem detalhes de implementação (linguagens, frameworks, APIs)
- [x] Focada no valor para o usuário e nas necessidades do negócio
- [x] Escrita para quem não é da área técnica
- [x] Todas as seções obrigatórias preenchidas

## Completude dos requisitos

- [x] Nenhum marcador [NEEDS CLARIFICATION] restante
- [x] Requisitos testáveis e sem ambiguidade
- [x] Critérios de sucesso mensuráveis
- [x] Critérios de sucesso sem detalhes de tecnologia
- [x] Todos os cenários de aceitação definidos
- [x] Casos extremos identificados
- [x] Escopo bem delimitado
- [x] Dependências e premissas identificadas

## Prontidão da feature

- [x] Todo requisito funcional tem critério de aceitação claro
- [x] Os cenários de usuário cobrem os fluxos principais
- [x] A feature atende aos resultados mensuráveis dos Critérios de sucesso
- [x] Nenhum detalhe de implementação vaza para a especificação

## Notas

- Validação em uma iteração, sem pendências.
- Nomes de fontes (`sistemasDb.js`, `Apps_VersionAddresses`, GitLab) e de recursos do UserManager aparecem como nomes do domínio, como no PRD e nas specs 002 e 003; não definem como implementar.
- Decisões tomadas sem perguntar, registradas nos Casos extremos e nas Premissas: nome repetido no catálogo (vale o primeiro, com aviso); nome da tabela vinculado por dois itens (Versões leva ao primeiro em ordem alfabética); a leitura feita pela ficha tem cache próprio e não altera a tela de Versões; "Atualizar agora" na ficha relê o catálogo e só as versões daquele sistema; o filtro **Sem ficha** entra como quarta opção do filtro de Versões; o contrato visual `design/ficha.html` será desenhado no plano.
- BU-02 (Jenkins na ficha) fica fora, para a fase 7.
