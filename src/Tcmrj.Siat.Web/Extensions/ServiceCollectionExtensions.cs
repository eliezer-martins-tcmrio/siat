using FluentValidation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetDevPack.SimpleMediator;
using Tcmrj.Siat.App.Web.Services;
using Tcmrj.Siat.App.Web.Services.Interfaces;
using Tcmrj.Siat.Infra.Configurations;
using Tcmrj.Siat.Web.Services;

namespace Tcmrj.Siat.Web.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra as features, os validadores, os services do App, a lista fechada de servidores, as consultas, o
    /// catálogo de sistemas, as versões publicadas e a <see cref="IInteracao"/>. Tudo que depende do usuário é Scoped (um por circuito).
    /// </summary>
    public static WebApplicationBuilder AddTcmrjSiat(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;
        var appAssembly = typeof(IRegistroConsultaService).Assembly;

        // SimpleMediator com o mediador autorizado do modelo e os handlers do App.
        services.AddScoped<IMediator, AuthorizedMediator>();
        services.AddMediatorHandlers(appAssembly);

        services.AddValidatorsFromAssembly(appAssembly, includeInternalTypes: true);

        services.AddScoped<IUsuarioAtualService, UsuarioAtualService>();
        services.AddScoped<IAutorizacaoConsultaService, AutorizacaoConsultaService>();
        services.AddScoped<IRegistroConsultaService, RegistroConsultaService>();
        services.AddScoped<IInteracao, Interacao>();

        // Lista fechada (servidores.json, adicionado à configuração no Program) e acesso aos bancos consultados.
        services.AddServidores(builder.Configuration);
        services.AddConsultas(builder.Configuration);

        // MailQueue: repositório do banco MailQueue de cada servidor (história 1).
        services.AddScoped<Tcmrj.Siat.Domain.Repositories.IMailQueueRepository, Tcmrj.Siat.Infra.Repositories.MailQueueRepository>();

        // AuditLog: repositório do AUDITORIADB e formatadores do detalhe (sem estado).
        services.AddScoped<Tcmrj.Siat.Domain.Repositories.IAuditLogRepository, Tcmrj.Siat.Infra.Repositories.AuditLogRepository>();
        services.AddSingleton<IFormatadorStackTrace, FormatadorStackTrace>();
        services.AddSingleton<IFormatadorLogEvent, FormatadorLogEvent>();

        // Catálogo de Sistemas: leitura do sistemasDb.js no GitLab (HttpClient tipado) e catálogo em memória (singleton).
        // Sem configuração válida, só a tela de Sistemas mostra o erro; o SIAT sobe normalmente (Princípio IV).
        services.AddCatalogoSistemas(builder.Configuration);
        services.Configure<CacheDoCatalogoOptions>(builder.Configuration.GetSection(CacheDoCatalogoOptions.Secao));
        services.AddSingleton<ICatalogoSistemasService, CatalogoSistemasService>();

        // Versões publicadas: tabela de endereços (servidor escolhido) e arquivos de versão (UNC e http/https), com o
        // resultado em memória por servidor (singleton). Sem a seção Versoes, valem os padrões (Princípio IV).
        services.AddVersoes(builder.Configuration);
        services.Configure<CacheDeVersoesOptions>(builder.Configuration.GetSection(CacheDeVersoesOptions.Secao));
        services.AddTransient<LeitorDeVersoes>();
        services.AddSingleton<IVersoesPublicadasService, VersoesPublicadasService>();

        // Exportação em HTML autocontido (história 4; sem estado).
        services.AddSingleton<IExportacaoHtmlService, ExportacaoHtmlService>();

        services.TryAddSingleton(TimeProvider.System);

        return builder;
    }
}
