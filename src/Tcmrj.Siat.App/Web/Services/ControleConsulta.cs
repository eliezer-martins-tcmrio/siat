using CSharpFunctionalExtensions;
using Tcmrj.Siat.Domain.Models;

namespace Tcmrj.Siat.App.Web.Services;

/// <summary>Estado da tela de consulta (data-model, Estado da tela).</summary>
public enum EstadoConsulta
{
    Ociosa,
    Consultando,
    ComResultado,
    ComErro,
}

/// <summary>Uma tentativa de consulta iniciada por <see cref="ControleConsulta{TResultado}.Iniciar"/>.</summary>
public sealed class TentativaConsulta
{
    internal TentativaConsulta(CancellationTokenSource fonte, long inicio)
    {
        Fonte = fonte;
        Token = fonte.Token;
        Inicio = inicio;
    }

    /// <summary>Token que chega ao SqlClient: cancelado por Cancelar, por uma nova consulta ou ao fechar a página.</summary>
    public CancellationToken Token { get; }

    /// <summary>Início pelo <see cref="TimeProvider.GetTimestamp"/>, para o tempo decorrido.</summary>
    public long Inicio { get; }

    internal CancellationTokenSource Fonte { get; }
}

/// <summary>
/// Consulta de uma página (história 4, T099): uma por vez, cada uma com o seu <see cref="CancellationTokenSource"/>.
/// <list type="bullet">
/// <item>Nova consulta cancela a anterior, e só a última é aplicada (<see cref="Concluir"/> devolve falso para as outras).</item>
/// <item><see cref="Cancelar"/> interrompe a consulta no banco; ao concluir cancelada, volta ao estado anterior e o
/// resultado anterior continua valendo.</item>
/// <item>Acesso negado ou servidor fora da lista descartam o resultado anterior (nenhum dado fica visível).</item>
/// <item><see cref="Dispose"/> (página fechada) cancela a consulta em andamento e ignora o que chegar depois.</item>
/// </list>
/// Não é seguro para uso concorrente: use no contexto de sincronização do circuito.
/// </summary>
public sealed class ControleConsulta<TResultado>(TimeProvider relogio) : IDisposable
    where TResultado : class
{
    private TentativaConsulta? atual;
    private bool descartado;

    public EstadoConsulta Estado { get; private set; } = EstadoConsulta.Ociosa;

    /// <summary>Último resultado aplicado (continua valendo depois de cancelar ou de um erro do servidor).</summary>
    public TResultado? Resultado { get; private set; }

    /// <summary>Erro da última consulta concluída, ou nulo.</summary>
    public Erro? Erro { get; private set; }

    /// <summary>Verdadeiro quando a última consulta concluída foi cancelada.</summary>
    public bool Cancelada { get; private set; }

    /// <summary>Duração da última consulta concluída.</summary>
    public TimeSpan Duracao { get; private set; }

    /// <summary>Início da consulta em andamento (para o tempo decorrido), ou nulo.</summary>
    public long? InicioAtual => atual?.Inicio;

    public bool EmAndamento => atual is not null;

    /// <summary>Inicia uma consulta, cancelando a que estiver em andamento.</summary>
    public TentativaConsulta Iniciar()
    {
        ObjectDisposedException.ThrowIf(descartado, this);

        var anterior = atual;
        atual = new TentativaConsulta(new CancellationTokenSource(), relogio.GetTimestamp());
        anterior?.Fonte.Cancel();

        Estado = EstadoConsulta.Consultando;
        Erro = null;
        Cancelada = false;
        return atual;
    }

    /// <summary>Cancela a consulta em andamento (botão Cancelar). Sem consulta em andamento, não faz nada.</summary>
    public void Cancelar() => atual?.Fonte.Cancel();

    /// <summary>
    /// Aplica o resultado de <paramref name="tentativa"/>. Devolve falso, sem mudar nada, quando ela foi substituída por
    /// outra ou quando o controle já foi descartado.
    /// </summary>
    public bool Concluir(TentativaConsulta tentativa, Result<TResultado, Erro> obtido)
    {
        ArgumentNullException.ThrowIfNull(tentativa);

        try
        {
            if (descartado || !ReferenceEquals(atual, tentativa))
            {
                return false;
            }

            atual = null;
            Duracao = relogio.GetElapsedTime(tentativa.Inicio);

            if (obtido.IsSuccess)
            {
                Resultado = obtido.Value;
                Estado = EstadoConsulta.ComResultado;
                return true;
            }

            if (obtido.Error.Code == Erros.Consultas.CodigoCancelada || tentativa.Token.IsCancellationRequested)
            {
                // Cancelar volta ao estado anterior: o resultado anterior continua na tela.
                Cancelada = true;
                Estado = Resultado is null ? EstadoConsulta.Ociosa : EstadoConsulta.ComResultado;
                return true;
            }

            // Acesso negado ou servidor fora da lista: nenhum dado anterior fica visível.
            if (obtido.Error.Code is Erros.Consultas.CodigoAcessoNegado or Erros.Consultas.CodigoServidorNaoEncontrado)
            {
                Resultado = null;
            }

            Erro = obtido.Error;
            Estado = EstadoConsulta.ComErro;
            return true;
        }
        finally
        {
            tentativa.Fonte.Dispose();
        }
    }

    public void Dispose()
    {
        if (descartado)
        {
            return;
        }

        descartado = true;
        atual?.Fonte.Cancel();
    }
}
