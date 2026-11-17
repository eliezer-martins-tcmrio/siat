using Microsoft.AspNetCore.Authorization;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.App.Web.ViewModels.Shared;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Extensions;
using Tcmrj.Siat.Domain.Repositories;

namespace Tcmrj.Siat.App.Web.Features.Servidores;

/// <summary>Servidores que o usuário pode consultar no módulo, agrupados por ambiente (DSV, HML, PRD).</summary>
public static class ListarPermitidos
{
    public sealed record Query(Modulo Modulo) : IRequest<IReadOnlyList<GrupoServidoresVM>>;

    public sealed class Handler(
        IServidoresRepository servidores,
        IAuthorizationService autorizacao,
        IUsuarioAtualService usuarioAtual) : IRequestHandler<Query, IReadOnlyList<GrupoServidoresVM>>
    {
        private static readonly Ambiente[] Ordem = [Ambiente.Dsv, Ambiente.Hml, Ambiente.Prd];

        public async Task<IReadOnlyList<GrupoServidoresVM>> Handle(Query request, CancellationToken cancellationToken)
        {
            var lista = servidores.Listar();
            var usuario = await usuarioAtual.ObterAsync();

            var grupos = new List<GrupoServidoresVM>();
            foreach (var ambiente in Ordem)
            {
                var doAmbiente = lista.Where(s => s.Ambiente == ambiente).Select(ServidorVM.De).ToList();
                if (doAmbiente.Count == 0)
                {
                    continue;
                }

                var permitido = await autorizacao.AuthorizeAsync(usuario, request.Modulo.PoliticaPara(ambiente));
                if (permitido.Succeeded)
                {
                    grupos.Add(new GrupoServidoresVM(ambiente, doAmbiente));
                }
            }

            return grupos;
        }
    }
}
