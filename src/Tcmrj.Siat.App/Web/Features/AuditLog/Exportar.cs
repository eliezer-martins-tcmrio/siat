using System.Globalization;
using CSharpFunctionalExtensions;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.AuditLog;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Features.AuditLog;

/// <summary>
/// Exportação do AuditLog em HTML autocontido (história 4, FR-021, [contracts/exportacao-html.md]).
/// Autoriza de novo pelo servidor da consulta (constituição, Princípio III) antes de montar o arquivo; negado, não
/// gera arquivo. Monta o arquivo com os registros já carregados na tela (não consulta o banco de novo), com stack
/// trace e LogEvent formatados, e registra a tentativa como <see cref="OperacaoConsulta.Exportacao"/>.
/// </summary>
public static class Exportar
{
    /// <param name="Servidor">Chave do servidor do resultado exibido.</param>
    /// <param name="AppId">Aplicação usada na consulta; nulo = todas.</param>
    /// <param name="PrefixoLogId">Início do LogID usado na consulta; nulo = sem filtro.</param>
    /// <param name="Quantidade">Quantidade usada na consulta.</param>
    /// <param name="FiltroLocal">Filtro local aplicado na tela; vem preenchido no arquivo.</param>
    /// <param name="Itens">Registros já carregados, na ordem da tela.</param>
    /// <param name="NomesAplicacoes">Nomes das aplicações conhecidas (lista do <c>CSLIB_UserManager</c>); nulo ou vazio quando indisponível.</param>
    public sealed record Query(
        string Servidor,
        int? AppId,
        string? PrefixoLogId,
        int Quantidade,
        string? FiltroLocal,
        IReadOnlyList<RegistroAuditoriaVM> Itens,
        IReadOnlyDictionary<int, string>? NomesAplicacoes = null) : IRequest<Result<ArquivoExportado, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        IServidoresRepository servidores,
        IExportacaoHtmlService exportacao,
        IRegistroConsultaService registro,
        IUsuarioAtualService usuarioAtual,
        TimeProvider relogio) : IRequestHandler<Query, Result<ArquivoExportado, Erro>>
    {
        public async Task<Result<ArquivoExportado, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Itens);

            if (string.IsNullOrWhiteSpace(request.Servidor))
            {
                return Erros.Consultas.ServidorNaoEncontrado;
            }

            var chave = request.Servidor.Trim();
            var prefixo = string.IsNullOrWhiteSpace(request.PrefixoLogId) ? null : request.PrefixoLogId.Trim();
            var filtros = new Pesquisar.FiltrosRegistrados(request.AppId, prefixo, request.Quantidade);

            // Toda operação é verificada no servidor: a permissão pode ter sido retirada depois da consulta.
            var autorizado = await autorizacao.AutorizarAsync(Modulo.AuditLog, chave);
            if (autorizado.IsFailure)
            {
                var resultadoNegado = autorizado.Error.Code == Erros.Consultas.CodigoAcessoNegado
                    ? ResultadoRegistro.AcessoNegado
                    : ResultadoRegistro.Erro;
                await Registrar(chave, servidores.ObterPorChave(chave), filtros, null, resultadoNegado);
                return autorizado.Error;
            }

            var servidor = autorizado.Value;
            var nomes = request.NomesAplicacoes ?? new Dictionary<int, string>();
            var usuario = await usuarioAtual.ObterAsync();
            var cabecalho = new CabecalhoExportacao(
                Modulo.AuditLog,
                ServidorVM.De(servidor),
                [
                    new("Aplicação", DescreverAplicacao(request.AppId, nomes)),
                    new("LogID começa com", prefixo ?? "(sem filtro)"),
                    new("Quantidade", request.Quantidade.ToString(CultureInfo.InvariantCulture)),
                ],
                relogio.GetLocalNow().DateTime,
                ExportacaoHtmlService.Autor(usuario),
                request.FiltroLocal);

            var arquivo = exportacao.AuditLog(cabecalho, request.Itens, nomes);
            await Registrar(chave, servidor, filtros, request.Itens.Count, ResultadoRegistro.Sucesso);
            return arquivo;
        }

        private static string DescreverAplicacao(int? appId, IReadOnlyDictionary<int, string> nomes)
        {
            if (appId is not int id)
            {
                return "Todas as aplicações";
            }

            var texto = id.ToString(CultureInfo.InvariantCulture);
            return nomes.TryGetValue(id, out var nome) ? $"{nome} (AppID {texto})" : $"AppID {texto}";
        }

        private Task Registrar(string chave, Servidor? servidor, Pesquisar.FiltrosRegistrados filtros, int? itens, ResultadoRegistro resultado) =>
            registro.RegistrarAsync(new RegistroConsulta(
                OperacaoConsulta.Exportacao,
                Modulo.AuditLog,
                chave,
                servidor?.Nome,
                servidor?.Ambiente,
                filtros,
                itens,
                resultado));
    }
}
