using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Copilot;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Features.Copilot;

/// <summary>
/// A pessoa da conta conectada: perfil, licença, times e limite vigente (specs/005-copilot-meu-consumo, FR-001,
/// FR-004, FR-013 e FR-015; research.md R4). Exige <c>CopilotProprio.CanSelect</c> e a conexão antes de qualquer
/// leitura. A consulta não tem login: só existe a conta da conexão.
/// </summary>
public static class ObterPessoaCopilot
{
    /// <param name="Atualizar">"Atualizar agora": relê a associação, o perfil, as licenças, os budgets e os times.</param>
    public sealed record Query(bool Atualizar) : IRequest<Result<PessoaCopilotVM, Erro>>;

    public sealed class Handler(
        IAutorizacaoConsultaService autorizacao,
        IConexaoGitHubService conexoes,
        IConsumoCopilotService consumo,
        IOptionsMonitor<CacheDoCopilotOptions> opcoes) : IRequestHandler<Query, Result<PessoaCopilotVM, Erro>>
    {
        public async Task<Result<PessoaCopilotVM, Erro>> Handle(Query request, CancellationToken cancellationToken)
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
                var pessoa = await consumo.ObterPessoaAsync(conexao, request.Atualizar, cancellationToken);
                return pessoa.IsSuccess ? PessoaCopilotVM.De(pessoa.Value, opcoes.CurrentValue.Organizacao) : pessoa.Error;
            }
            catch (OperationCanceledException)
            {
                return Erros.Copilot.Cancelada;
            }
        }
    }
}
