using CSharpFunctionalExtensions;

namespace Tcmrj.Siat.Domain.Models;

public static class Erros
{
    public static class Consultas
    {
        public const string CodigoAcessoNegado = "consultas.acesso-negado";

        public const string CodigoServidorNaoEncontrado = "consultas.servidor-nao-encontrado";

        public const string CodigoServidorIndisponivel = "consultas.servidor-indisponivel";

        public const string CodigoTempoEsgotado = "consultas.tempo-esgotado";

        public const string CodigoBancoInacessivel = "consultas.banco-inacessivel";

        public const string CodigoCancelada = "consultas.cancelada";

        public static Erro AcessoNegado => new(
            CodigoAcessoNegado,
            "Você não tem permissão para consultar este servidor.");

        public static Erro ServidorNaoEncontrado => new(
            CodigoServidorNaoEncontrado,
            "Servidor não encontrado na lista de servidores consultáveis.");

        public static Erro ServidorIndisponivel(string servidor, string motivo) => new(
            CodigoServidorIndisponivel,
            $"Não foi possível conectar ao servidor {servidor}: {motivo}");

        public static Erro TempoEsgotado(string servidor) => new(
            CodigoTempoEsgotado,
            $"A consulta ao servidor {servidor} excedeu o tempo limite. Tente de novo ou use filtros para reduzir o resultado.");

        public static Erro BancoInacessivel(string servidor, string banco) => new(
            CodigoBancoInacessivel,
            $"O banco {banco} do servidor {servidor} não está acessível. Confira se ele existe nesse servidor e se a conta de serviço do SIAT pode lê-lo.");

        public static Erro Cancelada => new(
            CodigoCancelada,
            "A consulta foi cancelada.");
    }

    /// <summary>
    /// Erros do Catálogo de Sistemas (research.md R6). Nenhuma mensagem recebe ou mostra o token do GitLab.
    /// </summary>
    public static class Catalogo
    {
        public const string CodigoNaoConfigurado = "catalogo.nao-configurado";

        public const string CodigoAcessoRecusado = "catalogo.acesso-recusado";

        public const string CodigoArquivoNaoEncontrado = "catalogo.arquivo-nao-encontrado";

        public const string CodigoTempoEsgotado = "catalogo.tempo-esgotado";

        public const string CodigoIndisponivel = "catalogo.indisponivel";

        public const string CodigoFormatoInvalido = "catalogo.formato-invalido";

        public const string CodigoCancelada = "catalogo.cancelada";

        public const string CodigoAcessoNegado = "catalogo.acesso-negado";

        public const string CodigoSistemaNaoEncontrado = "catalogo.sistema-nao-encontrado";

        /// <summary>Token, endereço ou demais opções de <c>CatalogoSistemas</c> ausentes ou inválidos.</summary>
        public static Erro NaoConfigurado => new(
            CodigoNaoConfigurado,
            "A origem do catálogo não está configurada.");

        /// <summary>401 do GitLab.</summary>
        public static Erro AcessoRecusado => new(
            CodigoAcessoRecusado,
            "O GitLab recusou o acesso ao catálogo (token inválido ou expirado).");

        /// <summary>403 ou 404 do GitLab.</summary>
        public static Erro ArquivoNaoEncontrado => new(
            CodigoArquivoNaoEncontrado,
            "O arquivo do catálogo não foi encontrado no GitLab.");

        public static Erro TempoEsgotado(int segundos) => new(
            CodigoTempoEsgotado,
            $"O GitLab não respondeu em {segundos} s.");

        /// <summary>Falha de rede ou resposta inesperada do GitLab.</summary>
        public static Erro Indisponivel(string motivo) => new(
            CodigoIndisponivel,
            $"O GitLab não está disponível: {motivo}");

        public static Erro FormatoInvalido(string motivo, int linha, int coluna) => new(
            CodigoFormatoInvalido,
            $"O sistemasDb.js não pôde ser lido: {motivo} (linha {linha}, coluna {coluna}).");

        /// <summary>Quem pediu a leitura cancelou; não é falha da origem.</summary>
        public static Erro Cancelada => new(
            CodigoCancelada,
            "A leitura do catálogo foi cancelada.");

        public static Erro AcessoNegado => new(
            CodigoAcessoNegado,
            "Você não tem acesso ao catálogo de sistemas.");

        /// <summary>O nome pedido na ficha não está no catálogo (specs/004-ficha-sistema, FR-019).</summary>
        public static Erro SistemaNaoEncontrado(string nome) => new(
            CodigoSistemaNaoEncontrado,
            $"Não há item com o nome {nome} no catálogo. Ele pode ter sido renomeado ou removido.");
    }

    /// <summary>
    /// Erros das Versões Publicadas (specs/003-versoes-publicadas, data-model.md e contracts/origem-enderecos.md). Só a
    /// leitura da tabela de endereços gera erro da tela; o erro de um arquivo de versão fica na célula.
    /// </summary>
    public static class Versoes
    {
        public const string CodigoAcessoNegado = "versoes.acesso-negado";

        public const string CodigoTabelaIndisponivel = "versoes.tabela-indisponivel";

        public const string CodigoTempoEsgotado = "versoes.tempo-esgotado";

        public const string CodigoSemSistemasAtivos = "versoes.sem-sistemas-ativos";

        public const string CodigoCancelada = "versoes.cancelada";

        /// <summary>Motivo usado quando a tabela não existe ou a conta do SIAT não pode lê-la (208, 229, 230, 916, 927).</summary>
        public const string MotivoSemPermissao = "tabela inexistente ou sem permissão para a conta do SIAT";

        public static Erro AcessoNegado => new(
            CodigoAcessoNegado,
            "Você não tem acesso às versões publicadas.");

        /// <summary>Falha de conexão, permissão ou tabela inexistente em <c>Apps_VersionAddresses</c>.</summary>
        public static Erro TabelaIndisponivel(string servidor, string motivo) => new(
            CodigoTabelaIndisponivel,
            $"Não foi possível ler os endereços de versão em {servidor}: {motivo}");

        public static Erro TempoEsgotado(string servidor) => new(
            CodigoTempoEsgotado,
            $"A leitura dos endereços de versão em {servidor} excedeu o tempo limite.");

        /// <summary>A tabela não tem registro ativo: estado vazio da tela, não erro de leitura.</summary>
        public static Erro SemSistemasAtivos(string servidor) => new(
            CodigoSemSistemasAtivos,
            $"Nenhum sistema cadastrado em {servidor}.");

        /// <summary>Quem pediu a leitura cancelou; não é falha da origem.</summary>
        public static Erro Cancelada => new(
            CodigoCancelada,
            "A leitura das versões foi cancelada.");
    }

    /// <summary>
    /// Erros do Copilot (specs/005-copilot-meu-consumo/research.md R9, contracts/github-api.md). Nenhuma mensagem
    /// recebe ou mostra o token de billing, o segredo da OAuth App, o código de autorização nem o corpo das respostas
    /// de erro do GitHub.
    /// </summary>
    public static class Copilot
    {
        public const string CodigoAcessoNegado = "copilot.acesso-negado";

        public const string CodigoNaoConfigurado = "copilot.nao-configurado";

        public const string CodigoSemConexao = "copilot.sem-conexao";

        public const string CodigoNaoMembro = "copilot.nao-membro";

        public const string CodigoSemLicencaNemLimite = "copilot.sem-licenca-nem-limite";

        public const string CodigoPeriodoInvalido = "copilot.periodo-invalido";

        public const string CodigoTokenRecusado = "copilot.token-recusado";

        public const string CodigoAcessoRecusadoPeloGitHub = "copilot.acesso-recusado-pelo-github";

        public const string CodigoNaoEncontrado = "copilot.nao-encontrado";

        public const string CodigoLimiteDeRequisicoes = "copilot.limite-de-requisicoes";

        public const string CodigoGitHubIndisponivel = "copilot.github-indisponivel";

        public const string CodigoRespostaInesperada = "copilot.resposta-inesperada";

        public const string CodigoCancelada = "copilot.cancelada";

        /// <summary>Primeira frase da mensagem de limite de requisições, sem o tempo para tentar de novo.</summary>
        public const string TextoLimiteDeRequisicoes = "Limite de chamadas do GitHub atingido.";

        public static Erro AcessoNegado => new(
            CodigoAcessoNegado,
            "Você não tem acesso ao consumo do Copilot.");

        /// <summary>Seção <c>Copilot</c> ausente ou incompleta: só esta página falha (Princípio IV).</summary>
        public static Erro NaoConfigurado => new(
            CodigoNaoConfigurado,
            "O módulo Copilot não está configurado no servidor.");

        /// <summary>O usuário do SIAT não tem conta do GitHub conectada neste navegador.</summary>
        public static Erro SemConexao => new(
            CodigoSemConexao,
            "Conecte sua conta do GitHub para ver o seu consumo.");

        /// <summary>A conta não é membro ativo da organização (404 na associação, outro estado ou outro id; FR-010).</summary>
        public static Erro NaoMembro(string login, string organizacao) => new(
            CodigoNaoMembro,
            $"A conta {login} do GitHub não é membro ativo da organização {organizacao}.");

        /// <summary>Sem licença e sem budget individual: nenhum relatório é lido (FR-021).</summary>
        public static Erro SemLicencaNemLimite(string organizacao) => new(
            CodigoSemLicencaNemLimite,
            $"Sua conta não tem licença Copilot nem limite individual na organização {organizacao}.");

        public static Erro PeriodoInvalido => new(
            CodigoPeriodoInvalido,
            "Selecione datas válidas, em ordem, num intervalo de até 92 dias entre o início dos 24 meses disponíveis e hoje (UTC).");

        /// <summary>401 do GitHub.</summary>
        public static Erro TokenRecusado => new(
            CodigoTokenRecusado,
            "O token de billing do GitHub não foi aceito. Verifique a configuração no servidor.");

        /// <summary>403 sem limite de requisições.</summary>
        public static Erro AcessoRecusadoPeloGitHub => new(
            CodigoAcessoRecusadoPeloGitHub,
            "O GitHub recusou o acesso. Verifique as permissões e a aprovação do token.");

        /// <summary>404 fora da associação.</summary>
        public static Erro NaoEncontrado => new(
            CodigoNaoEncontrado,
            "Organização não encontrada ou indisponível para a credencial.");

        /// <summary>
        /// 429, ou 403 com <c>x-ratelimit-remaining: 0</c> ou <c>retry-after</c> (FR-031). Com
        /// <paramref name="tenteEm"/>, a mensagem diz em quantos minutos tentar de novo, contados de
        /// <paramref name="agora"/> (o <see cref="Erro"/> só leva código e mensagem; por isso o tempo vai no texto).
        /// </summary>
        public static Erro LimiteDeRequisicoes(DateTimeOffset? tenteEm, DateTimeOffset agora)
        {
            if (tenteEm is not { } quando)
            {
                return new(CodigoLimiteDeRequisicoes, TextoLimiteDeRequisicoes);
            }

            var minutos = Math.Max(1, (int)Math.Ceiling((quando - agora).TotalMinutes));
            var unidade = minutos == 1 ? "minuto" : "minutos";
            return new(
                CodigoLimiteDeRequisicoes,
                $"{TextoLimiteDeRequisicoes} Tente de novo em {minutos.ToString(System.Globalization.CultureInfo.InvariantCulture)} {unidade}.");
        }

        /// <summary>5xx, tempo limite ou falha de rede.</summary>
        public static Erro GitHubIndisponivel => new(
            CodigoGitHubIndisponivel,
            "O GitHub não respondeu. Tente de novo.");

        /// <summary>JSON fora do formato, unidade de consumo desconhecida ou paginação incompleta.</summary>
        public static Erro RespostaInesperada => new(
            CodigoRespostaInesperada,
            "O GitHub retornou uma resposta inesperada.");

        /// <summary>Quem pediu a leitura cancelou; não é falha da origem e a página volta ao resultado anterior.</summary>
        public static Erro Cancelada => new(
            CodigoCancelada,
            "A leitura do consumo do Copilot foi cancelada.");
    }

    public static class UsuariosDeRede
    {
        public const string CodigoCadastroIndisponivel = "usuario-de-rede.cadastro-indisponivel";

        public static Erro CadastroIndisponivel(string motivo) => new(
            CodigoCadastroIndisponivel,
            $"Não foi possível consultar o vínculo do usuário de rede com a matrícula: {motivo}");
    }

    public static class Filtros
    {
        public const string CodigoQuantidadeNaoNumerica = "filtros.quantidade-nao-numerica";

        public const string CodigoTextoMuitoLongo = "filtros.texto-muito-longo";

        public const string CodigoLogUidInvalido = "filtros.loguid-invalido";

        public const string CodigoDiasNaoNumerico = "filtros.dias-nao-numerico";

        public const string CodigoDataInvalida = "filtros.data-invalida";

        public const string CodigoPeriodoInvertido = "filtros.periodo-invertido";

        public static Erro QuantidadeNaoNumerica => new(
            CodigoQuantidadeNaoNumerica,
            "Informe um número entre 1 e 1000 na quantidade.");

        public static Erro TextoMuitoLongo(int maximo) => new(
            CodigoTextoMuitoLongo,
            $"O texto de busca deve ter no máximo {maximo} caracteres.");

        public static Erro LogUidInvalido => new(
            CodigoLogUidInvalido,
            "Informe o início do LogUID só com dígitos hexadecimais (0-9, A-F) e hífens.");

        public static Erro DiasNaoNumerico => new(
            CodigoDiasNaoNumerico,
            "Informe um número de dias entre 1 e 3650.");

        public static Erro DataInvalida => new(
            CodigoDataInvalida,
            "Informe uma data válida.");

        public static Erro PeriodoInvertido => new(
            CodigoPeriodoInvertido,
            "A data inicial deve ser igual ou anterior à data final.");
    }
}
