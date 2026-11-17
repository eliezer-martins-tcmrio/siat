# language: pt
Funcionalidade: Filtros da MailQueue
  Os filtros da MailQueue seguem as regras do Visor (FR-010, FR-016): SMTP opcional, texto aparado e
  procurado literalmente, e quantidade de 1 a 1000 com padrão 50. Valores inválidos nunca vão ao banco
  como vieram: a quantidade numérica é ajustada ao limite e a não numérica é recusada.

  Cenário: Texto de busca é aparado
    Dado o texto de busca "   boleto 2026  "
    Quando monto o filtro da MailQueue
    Então o filtro é aceito
    E o texto do filtro é "boleto 2026"

  Cenário: Texto de busca vazio vale sem filtro
    Dado o texto de busca ""
    Quando monto o filtro da MailQueue
    Então o filtro é aceito
    E o filtro não tem texto

  Cenário: Texto de busca só com espaços vale sem filtro
    Dado o texto de busca "     "
    Quando monto o filtro da MailQueue
    Então o filtro é aceito
    E o filtro não tem texto

  Cenário: Texto de busca com curingas é guardado como digitado
    Dado o texto de busca "[teste]_%"
    Quando monto o filtro da MailQueue
    Então o filtro é aceito
    E o texto do filtro é "[teste]_%"

  Cenário: Texto de busca com até 500 caracteres é aceito
    Dado um texto de busca com 500 caracteres
    Quando monto o filtro da MailQueue
    Então o filtro é aceito

  Cenário: Texto de busca com mais de 500 caracteres é recusado
    Dado um texto de busca com 501 caracteres
    Quando monto o filtro da MailQueue
    Então o filtro é recusado com a mensagem "O texto de busca deve ter no máximo 500 caracteres."

  Cenário: Sem SMTP escolhido a consulta vale para todos os SMTPs
    Dado nenhum SMTP escolhido
    Quando monto o filtro da MailQueue
    Então o filtro é aceito
    E o filtro vale para todos os SMTPs

  Cenário: SMTP escolhido restringe a consulta
    Dado o SMTP 3
    Quando monto o filtro da MailQueue
    Então o filtro é aceito
    E o SMTP do filtro é 3

  Esquema do Cenário: Quantidade numérica é usada ou ajustada ao limite mais próximo
    Dado a quantidade "<quantidade>"
    Quando monto o filtro da MailQueue
    Então o filtro é aceito
    E a quantidade do filtro é <esperada>

    Exemplos:
      | quantidade           | esperada |
      |                      | 50       |
      | 50                   | 50       |
      | 1                    | 1        |
      | 1000                 | 1000     |
      | 10                   | 10       |
      | 0                    | 1        |
      | -7                   | 1        |
      | 1001                 | 1000     |
      | 99999999999999999999 | 1000     |

  Esquema do Cenário: Quantidade não numérica é recusada com mensagem
    Dado a quantidade "<quantidade>"
    Quando monto o filtro da MailQueue
    Então o filtro é recusado com a mensagem "Informe um número entre 1 e 1000 na quantidade."

    Exemplos:
      | quantidade |
      | abc        |
      | 10a        |
      | 1,5        |
      | 2.5        |

  Cenário: Consulta sem servidor é recusada
    Dado nenhum servidor escolhido
    Quando monto o filtro da MailQueue
    Então o filtro é recusado com a mensagem "Escolha o servidor."
