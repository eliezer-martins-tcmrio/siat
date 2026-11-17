using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Repositories;

namespace Tcmrj.Siat.Infra.Configurations;

public static class ServidoresConfiguration
{
    /// <summary>Adiciona o <c>servidores.json</c> (opcional, com recarga automática) à raiz da configuração.</summary>
    public static IConfigurationBuilder AddServidoresJson(this IConfigurationBuilder builder, string caminho = "servidores.json") =>
        builder.AddJsonFile(caminho, optional: true, reloadOnChange: true);

    /// <summary>Liga <see cref="ServidoresOptions"/> à raiz da configuração e registra o repositório.</summary>
    public static IServiceCollection AddServidores(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServidoresOptions>(configuration);
        services.AddSingleton<IServidoresRepository, ServidoresRepository>();
        return services;
    }
}
