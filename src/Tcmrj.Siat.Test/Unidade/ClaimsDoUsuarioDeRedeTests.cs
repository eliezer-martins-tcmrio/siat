using System.Security.Claims;
using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Tcmrj.Autenticacao.AspNetCore.Extensions;
using Tcmrj.Siat.Domain.Models;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Web.Configurations;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// PROTÓTIPO (features/prototipo_login_rede): DOMINIO\login vira matrícula pela view do RH; sem vínculo único com
/// usuário habilitado, o usuário fica sem matrícula (sem nenhuma permissão) e com a situação para a tela.
/// </summary>
public sealed class ClaimsDoUsuarioDeRedeTests : IDisposable
{
    private static readonly UsuarioDeRede Fulano = new(12345, "FULANO DE TAL", "fulano@tcmrio.tc.br", Habilitado: true);

    private readonly Mock<IUsuariosDeRedeRepository> _repositorio = new(MockBehavior.Strict);
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ClaimsDoUsuarioDeRede _transformacao;

    public ClaimsDoUsuarioDeRedeTests()
    {
        _transformacao = new ClaimsDoUsuarioDeRede(
            _repositorio.Object,
            _cache,
            Options.Create(new UsuarioDeRedeOptions { Dominio = "TCMNET", MinutosDeCache = 5 }),
            NullLogger<ClaimsDoUsuarioDeRede>.Instance);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task Vinculo_unico_e_habilitado_acrescenta_as_claims_lidas_pelo_pacote_de_autorizacao()
    {
        Responder("fulano", Fulano);

        var usuario = await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\fulano"));

        usuario.GetMatricula().Should().Be(12345);
        usuario.GetUserId().Should().Be(12345);
        usuario.GetUserName().Should().Be("FULANO DE TAL");
        usuario.GetUserEmail().Should().Be("fulano@tcmrio.tc.br");
        VinculoDeRede.Situacao(usuario).Should().BeNull();
    }

    [Fact]
    public async Task Identidade_do_Windows_continua_sendo_a_principal()
    {
        Responder("fulano", Fulano);

        var usuario = await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\fulano", grupo: @"TCMNET\Desenvolvimento"));

        usuario.Identity!.Name.Should().Be(@"TCMNET\fulano");
        usuario.Identity.IsAuthenticated.Should().BeTrue();
        usuario.IsInRole(@"TCMNET\Desenvolvimento").Should().BeTrue();
    }

    [Fact]
    public async Task Usuario_anonimo_nao_e_consultado_nem_alterado()
    {
        var anonimo = new ClaimsPrincipal(new ClaimsIdentity());

        var usuario = await _transformacao.TransformAsync(anonimo);

        usuario.Should().BeSameAs(anonimo);
        _repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Usuario_ja_transformado_passa_direto()
    {
        Responder("fulano", Fulano);
        var primeira = await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\fulano"));

        var segunda = await _transformacao.TransformAsync(primeira);

        segunda.Should().BeSameAs(primeira);
        segunda.FindAll("matricula").Should().ContainSingle();
    }

    [Theory]
    [InlineData(@"OUTRO\fulano")]
    [InlineData("fulano")]
    [InlineData("fulano@tcmnet.tcm")]
    [InlineData(@"TCMNET\")]
    [InlineData(@"\fulano")]
    [InlineData(@"TCMNET\a\b")]
    public async Task Login_fora_do_dominio_configurado_nao_e_consultado(string nome)
    {
        var usuario = await _transformacao.TransformAsync(UsuarioWindows(nome));

        usuario.GetMatricula().Should().BeNull();
        VinculoDeRede.Situacao(usuario).Should().Be(VinculoDeRede.DominioInvalido);
        _repositorio.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Dominio_e_comparado_sem_diferenciar_maiusculas()
    {
        Responder("fulano", Fulano);

        var usuario = await _transformacao.TransformAsync(UsuarioWindows(@"tcmnet\fulano"));

        usuario.GetMatricula().Should().Be(12345);
    }

    [Fact]
    public async Task Sem_matricula_ativa_no_RH_fica_sem_matricula()
    {
        Responder("fulano");

        var usuario = await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\fulano"));

        usuario.GetMatricula().Should().BeNull();
        VinculoDeRede.Situacao(usuario).Should().Be(VinculoDeRede.NaoEncontrado);
    }

    [Fact]
    public async Task Mais_de_uma_matricula_ativa_nao_escolhe_nenhuma()
    {
        Responder("fulano", Fulano, Fulano with { Matricula = 67890 });

        var usuario = await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\fulano"));

        usuario.GetMatricula().Should().BeNull();
        VinculoDeRede.Situacao(usuario).Should().Be(VinculoDeRede.Duplicado);
    }

    [Fact]
    public async Task Matricula_sem_usuario_habilitado_no_UserManager_fica_sem_matricula()
    {
        Responder("fulano", Fulano with { Habilitado = false });

        var usuario = await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\fulano"));

        usuario.GetMatricula().Should().BeNull();
        VinculoDeRede.Situacao(usuario).Should().Be(VinculoDeRede.UsuarioDesabilitado);
    }

    [Fact]
    public async Task Cadastro_indisponivel_fica_sem_matricula()
    {
        _repositorio.Setup(r => r.ObterPorLoginAsync("fulano", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Erros.UsuariosDeRede.CadastroIndisponivel("tempo esgotado"));

        var usuario = await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\fulano"));

        usuario.GetMatricula().Should().BeNull();
        VinculoDeRede.Situacao(usuario).Should().Be(VinculoDeRede.Indisponivel);
    }

    [Fact]
    public async Task Vinculo_encontrado_fica_em_cache_sem_diferenciar_maiusculas_no_login()
    {
        Responder("fulano", Fulano);
        await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\fulano"));

        var usuario = await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\FULANO"));

        usuario.GetMatricula().Should().Be(12345);
        _repositorio.Verify(r => r.ObterPorLoginAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Vinculo_nao_encontrado_nao_fica_em_cache()
    {
        // Quem acabou de ser corrigido no RH entra ao recarregar a página, sem esperar o cache expirar.
        Responder("fulano");
        await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\fulano"));
        Responder("fulano", Fulano);

        var usuario = await _transformacao.TransformAsync(UsuarioWindows(@"TCMNET\fulano"));

        usuario.GetMatricula().Should().Be(12345);
        _repositorio.Verify(r => r.ObterPorLoginAsync("fulano", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private void Responder(string login, params UsuarioDeRede[] vinculos)
        => _repositorio.Setup(r => r.ObterPorLoginAsync(login, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<UsuarioDeRede>, Erro>(vinculos));

    // Como o Negotiate entrega o usuário: identidade autenticada, nome DOMINIO\login e os grupos como papéis.
    private static ClaimsPrincipal UsuarioWindows(string nome, string? grupo = null)
    {
        List<Claim> claims = [new(ClaimTypes.Name, nome)];
        if (grupo is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, grupo));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Negotiate"));
    }
}
