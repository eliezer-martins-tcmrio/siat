# language: pt
Funcionalidade: Permissões por módulo
  Cada módulo da seção Consultas exige uma política do UserManager. O mesmo recurso libera
  os três ambientes (DSV, HML e PRD).

  Esquema do Cenário: Política exigida para consultar um módulo
    Dado o módulo "<modulo>"
    Quando calculo a política do módulo
    Então a política é "<politica>"
    E a política tem o formato do UserManager para o recurso "<recurso>"

    Exemplos:
      | modulo    | politica            | recurso   |
      | MailQueue | MailQueue.CanSelect | MailQueue |
      | AuditLog  | AuditLog.CanSelect  | AuditLog  |
