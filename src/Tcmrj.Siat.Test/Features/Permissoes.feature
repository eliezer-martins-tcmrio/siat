# language: pt
Funcionalidade: Permissões por módulo
  Cada módulo do SIAT (Catálogo e Consultas) exige uma política do UserManager. O mesmo recurso
  libera todos os ambientes do módulo.

  Esquema do Cenário: Política exigida para consultar um módulo
    Dado o módulo "<modulo>"
    Quando calculo a política do módulo
    Então a política é "<politica>"
    E a política tem o formato do UserManager para o recurso "<recurso>"

    Exemplos:
      | modulo    | politica            | recurso   |
      | MailQueue | MailQueue.CanSelect | MailQueue |
      | AuditLog  | AuditLog.CanSelect  | AuditLog  |
      | Sistemas  | Sistemas.CanSelect  | Sistemas  |
