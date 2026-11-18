using AwesomeAssertions;
using Reqnroll;
using Tcmrj.Autorizacao.App.AspNetCore;
using Tcmrj.Siat.Domain.Enums;
using Tcmrj.Siat.Domain.Extensions;
using Tcmrj.UserManager.Domain.Models.Enums;

namespace Tcmrj.Siat.Test.Features.Steps;

[Binding]
public sealed class PermissoesSteps
{
    private Modulo _modulo;
    private string _politica = string.Empty;

    [Given(@"o módulo ""(.*)""")]
    public void DadoOModulo(string modulo) => _modulo = Enum.Parse<Modulo>(modulo);

    [When(@"calculo a política do módulo")]
    public void QuandoCalculoAPolitica() => _politica = _modulo.Politica();

    [Then(@"a política é ""(.*)""")]
    public void EntaoAPoliticaE(string politica) => _politica.Should().Be(politica);

    [Then(@"a política tem o formato do UserManager para o recurso ""(.*)""")]
    public void EntaoAPoliticaTemOFormato(string recurso) =>
        _politica.Should().Be(Politica.Para(recurso, AccessType.CanSelect));
}
