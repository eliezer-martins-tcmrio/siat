# Contrato: origem do catálogo (`sistemasDb.js` no GitLab)

## Localização

| Item | Valor padrão | Configuração |
|---|---|---|
| GitLab | `https://git.tcmnet.tcm` (confirmado em 2026-11-24; conferir a rota a partir dos servidores do IIS na publicação) | `CatalogoSistemas:GitLab:BaseUrl` |
| Projeto | `asi/TCMRJDoc` | `CatalogoSistemas:GitLab:Projeto` |
| Arquivo | `sistemasDb.js` | `CatalogoSistemas:GitLab:Arquivo` |
| Branch | `master` | `CatalogoSistemas:GitLab:Ref` |

## Requisição

```http
GET {BaseUrl}/api/v4/projects/{Projeto codificado}/repository/files/{Arquivo codificado}/raw?ref={Ref}
PRIVATE-TOKEN: {token}
```

- `Projeto` e `Arquivo` codificados para URL (`asi%2FTCMRJDoc`, `sistemasDb.js`).
- Resposta 200: o texto do arquivo, em UTF-8, possivelmente com BOM.
- 401: token inválido ou expirado. 403/404: projeto, arquivo ou branch inexistente, ou token sem acesso. Outros códigos e falhas de rede: indisponível. Mapeamento das mensagens em [research.md R6](../research.md#r6-erros-e-mensagens).
- Tempo limite: `CatalogoSistemas:TempoLimiteSegundos` (padrão 10).
- Link exibido no rodapé (FR-022): `{BaseUrl}/{Projeto}/-/blob/{Ref}/{Arquivo}`.

## Formato aceito

O arquivo é o que o `sistemas.html` carrega com `<script src="sistemasDb.js">`: uma lista de objetos atribuída a uma variável.

```js
var data = [
{
    "Title": "Nome do sistema",
    "DSV" : [ { "Machine" : "M1, M2", "URL" : "https://a|https://b", "Path" : "W:\\Pasta" } ],
    "HML" : [ ... ],
    "PRD" : [ ... ],
    "CONT" : [ ... ],
    "OBS" : "Observação opcional"
},
...
];
```

| Campo | Obrigatório | Tipo | Observação |
|---|---|---|---|
| `Title` | sim | texto | Não vazio depois de remover espaços das pontas |
| `DSV`, `HML`, `PRD`, `CONT` | não | lista de objetos | Cada objeto é uma instância |
| `Machine`, `URL`, `Path` | não | texto | `URL` separa endereços por `\|`; `Machine` separa máquinas por `,` |
| `OBS` | não | texto | Exibido como texto puro |

Demais campos são ignorados.

### Sintaxe aceita além do JSON

O leitor aceita o subconjunto de JavaScript abaixo, com o mesmo significado que o navegador dá ([research.md R2](../research.md#r2-interpretação-do-sistemasdbjs)):

- BOM; qualquer texto antes do primeiro `[` e depois do último `]`.
- Vírgula depois do último item de lista ou objeto.
- Nome de campo sem aspas (`OBS: "..."`) ou entre aspas simples.
- Texto entre aspas simples.
- Comentários `//` e `/* */` fora de texto.
- Escapes do JavaScript dentro de texto: `\n \r \t \b \f \v \0 \xHH \uXXXX \u{X...} \' \" \\`, barra seguida de quebra de linha (removida) e **qualquer outro `\c`, que vira `c`** (a barra é descartada, como no navegador).

Qualquer outra construção é erro de formato, com linha e coluna.

## Valores do arquivo atual que perdem a barra

Pela regra do JavaScript, estes caminhos aparecem sem uma ou mais barras, no SIAT e no `sistemas.html`. A correção é na origem (trocar `\` por `\\` no ponto indicado), por commit no TCMRJDoc, fora desta feature:

| Sistema | Ambientes | Trecho no arquivo | Exibido |
|---|---|---|---|
| SCP - Sistema de Controle de Processos | DSV | `G:\DESENVOLVIMENTO\VBSCP\\SCP DESENVOLVIMENTO.EXE` | `G:DESENVOLVIMENTOVBSCP\SCP DESENVOLVIMENTO.EXE` |
| SIGA-RH - AgenteCalculo | DSV, HML, PRD | `C:\\TcmRoot\SIGARH_Agentes\\...` | `C:\TcmRootSIGARH_Agentes\AgenteCalculo\...` |

Outras 12 ocorrências, em DSV e HML de SCP Api, Portal da Segurança Api, SIGA-RH (Vr2) - Portal (intranet), SIGA-RH (Vr2) - Pagamento API, SiCATI e STP, têm uma barra a mais (`W:\\TcmRoot\\\DSV\\...`) e aparecem certas (`W:\TcmRoot\DSV\...`); a barra extra é inofensiva, mas pode ser limpa no mesmo commit.

Levantamento feito no commit `f2db440` do TCMRJDoc (16 linhas, 17 escapes); o roteiro do [quickstart](../quickstart.md) confere de novo antes da validação.
