using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tcmrj.Siat.Domain.Repositories;
using Tcmrj.Siat.Infra.Copilot;

namespace Tcmrj.Siat.Infra.Configurations;

public static class CopilotConfiguration
{
    /// <summary>Endereço da API REST do GitHub.</summary>
    public static readonly Uri EnderecoDaApi = new("https://api.github.com/");

    /// <summary>Tamanho máximo aceito numa resposta do GitHub (uma página de 100 itens tem dezenas de KB).</summary>
    public const long TamanhoMaximoDaResposta = 10 * 1024 * 1024;

    /// <summary>
    /// Liga <see cref="CopilotOptions"/> à seção <c>Copilot</c>, sem validar na subida (a validação é na leitura,
    /// Princípio IV), e registra o <see cref="IGitHubCopilotRepository"/> com um <see cref="HttpClient"/> tipado:
    /// endereço da API, tempo limite de <see cref="GitHubCopilotOptions.TempoLimiteSegundos"/> e sem seguir
    /// redirecionamentos, para o cabeçalho com o token nunca ser reenviado a outro endereço (contracts/github-api.md).
    /// </summary>
    public static IServiceCollection AddCopilotConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<CopilotOptions>(configuration.GetSection(CopilotOptions.Secao));

        services.AddHttpClient<IGitHubCopilotRepository, GitHubCopilotRepository>((provider, client) =>
            {
                client.BaseAddress = EnderecoDaApi;

                // Fora do intervalo, o repositório devolve NaoConfigurado antes de qualquer chamada.
                var segundos = provider.GetRequiredService<IOptionsMonitor<CopilotOptions>>().CurrentValue.GitHub?.TempoLimiteSegundos ?? 0;
                if (segundos is >= CopilotOptions.MinimoTempoLimite and <= CopilotOptions.MaximoTempoLimite)
                {
                    client.Timeout = TimeSpan.FromSeconds(segundos);
                }

                client.MaxResponseContentBufferSize = TamanhoMaximoDaResposta;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

        return services;
    }
}
