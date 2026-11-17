using System.Security.Claims;
using AwesomeAssertions;
using Tcmrj.Autenticacao.AspNetCore.Extensions;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>As claims do SIA são lidas pela biblioteca, com os nomes que chegam sem MapInboundClaims.</summary>
public class ClaimsDoSiaTests
{
    [Fact]
    public void ClaimsDoSiaSaoLidasPelaBiblioteca()
    {
        var usuario = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("email", "a@tcmrio.rj.gov.br"),
                new Claim("matricula", "123"),
                new Claim("sessionId", "42"),
            ],
            authenticationType: "oidc"));

        usuario.GetUserEmail().Should().Be("a@tcmrio.rj.gov.br");
        usuario.GetMatricula().Should().Be(123);
        usuario.GetSessionId().Should().Be(42L);
    }

    [Fact]
    public void NomeAusenteVoltaNulo()
    {
        // Quem mostra o nome (rodapé do menu, registro de consulta) precisa tratar o nulo.
        var usuario = new ClaimsPrincipal(new ClaimsIdentity([], authenticationType: "oidc"));

        usuario.GetUserName().Should().BeNull();
    }
}
