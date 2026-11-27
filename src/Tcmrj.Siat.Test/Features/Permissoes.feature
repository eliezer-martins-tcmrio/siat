# language: pt
Funcionalidade: Permissões por módulo
  Cada módulo do SIAT (Catálogo, Versões e Consultas) exige uma política do UserManager. O mesmo recurso
  libera todos os ambientes do módulo. A ficha do sistema mostra o catálogo a quem tem Catalogo_Sistemas e as
  versões do vínculo a quem tem também Versoes_Sistemas (specs/004-ficha-sistema, FR-001, FR-002 e FR-005).
  O próprio consumo do Copilot exige CopilotProprio e mostra só a conta do GitHub conectada
  (specs/005-copilot-meu-consumo, FR-001 e FR-004).

  Esquema do Cenário: Política exigida para consultar um módulo
    Dado o módulo "<modulo>"
    Quando calculo a política do módulo
    Então a política é "<politica>"
    E a política tem o formato do UserManager para o recurso "<recurso>"

    Exemplos:
      | modulo           | politica                   | recurso          |
      | MailQueue        | MailQueue.CanSelect        | MailQueue        |
      | AuditLog         | AuditLog.CanSelect         | AuditLog         |
      | Catalogo_Sistemas | Catalogo_Sistemas.CanSelect | Catalogo_Sistemas |
      | Versoes_Sistemas | Versoes_Sistemas.CanSelect | Versoes_Sistemas |
      | CopilotProprio   | CopilotProprio.CanSelect   | CopilotProprio   |

  Esquema do Cenário: Partes da ficha liberadas por recurso
    Dado um usuário com o catálogo "<catalogo>" e as versões "<versoes>"
    Quando ele pede a ficha do SIGARH, as versões da ficha e as fichas da tela de Versões
    Então a ficha é "<ficha>"
    E as versões da ficha são "<versoes_da_ficha>"
    E as fichas da tela de Versões são "<fichas>"
    E nenhuma leitura é feita para o que foi negado

    Exemplos:
      | catalogo | versoes | ficha       | versoes_da_ficha | fichas    |
      | não      | não     | negada      | negadas          | negadas   |
      | sim      | não     | sem vínculo | negadas          | entregues |
      | não      | sim     | negada      | negadas          | negadas   |
      | sim      | sim     | com vínculo | entregues        | entregues |

  Esquema do Cenário: Próprio consumo do Copilot por recurso e conexão
    Dado um usuário com o recurso CopilotProprio "<recurso>" e a conexão "<conexao>"
    Quando ele pede a pessoa, o consumo do período e o uso do limite do Copilot
    Então as três consultas do Copilot respondem "<resultado>"
    E o GitHub foi consultado só para a conta "<contas>"

    Exemplos:
      | recurso | conexao      | resultado      | contas  |
      | não     | nenhuma      | acesso negado  | nenhuma |
      | não     | membro ativo | acesso negado  | nenhuma |
      | sim     | nenhuma      | sem conexão    | nenhuma |
      | sim     | membro ativo | dados da conta | ana-m   |
      | sim     | ex-membro    | não membro     | ana-m   |
