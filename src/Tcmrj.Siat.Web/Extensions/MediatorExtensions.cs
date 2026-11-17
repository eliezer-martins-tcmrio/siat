using System.Reflection;
using NetDevPack.SimpleMediator;

namespace Tcmrj.Siat.Web.Extensions;

public static class MediatorExtensions
{
    /// <summary>Registra como transient todos os <see cref="IRequestHandler{TRequest, TResponse}"/> do assembly.</summary>
    public static IServiceCollection AddMediatorHandlers(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetInterfaces().Any(EhRequestHandler));

        foreach (var handlerType in handlerTypes)
        {
            foreach (var @interface in handlerType.GetInterfaces().Where(EhRequestHandler))
            {
                services.AddTransient(@interface, handlerType);
            }
        }

        return services;
    }

    private static bool EhRequestHandler(Type tipo)
        => tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(IRequestHandler<,>);
}
