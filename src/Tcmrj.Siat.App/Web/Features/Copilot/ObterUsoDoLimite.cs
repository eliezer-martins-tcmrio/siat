using System.Globalization;
using CSharpFunctionalExtensions;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Copilot;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Models.Copilot;

namespace Tcmrj.Siat.App.Web.Features.Copilot;

/// <summary>
/// Uso do limite no mês atual (specs/005-copilot-meu-consumo, FR-016): consumo bruto acumulado do mês (relatório
/// mensal, à parte do período) sobre o limite vigente da pessoa. Limite sem resposta, ambíguo ou ausente →
/// <see cref="FaixaDeUso.Indisponivel"/>.
/// </summary>
public static class ObterUsoDoLimite
{
    public sealed record Query(bool Atualizar) : IRequest<Result<UsoDoLimiteVM, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        IConexaoGitHubService conexoes,
        IConsumoCopilotService consumo,
        TimeProvider relogio) : IRequestHandler<Query, Result<UsoDoLimiteVM, Erro>>
    {
        public async Task<Result<UsoDoLimiteVM, Erro>> Handle(Query request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!await autorizacao.PodeAcessarModuloAsync(Modulo.CopilotProprio))
            {
                return Erros.Copilot.AcessoNegado;
            }

            if (await conexoes.ObterAsync() is not { } conexao)
            {
                return Erros.Copilot.SemConexao;
            }

            try
            {
                var pessoa = consumo.ObterPessoaAsync(conexao, request.Atualizar, cancellationToken);
                var mes = consumo.ObterMesAtualAsync(conexao, request.Atualizar, cancellationToken);
                await Task.WhenAll(pessoa, mes);

                if (pessoa.Result.IsFailure)
                {
                    return pessoa.Result.Error;
                }

                if (mes.Result.IsFailure)
                {
                    return mes.Result.Error;
                }

                var hoje = relogio.GetUtcNow().UtcDateTime;
                var bruto = mes.Result.Value.Total.ValorBruto;
                var limite = pessoa.Result.Value.Limite.IsSuccess ? pessoa.Result.Value.Limite.Value : null;
                var uso = UsoDoLimite.Calcular(bruto, limite, hoje.ToString("yyyy-MM", CultureInfo.InvariantCulture));
                return UsoDoLimiteVM.De(uso, bruto);
            }
            catch (OperationCanceledException)
            {
                return Erros.Copilot.Cancelada;
            }
        }
    }
}
