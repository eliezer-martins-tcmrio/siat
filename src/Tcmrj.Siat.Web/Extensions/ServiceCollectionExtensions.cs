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
    /// Registra as features, os validadores, os services do App, a lista fechada de servidores, as consultas
    /// e a <see cref="IInteracao"/>. Tudo que depende do usuário é Scoped (um por circuito).
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

        // Exportação em HTML autocontido (história 4; sem estado).
        services.AddSingleton<IExportacaoHtmlService, ExportacaoHtmlService>();

        services.TryAddSingleton(TimeProvider.System);

        return builder;
    }
}
