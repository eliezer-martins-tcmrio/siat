using Tcmrj.Autorizacao.App.Services;
using Tcmrj.CoreLib.DBLog.AspNetCore.Extensions;

namespace Tcmrj.Siat.Web.Configurations;

/// <summary>
/// Registro no <c>AUDITORIADB.audit.Log</c> pelo DBLog (R7). O pacote lê a connection string
/// <c>ConnectionStrings:AuditoriaDbConnection</c> e grava só eventos com <c>LogUID</c> e nível Error.
/// </summary>
public static class DBLogConfiguration
{
    public static WebApplicationBuilder AddDBLogConfiguration(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Seção opcional "DBLog" (PreLogID, OnErrorRedirect); usada só pelo UseTcmrjExceptionHandler.
        builder.Services.AddTcmrjDBLog(builder.Configuration);
        builder.Host.UseTcmrjDBLog();

        return builder;
    }

    public static WebApplication UseDBLogConfiguration(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // AppID dos registros = o do SIAT no UserManager. Um valor <= 0 é recusado na inicialização
        // pela validação de UserManagerServiceOptions (AuthenticationConfiguration).
        var applicationId = app.Configuration.GetValue<int>(
            $"{UserManagerServiceOptions.UserManagerService}:{nameof(UserManagerServiceOptions.ApplicationId)}");
        app.UseTcmrjDBLog(applicationId);

        return app;
    }
}
