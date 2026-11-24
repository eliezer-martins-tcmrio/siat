# Contrato: configuração e operação de Versões

## Seção `Versoes` (`appsettings.json`)

```json
"Versoes": {
  "ServidorPadrao": "tcmbd-prd",
  "TempoLimiteSegundos": 5,
  "LeiturasSimultaneas": 32,
  "MinutosDeCache": 5
}
```

| Chave | Padrão | Faixa | Uso |
|---|---|---|---|
| `ServidorPadrao` | `tcmbd-prd` | chave existente no `servidores.json` | Servidor sem escolha na URL: `tcmbd-prd` em todos os ambientes, inclusive no desenvolvimento (a tela abre com produção; DSV e HML ficam no seletor) |
| `TempoLimiteSegundos` | 5 | 1 a 60 | Por arquivo ou endereço |
| `LeiturasSimultaneas` | 32 | 1 a 128 | Leituras de arquivo em paralelo |
| `MinutosDeCache` | 5 | 1 a 60 | Validade do resultado por servidor |

- `VersoesOptions` validado na subida (`ValidateOnStart`), com mensagem em português. Seção ausente: valores padrão; a aplicação sobe normalmente (Princípio IV). `ServidorPadrao` que não existe na lista não impede a subida: a tela pede para escolher o servidor.
- Nenhum segredo novo: SQL e SMB usam a identidade do pool do IIS (autenticação integrada), como as Consultas; os endereços `https` são públicos na rede do Tribunal e lidos sem credencial.
- Tempo de conexão e de comando da consulta à tabela: `ConsultasOptions` (existente).

## Operação (antes de publicar)

| Item | Onde | Conferência |
|---|---|---|
| `SELECT` em `dbo.Apps_VersionAddresses` para a conta do pool | `CSLIB_UserManager` de `TCMBD3\DEV`, `TCMBD4\HML` e `TCMBD` | A conta já lê `dbo.Apps` (AuditLog); conferir a tabela nova pelo quickstart |
| Leitura (NTFS e compartilhamento) | `\\tcm2016web1..4\TcmRoot`, `\\tcm2016webc\TcmRoot`, `\\unity\Corp2\Desenvolvimento`, `\\unity\producao\prod` | Células "Acesso negado à conta do SIAT" indicam o que falta |
| HTTPS de saída | `etcm`, `app`, `www` e `portal.tcm.rj.gov.br` | Células com "Não foi possível acessar o endereço" |

Novos compartilhamentos cadastrados na tabela exigem a mesma permissão de leitura; a falta aparece só na célula, com a mensagem acima.
