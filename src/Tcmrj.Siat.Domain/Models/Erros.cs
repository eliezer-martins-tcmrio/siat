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
            "Servidor não encontrado na lista");

        public static Erro ServidorIndisponivel(string servidor, string motivo) => new(
            CodigoServidorIndisponivel,
            $"Não foi possível conectar ao servidor {servidor}: {motivo}");

        public static Erro TempoEsgotado(string servidor) => new(
            CodigoTempoEsgotado,
            $"A consulta ao servidor {servidor} excedeu o tempo limite.");

        public static Erro BancoInacessivel(string servidor, string banco) => new(
            CodigoBancoInacessivel,
            $"O banco {banco} do servidor {servidor} não está acessível.");

        public static Erro Cancelada => new(
            CodigoCancelada,
            "A consulta foi cancelada.");
    }
}
