# language: pt
Funcionalidade: Filtros do AuditLog
  A consulta do AuditLog filtra por aplicação, pelo início do LogID e pela quantidade (FR-013).
  Filtro inválido não chega ao banco: a tela mostra a mensagem do campo e a consulta não é feita.

  Contexto:
    Dado uma consulta ao AuditLog no servidor "tcmbd4-hml"

  Esquema do Cenário: Prefixo de LogID só com dígitos é aceito
    Dado o prefixo de LogID "<informado>"
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado com o prefixo de LogID "<enviado>"

    Exemplos:
      | informado            | enviado              |
      | 4363                 | 4363                 |
      | 0                    | 0                    |
      | 12345678901234567890 | 12345678901234567890 |

  Cenário: Espaços em volta do LogID são ignorados
    Dado o prefixo de LogID " 4363 "
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado com o prefixo de LogID "4363"

  Esquema do Cenário: LogID com outro conteúdo é recusado sem consulta
    Dado o prefixo de LogID "<informado>"
    Quando a consulta ao AuditLog é feita
    Então a consulta ao AuditLog é recusada com a mensagem "Informe apenas dígitos no LogID."
    E o banco de auditoria não é consultado

    Exemplos:
      | informado             |
      | 43a                   |
      | -4363                 |
      | 43.63                 |
      | 43 63                 |
      | 4363%                 |
      | 123456789012345678901 |

  Cenário: LogID vazio não filtra
    Dado o prefixo de LogID ""
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado sem prefixo de LogID

  Esquema do Cenário: Quantidade de 1 a 1000, padrão 50, ajustada ao limite mais próximo
    Dado a quantidade "<informada>" no AuditLog
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado com a quantidade <enviada>

    Exemplos:
      | informada   | enviada |
      |             | 50      |
      | 1           | 1       |
      | 100         | 100     |
      | 1000        | 1000    |
      | 0           | 1       |
      | -5          | 1       |
      | 1001        | 1000    |
      | 99999999999 | 1000    |

  Esquema do Cenário: Quantidade não numérica é recusada sem consulta
    Dado a quantidade "<informada>" no AuditLog
    Quando a consulta ao AuditLog é feita
    Então a consulta ao AuditLog é recusada com a mensagem "Informe um número entre 1 e 1000 na quantidade."
    E o banco de auditoria não é consultado

    Exemplos:
      | informada |
      | abc       |
      | 10a       |
      | 1.5       |

  Cenário: Sem aplicação, a consulta é de todas as aplicações
    Dado a aplicação "" no AuditLog
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado em todas as aplicações

  Cenário: Com aplicação, a consulta é só daquela aplicação
    Dado a aplicação "12" no AuditLog
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado na aplicação 12
