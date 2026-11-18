# language: pt
Funcionalidade: Filtros do AuditLog
  A consulta do AuditLog filtra por aplicação, pelo início do LogUID, pelo período (dias ou datas) e pela quantidade (FR-013).
  Filtro inválido não chega ao banco: a tela mostra a mensagem do campo e a consulta não é feita.

  Contexto:
    Dado uma consulta ao AuditLog no servidor "tcmbd4-hml"

  Esquema do Cenário: Início do LogUID com dígitos hexadecimais e hífens é aceito, em maiúsculas
    Dado o início do LogUID "<informado>"
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado com o início do LogUID "<enviado>"

    Exemplos:
      | informado                            | enviado                              |
      | 4FDF7B6B                             | 4FDF7B6B                             |
      | 4fdf7b6b                             | 4FDF7B6B                             |
      | 0                                    | 0                                    |
      | 4FDF7B6B-1A2B                        | 4FDF7B6B-1A2B                        |
      | 4fdf7b6b-1a2b-3c4d-5e6f-7a8b9c0d1e2f | 4FDF7B6B-1A2B-3C4D-5E6F-7A8B9C0D1E2F |

  Cenário: Espaços em volta do LogUID são ignorados
    Dado o início do LogUID " 4fdf "
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado com o início do LogUID "4FDF"

  Esquema do Cenário: LogUID com outro conteúdo é recusado sem consulta
    Dado o início do LogUID "<informado>"
    Quando a consulta ao AuditLog é feita
    Então a consulta ao AuditLog é recusada com a mensagem "Informe o início do LogUID só com dígitos hexadecimais (0-9, A-F) e hífens."
    E o banco de auditoria não é consultado

    Exemplos:
      | informado                             |
      | 4fdg                                  |
      | 4f.df                                 |
      | 4f df                                 |
      | 4fdf%                                 |
      | 4f_df                                 |
      | 4fdf7b6b-1a2b-3c4d-5e6f-7a8b9c0d1e2f0 |

  Cenário: LogUID vazio não filtra
    Dado o início do LogUID ""
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado sem início de LogUID

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

  Esquema do Cenário: Dias de 1 a 3650, padrão 30, ajustados ao limite mais próximo
    Dado os dias "<informados>" no AuditLog
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado nos últimos <enviados> dias

    Exemplos:
      | informados | enviados |
      |            | 30       |
      | 1          | 1        |
      | 90         | 90       |
      | 0          | 1        |
      | 5000       | 3650     |

  Cenário: Dias não numéricos são recusados sem consulta
    Dado os dias "abc" no AuditLog
    Quando a consulta ao AuditLog é feita
    Então a consulta ao AuditLog é recusada com a mensagem "Informe um número de dias entre 1 e 3650."
    E o banco de auditoria não é consultado

  Esquema do Cenário: Com alguma data, o período é pelas datas e os dias são ignorados
    Dado os dias "90" no AuditLog
    E o período de "<de>" a "<ate>" no AuditLog
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado de "<de>" a "<ate>" sem os dias

    Exemplos:
      | de         | ate        |
      | 2025-01-01 | 2025-01-31 |
      | 2025-01-01 | 2025-01-01 |
      | 2025-01-01 |            |
      |            | 2025-01-31 |

  Cenário: Data inicial depois da final é recusada sem consulta
    Dado o período de "2025-02-01" a "2025-01-31" no AuditLog
    Quando a consulta ao AuditLog é feita
    Então a consulta ao AuditLog é recusada com a mensagem "A data inicial deve ser igual ou anterior à data final."
    E o banco de auditoria não é consultado

  Cenário: Data fora do formato é recusada sem consulta
    Dado o período de "31/01/2025" a "" no AuditLog
    Quando a consulta ao AuditLog é feita
    Então a consulta ao AuditLog é recusada com a mensagem "Informe uma data válida."
    E o banco de auditoria não é consultado

  Esquema do Cenário: "sem" na aplicação consulta só os registros sem AppID
    Dado a aplicação "<informada>" no AuditLog
    Quando a consulta ao AuditLog é feita
    Então o AuditLog é consultado só nos registros sem AppID

    Exemplos:
      | informada |
      | sem       |
      | SEM       |
      |  sem      |
