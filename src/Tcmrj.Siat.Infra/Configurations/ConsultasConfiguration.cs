using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tcmrj.Siat.Infra.Conexoes;

namespace Tcmrj.Siat.Infra.Configurations;

public static class ConsultasConfiguration
{
    public static IServiceCollection AddConsultas(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConsultasOptions>(configuration.GetSection(ConsultasOptions.Secao));
        services.AddSingleton<IFabricaDeConexao, FabricaDeConexao>();
        return services;
    }
}
