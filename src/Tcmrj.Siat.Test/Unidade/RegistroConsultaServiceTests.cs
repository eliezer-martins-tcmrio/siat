using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Domain.Enums;

namespace Tcmrj.Siat.Test.Unidade;

/// <summary>
/// O que chega ao logger (e, por ele, ao DBLog): um evento por tentativa, TypeID 10 e as propriedades do contrato
/// (contracts/registro-consulta.md). O Serilog junta as propriedades do modelo da mensagem com as dos escopos.
/// </summary>
public class RegistroConsultaServiceTests
{
    private static readonly string[] PropriedadesDoContrato =
        ["Operacao", "Modulo", "Servidor", "ServidorNome", "Ambiente", "Matricula", "Usuario", "Filtros", "Itens", "Resultado"];

    private static RegistroConsulta Registro(ResultadoRegistro resultado, int? itens = 10, object? filtros = null, OperacaoConsulta operacao = OperacaoConsulta.Consulta) =>
        new(operacao, Modulo.MailQueue, "tcmbd4-hml", @"TCMBD4\HML", Ambiente.Hml,
            filtros ?? new { SmtpId = 3, Texto = "boleto", Quantidade = 100 }, itens, resultado);

    private static (RegistroConsultaService Servico, LoggerGravador Logger) Criar()
    {
        var logger = new LoggerGravador();
        var usuario = new Mock<IUsuarioAtualService>();
        usuario.Setup(u => u.ObterAsync()).ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("matricula", "12345"), new Claim("nome", "Ana Souza")], "teste")));
        return (new RegistroConsultaService(usuario.Object, logger), logger);
    }

    [Theory]
    [InlineData(ResultadoRegistro.Sucesso, 10)]
    [InlineData(ResultadoRegistro.Cancelada, null)]
    [InlineData(ResultadoRegistro.Erro, null)]
    [InlineData(ResultadoRegistro.AcessoNegado, null)]
    public async Task Cada_resultado_gera_um_evento_TypeID_10_com_as_propriedades_do_contrato(ResultadoRegistro resultado, int? itens)
    {
        var (servico, logger) = Criar();

        await servico.RegistrarAsync(Registro(resultado, itens));

        var evento = logger.Eventos.Should().ContainSingle().Which;
        var p = evento.Propriedades;
        p.Keys.Should().Contain(PropriedadesDoContrato);
        p["TypeID"].Should().Be((byte)10);
        p["Operacao"].Should().Be("Consulta");
        p["Modulo"].Should().Be("MailQueue");
        p["Servidor"].Should().Be("tcmbd4-hml");
        p["ServidorNome"].Should().Be(@"TCMBD4\HML");
        p["Ambiente"].Should().Be("HML");
        p["Matricula"].Should().Be("12345");
        p["Usuario"].Should().Be("Ana Souza");
        p["Itens"].Should().Be(itens);
        p["Resultado"].Should().Be(resultado.ToString());
        p["Filtros"]!.ToString().Should().Contain("SmtpId = 3").And.Contain("Texto = boleto").And.Contain("Quantidade = 100");
        evento.Mensagem.Should().Be(@"Consulta MailQueue em TCMBD4\HML (HML) por 12345");
    }

    [Fact]
    public async Task Exportacao_leva_o_texto_acentuado_na_mensagem_e_Exportacao_na_propriedade()
    {
        var (servico, logger) = Criar();

        await servico.RegistrarAsync(Registro(ResultadoRegistro.Sucesso, 5, operacao: OperacaoConsulta.Exportacao));

        var evento = logger.Eventos.Should().ContainSingle().Which;
        evento.Mensagem.Should().StartWith("Exportação MailQueue");
        evento.Propriedades["Operacao"].Should().Be("Exportacao");
    }

    [Fact]
    public async Task Nome_do_servidor_ausente_usa_a_chave_e_ambiente_ausente_fica_vazio()
    {
        var (servico, logger) = Criar();

        await servico.RegistrarAsync(new RegistroConsulta(
            OperacaoConsulta.Consulta, Modulo.AuditLog, "desconhecido", null, null, new { }, null, ResultadoRegistro.Erro));

        var p = logger.Eventos.Should().ContainSingle().Which.Propriedades;
        p["ServidorNome"].Should().Be("desconhecido");
        p["Ambiente"].Should().Be(string.Empty);
        p["Modulo"].Should().Be("AuditLog");
    }

    [Fact]
    public async Task Registra_so_os_filtros_recebidos_sem_corpo_de_email_nem_conteudo_de_log()
    {
        var (servico, logger) = Criar();

        await servico.RegistrarAsync(Registro(ResultadoRegistro.Sucesso, 2, new { SmtpId = 3, Texto = "boleto", Quantidade = 100 }));

        var evento = logger.Eventos.Should().ContainSingle().Which;
        evento.Propriedades.Keys.Should().NotContain(["Corpo", "LogEvent", "Mensagem", "Excecao"]);
        evento.Propriedades.Keys.Should().BeEquivalentTo(PropriedadesDoContrato.Concat(["TypeID", "LogUID", "OperacaoTexto"]));
    }

    [Fact]
    public async Task Falha_do_logger_nao_se_propaga()
    {
        var logger = new Mock<ILogger<RegistroConsultaService>>();
        logger.Setup(l => l.Log(
                It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Throws(new InvalidOperationException("banco de log fora do ar"));
        var usuario = new Mock<IUsuarioAtualService>();
        usuario.Setup(u => u.ObterAsync()).ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity()));
        var servico = new RegistroConsultaService(usuario.Object, logger.Object);

        var acao = () => servico.RegistrarAsync(Registro(ResultadoRegistro.Sucesso));

        await acao.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Registro_nulo_e_recusado()
    {
        var (servico, _) = Criar();

        var acao = () => servico.RegistrarAsync(null!);

        await acao.Should().ThrowAsync<ArgumentNullException>();
    }

    private sealed record Evento(LogLevel Nivel, string Mensagem, IReadOnlyDictionary<string, object?> Propriedades);

    /// <summary>Logger que guarda o que recebe: a mensagem, as propriedades do modelo e as dos escopos abertos.</summary>
    private sealed class LoggerGravador : ILogger<RegistroConsultaService>
    {
        private readonly List<Evento> _eventos = [];
        private readonly List<IReadOnlyList<KeyValuePair<string, object?>>> _escopos = [];

        public IReadOnlyList<Evento> Eventos => _eventos;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            var itens = state is IEnumerable<KeyValuePair<string, object?>> pares ? pares.ToList() : [];
            _escopos.Add(itens);
            return new Fim(() => _escopos.Remove(itens));
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var propriedades = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var par in _escopos.SelectMany(e => e))
            {
                propriedades[par.Key.TrimStart('@')] = par.Value;
            }

            if (state is IEnumerable<KeyValuePair<string, object?>> modelo)
            {
                foreach (var par in modelo.Where(p => p.Key != "{OriginalFormat}"))
                {
                    propriedades[par.Key] = par.Value;
                }
            }

            _eventos.Add(new Evento(logLevel, formatter(state, exception), propriedades));
        }

        private sealed class Fim(Action acao) : IDisposable
        {
            public void Dispose() => acao();
        }
    }
}
