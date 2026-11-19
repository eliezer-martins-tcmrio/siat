using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Tcmrj.Autenticacao.AspNetCore.ValueObjects;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Web.Configurations;

namespace Tcmrj.Siat.Web.Services;

/// <summary>
/// Liga o usuário de rede (<c>DOMINIO\login</c>, da autenticação Windows) à matrícula pela view do RH e acrescenta as
/// claims que o <c>Tcmrj.Autorizacao.App</c> e o <c>ITcmrjContext</c> leem: <c>matricula</c>, <c>userId</c>,
/// <c>nome</c> e <c>email</c>. As permissões continuam sendo as do UserManager, verificadas a cada uso.
/// Sem vínculo único com usuário habilitado, o usuário fica sem matrícula (todas as políticas negam) e a
/// situação vai na claim <see cref="VinculoDeRede.TipoClaim"/>.
/// </summary>
public sealed partial class ClaimsDoUsuarioDeRede(
    IUsuariosDeRedeRepository repositorio,
    IMemoryCache cache,
    IOptions<UsuarioDeRedeOptions> opcoes,
    ILogger<ClaimsDoUsuarioDeRede> logger) : IClaimsTransformation
{
    internal const string TipoAutenticacao = "SIAT.UsuarioDeRede";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // O ASP.NET Core chama a transformação a cada AuthenticateAsync, às vezes mais de uma vez por requisição:
        // usuário já transformado (com matrícula ou com a situação do vínculo) passa direto.
        if (principal.Identity is not { IsAuthenticated: true, Name: { Length: > 0 } nome }
            || principal.HasClaim(c => c.Type is TcmClaimTypes.Matricula or VinculoDeRede.TipoClaim))
        {
            return principal;
        }

        var login = LoginNoDominio(nome);
        if (login is null)
        {
            LogDominioInvalido(logger, nome, opcoes.Value.Dominio);
            return ComSituacao(principal, VinculoDeRede.DominioInvalido);
        }

        var chave = "usuario-de-rede:" + login.ToUpperInvariant();
        if (cache.TryGetValue(chave, out UsuarioDeRede? emCache) && emCache is not null)
        {
            return ComUsuario(principal, emCache);
        }

        // A interface não recebe CancellationToken; o tempo limite fica no repositório.
        var resultado = await repositorio.ObterPorLoginAsync(login, CancellationToken.None);
        if (resultado.IsFailure)
        {
            LogCadastroIndisponivel(logger, nome, resultado.Error.Message);
            return ComSituacao(principal, VinculoDeRede.Indisponivel);
        }

        var vinculos = resultado.Value;
        if (vinculos.Count == 0)
        {
            LogNaoEncontrado(logger, nome);
            return ComSituacao(principal, VinculoDeRede.NaoEncontrado);
        }

        if (vinculos.Count > 1)
        {
            // Nunca escolhe uma das matrículas: o erro no cadastro do RH daria a uma pessoa o acesso de outra.
            LogDuplicado(logger, nome);
            return ComSituacao(principal, VinculoDeRede.Duplicado);
        }

        var usuario = vinculos[0];
        if (!usuario.Habilitado)
        {
            LogUsuarioDesabilitado(logger, nome, usuario.Matricula);
            return ComSituacao(principal, VinculoDeRede.UsuarioDesabilitado);
        }

        // Só o vínculo encontrado fica em memória: quem acabou de ser cadastrado entra ao recarregar a página.
        cache.Set(chave, usuario, TimeSpan.FromMinutes(opcoes.Value.MinutosDeCache));
        LogVinculado(logger, nome, usuario.Matricula);
        return ComUsuario(principal, usuario);
    }

    /// <summary>O login sem o domínio, quando o nome é <c>DOMINIO\login</c> do domínio configurado; senão nulo.</summary>
    private string? LoginNoDominio(string nome)
    {
        var separador = nome.IndexOf('\\', StringComparison.Ordinal);
        if (separador <= 0 || separador == nome.Length - 1)
        {
            return null;
        }

        var dominio = nome[..separador];
        var login = nome[(separador + 1)..];
        return dominio.Equals(opcoes.Value.Dominio, StringComparison.OrdinalIgnoreCase) && !login.Contains('\\', StringComparison.Ordinal)
            ? login
            : null;
    }

    private static ClaimsPrincipal ComUsuario(ClaimsPrincipal principal, UsuarioDeRede usuario)
    {
        var matricula = usuario.Matricula.ToString(CultureInfo.InvariantCulture);
        return ComIdentidade(principal, new ClaimsIdentity(
            [
                new Claim(TcmClaimTypes.Matricula, matricula),
                // No UserManager o Id do usuário é a matrícula (o SIA grava as duas claims com o mesmo valor).
                new Claim(TcmClaimTypes.UserId, matricula),
                new Claim(TcmClaimTypes.Nome, usuario.Nome ?? string.Empty),
                new Claim(TcmClaimTypes.Email, usuario.Email ?? string.Empty),
            ],
            TipoAutenticacao));
    }

    private static ClaimsPrincipal ComSituacao(ClaimsPrincipal principal, string situacao)
        => ComIdentidade(principal, new ClaimsIdentity([new Claim(VinculoDeRede.TipoClaim, situacao)]));

    // Uma identidade a mais, sem mexer na do Windows: o nome e os grupos do AD continuam disponíveis.
    private static ClaimsPrincipal ComIdentidade(ClaimsPrincipal principal, ClaimsIdentity identidade)
    {
        var transformado = principal.Clone();
        transformado.AddIdentity(identidade);
        return transformado;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuário de rede {UsuarioDeRede} vinculado à matrícula {Matricula}.")]
    private static partial void LogVinculado(ILogger logger, string usuarioDeRede, int matricula);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Usuário de rede {UsuarioDeRede} fora do domínio {Dominio}: sem vínculo.")]
    private static partial void LogDominioInvalido(ILogger logger, string usuarioDeRede, string dominio);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Usuário de rede {UsuarioDeRede} sem matrícula ativa no cadastro do RH.")]
    private static partial void LogNaoEncontrado(ILogger logger, string usuarioDeRede);

    [LoggerMessage(Level = LogLevel.Error, Message = "Usuário de rede {UsuarioDeRede} ligado a mais de uma matrícula ativa no cadastro do RH: acesso negado.")]
    private static partial void LogDuplicado(ILogger logger, string usuarioDeRede);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Usuário de rede {UsuarioDeRede} ligado à matrícula {Matricula}, sem usuário habilitado no UserManager.")]
    private static partial void LogUsuarioDesabilitado(ILogger logger, string usuarioDeRede, int matricula);

    [LoggerMessage(Level = LogLevel.Error, Message = "Vínculo do usuário de rede {UsuarioDeRede} não consultado: {Motivo}")]
    private static partial void LogCadastroIndisponivel(ILogger logger, string usuarioDeRede, string motivo);
}
