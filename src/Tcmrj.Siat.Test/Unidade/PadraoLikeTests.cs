using AwesomeAssertions;
using Tcmrj.Siat.Infra.Conexoes;

namespace Tcmrj.Siat.Test.Unidade;

public class PadraoLikeTests
{
    [Fact]
    public void Escapa_colchete_percentual_e_sublinhado() =>
        PadraoLike.Montar("[teste]_%").Should().Be("%[[]teste][_][%]%");

    [Fact]
    public void Texto_simples_vira_contem() =>
        PadraoLike.Montar("texto").Should().Be("%texto%");
}
