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
