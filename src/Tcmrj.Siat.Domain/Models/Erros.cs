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
