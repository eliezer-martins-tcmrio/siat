using System.Globalization;
using CSharpFunctionalExtensions;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.MailQueue;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Features.MailQueue;

/// <summary>
/// Exportação da MailQueue em HTML autocontido (história 4, FR-021, [contracts/exportacao-html.md]).
/// Autoriza de novo pelo servidor da consulta (constituição, Princípio III) antes de montar o arquivo; negado, não
/// gera arquivo. Monta o arquivo com os itens já carregados na tela (não consulta o banco de novo) e registra a
/// tentativa como <see cref="OperacaoConsulta.Exportacao"/> ([contracts/registro-consulta.md]).
/// </summary>
public static class Exportar
{
    /// <param name="Servidor">Chave do servidor do resultado exibido.</param>
    /// <param name="SmtpId">SMTP usado na consulta; nulo = todos.</param>
    /// <param name="SmtpRotulo">Rótulo do SMTP para o cabeçalho (por exemplo, "3 · RH"); nulo usa "SMTP N".</param>
    /// <param name="Texto">Texto procurado no banco, como digitado (aparado).</param>
    /// <param name="Quantidade">Quantidade usada na consulta.</param>
    /// <param name="FiltroLocal">Filtro local aplicado na tela; vem preenchido no arquivo.</param>
    /// <param name="Itens">Itens já carregados, na ordem da tela.</param>
    public sealed record Query(
        string Servidor,
        int? SmtpId,
        string? SmtpRotulo,
        string? Texto,
        int Quantidade,
        string? FiltroLocal,
        IReadOnlyList<MensagemVM> Itens) : IRequest<Result<ArquivoExportado, Erro>>;

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
            var texto = string.IsNullOrWhiteSpace(request.Texto) ? null : request.Texto.Trim();
            var filtros = new Pesquisar.FiltrosRegistrados(request.SmtpId, texto, request.Quantidade);

            // Toda operação é verificada no servidor: a permissão pode ter sido retirada depois da consulta.
            var autorizado = await autorizacao.AutorizarAsync(Modulo.MailQueue, chave);
            if (autorizado.IsFailure)
            {
                var resultadoNegado = autorizado.Error.Code == Erros.Consultas.CodigoAcessoNegado
                    ? ResultadoRegistro.AcessoNegado
                    : ResultadoRegistro.Erro;
                await Registrar(chave, servidores.ObterPorChave(chave), filtros, null, resultadoNegado);
                return autorizado.Error;
            }

            var servidor = autorizado.Value;
            var usuario = await usuarioAtual.ObterAsync();
            var cabecalho = new CabecalhoExportacao(
                Modulo.MailQueue,
                ServidorVM.De(servidor),
                [
                    new("SMTP", request.SmtpId is int id ? request.SmtpRotulo ?? $"SMTP {id.ToString(CultureInfo.InvariantCulture)}" : "Todos os SMTPs"),
                    new("Texto", texto is null ? "(sem texto)" : $"\"{texto}\""),
                    new("Quantidade", request.Quantidade.ToString(CultureInfo.InvariantCulture)),
                ],
                relogio.GetLocalNow().DateTime,
                ExportacaoHtmlService.Autor(usuario),
                request.FiltroLocal);

            var arquivo = exportacao.MailQueue(cabecalho, request.Itens);
            await Registrar(chave, servidor, filtros, request.Itens.Count, ResultadoRegistro.Sucesso);
            return arquivo;
        }

        private Task Registrar(string chave, Servidor? servidor, Pesquisar.FiltrosRegistrados filtros, int? itens, ResultadoRegistro resultado) =>
            registro.RegistrarAsync(new RegistroConsulta(
                OperacaoConsulta.Exportacao,
                Modulo.MailQueue,
                chave,
                servidor?.Nome,
                servidor?.Ambiente,
                filtros,
                itens,
                resultado));
    }
}
