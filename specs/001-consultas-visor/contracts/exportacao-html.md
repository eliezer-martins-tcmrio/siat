# Contrato: arquivo exportado (FR-021, MQ-04, AL-04)

- Nome: `mailqueue_AAAAMMDD_HHMMSS.html` ou `auditlog_AAAAMMDD_HHMMSS.html`, com a hora local do servidor.
- `Content-Type: text/html; charset=utf-8`; baixado pelo navegador.
- Conteúdo: só os itens já carregados na tela, na mesma ordem; o filtro local aplicado vem preenchido.
- Autocontido: CSS e script embutidos; nenhum recurso externo e nenhuma chamada ao SIAT.
- Cabeçalho: módulo, servidor, ambiente (PRD destacado), filtros usados, data e hora da exportação e quem exportou.
- Funciona sem o SIAT: filtro local (sem diferenciar acentos e maiúsculas, contador "X de Y") e detalhes expansíveis.
- MailQueue: corpo de cada e-mail em `<iframe sandbox="" srcdoc>`, com a mesma CSP da decisão R10.
- AuditLog: stack trace e LogEvent já formatados.
- Toda exportação gera um registro `Exportacao` ([registro-consulta.md](./registro-consulta.md)).
