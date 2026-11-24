using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Repositories;

namespace Tcmrj.Siat.Infra.Configurations;

public static class CatalogoSistemasConfiguration
{
    /// <summary>Tamanho máximo aceito do <c>sistemasDb.js</c> (o atual tem dezenas de KB).</summary>
    public const long TamanhoMaximoDoArquivo = 5 * 1024 * 1024;

    /// <summary>
    /// Liga <see cref="CatalogoSistemasOptions"/> à seção <c>CatalogoSistemas</c>, sem validar na subida (a validação é
    /// na leitura, Princípio IV), e registra o repositório com um <see cref="HttpClient"/> tipado: tempo limite de
    /// <see cref="CatalogoSistemasOptions.TempoLimiteSegundos"/> e sem seguir redirecionamentos, para o cabeçalho com o
    /// token nunca ser reenviado a outro endereço.
    /// </summary>
    public static IServiceCollection AddCatalogoSistemas(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CatalogoSistemasOptions>(configuration.GetSection(CatalogoSistemasOptions.Secao));

        services.AddHttpClient<ICatalogoSistemasRepository, CatalogoSistemasRepository>((provider, client) =>
            {
                var segundos = provider.GetRequiredService<IOptionsMonitor<CatalogoSistemasOptions>>().CurrentValue.TempoLimiteSegundos;

                // Fora do intervalo, o repositório devolve NaoConfigurado antes de qualquer chamada.
                if (segundos is >= CatalogoSistemasOptions.Minimo and <= CatalogoSistemasOptions.Maximo)
                {
                    client.Timeout = TimeSpan.FromSeconds(segundos);
                }

                client.MaxResponseContentBufferSize = TamanhoMaximoDoArquivo;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

        return services;
    }
}
