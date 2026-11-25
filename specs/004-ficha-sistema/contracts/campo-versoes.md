# Contrato: campo `Versoes` do `sistemasDb.js`

Complementa [specs/002-catalogo-sistemas/contracts/origem-sistemasdb.md](../../002-catalogo-sistemas/contracts/origem-sistemasdb.md). É o vínculo explícito entre um item do catálogo e os registros de `CSLIB_UserManager.dbo.Apps_VersionAddresses` (PRD VE-06.1; [research.md R1](../research.md#r1-formato-e-leitura-do-campo-versoes)).

## Formato

Campo opcional de cada item, lista de textos com os valores de `Apps_VersionAddresses.Sistema` que correspondem ao item, na ordem em que devem aparecer na ficha:

```js
{
    "Title": "SIGARH",
    "DSV" : [ { "Machine" : "TCM2016WEB3, TCM2016WEB4", "URL" : "https://...", "Path" : "W:\\..." } ],
    "PRD" : [ { "Machine" : "TCM2016WEB1, TCM2016WEB2", "URL" : "https://...", "Path" : "W:\\..." } ],
    "Versoes" : [ "SIGARH", "SIGARH Agendador" ],
},
```

## Leitura pelo SIAT

| Valor | Resultado |
|---|---|
| Campo ausente ou `null` | Sem vínculo: a ficha mostra só o catálogo e, para quem tem `Versoes_Sistemas.CanSelect`, a nota de como vincular (FR-018) |
| `[]` ou lista só com textos vazios | Sem vínculo, como acima |
| Lista de textos | Cada texto sem espaços nas pontas; vazios descartados; repetidos, considerados uma vez (comparação de [R2](../research.md#r2-comparação-de-nomes)); ordem do campo mantida (FR-009) |
| Texto solto (`"Versoes": "SIGARH"`), número, objeto, `true`/`false` ou lista com algo que não seja texto | Vínculo inválido: o item continua no catálogo e na busca, a ficha mostra o catálogo e o aviso "O campo Versoes deste sistema no catálogo não é uma lista de nomes; corrija no sistemasDb.js." para quem tem `Versoes_Sistemas.CanSelect` (FR-010); log Warning com a posição do item |

- Comparação com a tabela: sem diferenciar acentos, maiúsculas e espaços nas pontas (FR-008). Espaços internos contam: o nome tem de ser escrito como está na tabela.
- Um nome pode estar no vínculo de mais de um item; aparece em todas essas fichas. Na tela de Versões, o link leva ao primeiro desses itens em ordem alfabética, desde que ele seja o primeiro item com o seu nome no arquivo ([R4](../research.md#r4-localizar-o-item-e-montar-o-mapa-de-fichas)).
- O SIAT não deduz vínculo por semelhança de nomes (FR-007): sem o campo, não há versões na ficha.

## O que não muda

- A tela de Catálogo do SIAT não mostra o campo, e a busca dele não procura nos nomes vinculados (FR-011; esclarecimento de 2026-11-25).
- O `sistemas.html` do TCMRJDoc ignora o campo (percorre só `DSV`, `HML`, `PRD`, `CONT` e `OBS`), e o leitor do catálogo do SIAT já publicado também (ignora campos desconhecidos). O campo pode ser preenchido antes da publicação desta feature.
- Editar o vínculo é editar o `sistemasDb.js` pelo GitLab, como qualquer correção do catálogo; o SIAT só lê. A ficha mostra o vínculo novo depois de no máximo 10 minutos (cache do catálogo), ou na hora com "Atualizar agora".

## Preenchimento inicial

Feito pela equipe no TCMRJDoc, fora do SIAT, com os nomes como estão na tabela do servidor de PRD (`TCMBD`). Para achar o que falta: filtro **Sem ficha** da tela de Versões em PRD. Para achar nome escrito errado: aviso "não encontrado em TCMBD" na ficha.
