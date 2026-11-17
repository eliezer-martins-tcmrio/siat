# language: pt
Funcionalidade: Permissões por módulo e ambiente
  Cada módulo da seção Consultas exige uma política do UserManager que depende do ambiente do servidor:
  DSV e HML usam o recurso comum e PRD usa o recurso com sufixo PRD.

  Esquema do Cenário: Política exigida para consultar um módulo em um ambiente
    Dado o módulo "<modulo>"
    Quando calculo a política para o ambiente "<ambiente>"
    Então a política é "<politica>"
    E a política tem o formato do UserManager para o recurso "<recurso>"

    Exemplos:
      | modulo    | ambiente | politica               | recurso      |
      | MailQueue | Dsv      | MailQueue.CanSelect    | MailQueue    |
      | MailQueue | Hml      | MailQueue.CanSelect    | MailQueue    |
      | MailQueue | Prd      | MailQueuePRD.CanSelect | MailQueuePRD |
      | AuditLog  | Dsv      | AuditLog.CanSelect     | AuditLog     |
      | AuditLog  | Hml      | AuditLog.CanSelect     | AuditLog     |
      | AuditLog  | Prd      | AuditLogPRD.CanSelect  | AuditLogPRD  |

  Esquema do Cenário: Políticas que liberam o módulo no menu
    Dado o módulo "<modulo>"
    Quando listo as políticas do módulo
    Então as políticas são "<comum>" e "<prd>"

    Exemplos:
      | modulo    | comum               | prd                    |
      | MailQueue | MailQueue.CanSelect | MailQueuePRD.CanSelect |
      | AuditLog  | AuditLog.CanSelect  | AuditLogPRD.CanSelect  |
