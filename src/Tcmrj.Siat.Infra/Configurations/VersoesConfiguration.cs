using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Repositories;
using Tcmrj.Siat.Infra.Versoes;

namespace Tcmrj.Siat.Infra.Configurations;

public static class VersoesConfiguration
{
    /// <summary>Redirecionamentos seguidos nos endereços <c>http</c>/<c>https</c> (research.md R3).</summary>
    public const int RedirecionamentosMaximos = 3;

    /// <summary>
    /// Liga <see cref="VersoesOptions"/> à seção <c>Versoes</c>, validada na subida (sem a seção, valem os padrões), e
    /// registra o repositório da tabela de endereços e o leitor de arquivos de versão com um <see cref="HttpClient"/>
    /// tipado: sem credenciais (<c>UseDefaultCredentials = false</c>), até 3 redirecionamentos e sem tempo limite próprio,
    /// porque o tempo limite por arquivo é do <c>LeitorDeVersoes</c>.
    /// </summary>
    public static IServiceCollection AddVersoes(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<VersoesOptions>()
            .Bind(configuration.GetSection(VersoesOptions.Secao))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<VersoesOptions>, ValidacaoDeVersoesOptions>();

        services.AddScoped<IEnderecosDeVersaoRepository, EnderecosDeVersaoRepository>();

        services.AddHttpClient<ILeitorDeArquivoDeVersao, LeitorDeArquivoDeVersao>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = RedirecionamentosMaximos,
                UseCookies = false,
                Credentials = null,
            });

        return services;
    }

    /// <summary>Validação da subida com os motivos de <see cref="VersoesOptions.Validar"/>, em português.</summary>
    private sealed class ValidacaoDeVersoesOptions : IValidateOptions<VersoesOptions>
    {
        public ValidateOptionsResult Validate(string? name, VersoesOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            var motivos = options.Validar();
            return motivos.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(motivos);
        }
    }
}
